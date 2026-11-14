using Marites.Application.Access;
using Marites.Domain.Access;

namespace Marites.Infrastructure.Access;

/// <summary>
/// Entra ID: a JWT bearer token per request, with the role arriving as an App Role.
/// </summary>
/// <remarks>
/// Entra differs from Google in where permission comes from: the `roles` claim, not a local
/// mapping. That is ADR-008's original design and this amendment does not change it — the two
/// providers map into the same three roles, but they carry the role differently.
/// </remarks>
public sealed class EntraIdentityProvider : IIdentityTokenValidator
{
    private readonly JwtValidator _validator;

    public EntraIdentityProvider(HttpClient http, string tenantId, string audience)
    {
        var issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        var jwks = $"https://login.microsoftonline.com/{tenantId}/discovery/v2.0/keys";
        _validator = new JwtValidator(http, [issuer], audience, jwks);
    }

    public async Task<VerifiedIdentity?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var claims = await _validator.ValidateAsync(token, cancellationToken);

        if (claims is not { } payload
            || !payload.TryGetProperty("oid", out var objectId)
            || objectId.GetString() is not { Length: > 0 } subject)
        {
            return null;
        }

        var email = payload.TryGetProperty("preferred_username", out var name) ? name.GetString()
            : payload.TryGetProperty("email", out var mail) ? mail.GetString()
            : null;

        // The first recognised App Role wins. A token with no known role authenticates and is
        // then refused by every policy — 403, not 401: the identity is valid, the permission is not.
        string? role = null;
        if (payload.TryGetProperty("roles", out var roles) && roles.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            role = roles.EnumerateArray()
                .Select(entry => entry.GetString())
                .FirstOrDefault(value => value is not null && AppRoles.IsKnown(value));
        }

        return new VerifiedIdentity(
            "https://login.microsoftonline.com/", subject, email ?? string.Empty, role);
    }
}
