using System.Security.Claims;
using System.Text.Encodings.Web;
using Marites.Application.Access;
using Marites.Domain.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marites.Api.Auth;

/// <summary>
/// Authenticates a request according to the configured mode: a bearer token for Entra, the
/// signed session cookie for Google, an implicit Admin for <c>none</c>.
/// </summary>
/// <remarks>
/// A missing or invalid credential is <c>401 ERR-AUTH-001</c>; a valid identity whose role lacks
/// the capability is <c>403 ERR-AUTH-002</c>. The two are never merged — one means "sign in",
/// the other means "you cannot do this", and telling a signed-in Viewer their session expired
/// sends them round a loop that cannot help.
/// </remarks>
public sealed class MaritesAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthOptions auth,
    IdentityService identity,
    IIdentityTokenValidator? tokenValidator = null)
#pragma warning disable CS0618 // The clock overload is the only one this ASP.NET version exposes.
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
#pragma warning restore CS0618
{
    public const string SchemeName = "Marites";
    public const string AuthMethodClaim = "marites:auth_method";
    public const string CsrfClaim = "marites:csrf";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (auth.IsNone)
        {
            return AuthenticateResult.Success(Ticket(Guid.Empty, "local", AppRoles.Admin, "none", null));
        }

        if (auth.IsGoogle)
        {
            var cookie = Request.Cookies[AuthOptions.SessionCookie];
            var session = await identity.ReadSessionAsync(cookie, Context.RequestAborted);

            return session is null
                ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(
                    Ticket(session.UserId, session.Email, session.Role, "cookie", session.CsrfToken));
        }

        // Entra: the bearer token is the only credential, and its role claim is the permission.
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || tokenValidator is null)
        {
            return AuthenticateResult.NoResult();
        }

        var verified = await tokenValidator.ValidateAsync(header["Bearer ".Length..].Trim(), Context.RequestAborted);

        if (verified is null)
        {
            return AuthenticateResult.NoResult();
        }

        return AuthenticateResult.Success(
            Ticket(Guid.Empty, verified.Email, verified.Role ?? string.Empty, "bearer", null));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return ProblemAsync("ERR-AUTH-001", "Denied", "No valid credential",
            "The token or session is missing, expired, invalid or revoked. Sign in again.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return ProblemAsync("ERR-AUTH-002", "Denied", "Authenticated but not permitted",
            "This role does not hold the capability for this operation.");
    }

    private async Task ProblemAsync(string code, string outcome, string title, string detail)
    {
        Response.ContentType = "application/problem+json";

        await Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            title,
            status = Response.StatusCode,
            detail,
            code,
            outcome,
            retryable = false,
        });
    }

    private static AuthenticationTicket Ticket(
        Guid userId, string email, string role, string method, string? csrf)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new(AuthMethodClaim, method),
        };

        if (csrf is not null)
        {
            claims.Add(new Claim(CsrfClaim, csrf));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return new AuthenticationTicket(principal, SchemeName);
    }
}
