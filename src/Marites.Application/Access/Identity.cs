using Marites.Domain.Access;

namespace Marites.Application.Access;

/// <summary>
/// A token the provider vouched for: the identity, and — for Entra — the App Role it carries.
/// Google carries no role; its role comes from the explicit mapping.
/// </summary>
public sealed record VerifiedIdentity(string Issuer, string Subject, string Email, string? Role = null);

/// <summary>
/// Validates a provider's token. Google and Entra each implement this; the API never trusts a
/// claim that has not passed through it (ADR-008).
/// </summary>
public interface IIdentityTokenValidator
{
    Task<VerifiedIdentity?> ValidateAsync(string token, CancellationToken cancellationToken);
}

/// <summary>Who is signed in, and the CSRF token their client must echo on writes.</summary>
public sealed record SessionPrincipal(
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string CsrfToken);

/// <summary>Issues and reads the signed session token carried in the cookie.</summary>
public interface ISessionTokens
{
    (string Token, SessionPrincipal Principal) Issue(AppUser user, DateTimeOffset now);

    /// <summary>
    /// Verifies the signature and expiry only. The caller still checks that the user exists and
    /// that the session predates their last revocation — a token cannot know either.
    /// </summary>
    SessionPrincipal? Read(string token);
}

/// <summary>The explicit Marites role mapping (ADR-008 amendment).</summary>
public interface IUserDirectory
{
    Task<AppUser?> FindBySubjectAsync(string issuer, string subject, CancellationToken cancellationToken);

    Task<AppUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken cancellationToken);

    Task<AppUser> GrantAsync(
        string issuer, string subject, string email, string role, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(string issuer, string subject, CancellationToken cancellationToken);

    Task RevokeSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum SignInStatus
{
    SignedIn,
    InvalidToken,
    Unmapped
}

public sealed record SignInOutcome(
    SignInStatus Status,
    SessionPrincipal? Principal = null,
    string? Token = null,
    string? Subject = null);

/// <summary>
/// The sign-in exchange, the session read, and logout — the three operations Google mode adds.
/// </summary>
/// <remarks>
/// The provider proves identity; this service decides whether that identity has a role. An
/// unmapped identity is refused rather than defaulted, and the refusal names its own subject so
/// an operator can grant it.
/// </remarks>
public sealed class IdentityService(
    IIdentityTokenValidator validator,
    ISessionTokens sessions,
    IUserDirectory users,
    TimeProvider clock)
{
    public async Task<SignInOutcome> SignInAsync(string token, CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(token, cancellationToken);

        if (identity is null)
        {
            return new SignInOutcome(SignInStatus.InvalidToken);
        }

        var user = await users.FindBySubjectAsync(identity.Issuer, identity.Subject, cancellationToken);

        if (user is null)
        {
            return new SignInOutcome(SignInStatus.Unmapped, Subject: identity.Subject);
        }

        var (issued, principal) = sessions.Issue(user, clock.GetUtcNow());
        return new SignInOutcome(SignInStatus.SignedIn, principal, issued);
    }

    /// <summary>
    /// The session behind a cookie value, or null. A token signed before the user's last
    /// revocation is refused even though its signature is valid — that is what logout means.
    /// </summary>
    public async Task<SessionPrincipal?> ReadSessionAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var principal = sessions.Read(token);
        if (principal is null)
        {
            return null;
        }

        var user = await users.FindByIdAsync(principal.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.SessionsRevokedAt is { } revoked && principal.IssuedAt <= revoked)
        {
            return null;
        }

        // The role comes from the directory, not the token: a role change takes effect on the
        // next request rather than at the next sign-in.
        return principal with { Role = user.Role, Email = user.Email };
    }

    public Task LogoutAsync(Guid userId, CancellationToken cancellationToken) =>
        users.RevokeSessionsAsync(userId, clock.GetUtcNow(), cancellationToken);
}

/// <summary>
/// Role → capability, in one place (DOC-UX-001 §2). Endpoints name a capability; the policy
/// checks the role. The matrix is identical for Entra and Google.
/// </summary>
public static class Capabilities
{
    public const string Read = "read";
    public const string ManageBrokers = "manage_brokers";
    public const string Peek = "peek";
    public const string Discard = "discard";
    public const string Purge = "purge";
    public const string Replay = "replay";

    private static readonly IReadOnlyDictionary<string, string[]> ByRole =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [AppRoles.Viewer] = [Read],
            [AppRoles.Support] = [Read, Peek, Discard, Replay],
            [AppRoles.Admin] = [Read, ManageBrokers, Peek, Discard, Purge, Replay],
        };

    public static IReadOnlyList<string> ForRole(string role) =>
        ByRole.TryGetValue(role, out var capabilities) ? capabilities : [];

    public static bool Allows(string role, string capability) =>
        ForRole(role).Contains(capability, StringComparer.Ordinal);
}
