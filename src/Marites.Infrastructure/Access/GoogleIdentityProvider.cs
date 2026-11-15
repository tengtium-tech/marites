using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marites.Application.Access;
using Marites.Domain.Access;
using Marites.Infrastructure.Security;

namespace Marites.Infrastructure.Access;

/// <summary>Google sign-in: an ID token, validated against Google's published keys.</summary>
/// <remarks>
/// Both issuer spellings are accepted because Google issues one and documents the other
/// (OpenID Connect: <c>https://accounts.google.com</c>; some libraries emit
/// <c>accounts.google.com</c>). The audience is the client ID — a token minted for another app
/// is refused even though its signature is genuine.
/// </remarks>
public sealed class GoogleIdentityProvider : IIdentityTokenValidator
{
    public const string Issuer = "https://accounts.google.com";
    public const string IssuerShort = "accounts.google.com";
    private const string JwksUri = "https://www.googleapis.com/oauth2/v3/certs";

    private readonly JwtValidator _validator;

    public GoogleIdentityProvider(HttpClient http, string clientId)
    {
        _validator = new JwtValidator(http, [Issuer, IssuerShort], clientId, JwksUri);
    }

    public async Task<VerifiedIdentity?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var claims = await _validator.ValidateAsync(token, cancellationToken);

        if (claims is not { } payload
            || !payload.TryGetProperty("sub", out var subject)
            || subject.GetString() is not { Length: > 0 } identifier)
        {
            return null;
        }

        var email = payload.TryGetProperty("email", out var claim) ? claim.GetString() : null;

        // The subject is the identity; the email is display data. A token without the email
        // scope still signs in, because the mapping is by subject.
        return new VerifiedIdentity(Issuer, identifier, email ?? string.Empty);
    }
}

/// <summary>
/// The Marites session: a signed, expiring token in an HttpOnly cookie.
/// </summary>
/// <remarks>
/// Signed with a key derived from <c>MARITES_KEK</c> rather than a new secret, so an install
/// that can decrypt credentials can also issue sessions and there is one root of trust to
/// protect. The payload is readable — it holds a user id, a role, timestamps and a CSRF token,
/// none of which is secret — but not modifiable without the key.
/// </remarks>
public sealed class HmacSessionTokens : ISessionTokens
{
    private static readonly byte[] DerivationInfo = Encoding.UTF8.GetBytes("marites-session-v1");

    private readonly byte[] _key;
    private readonly TimeSpan _lifetime;

    public HmacSessionTokens(KekOptions keys, TimeSpan lifetime)
    {
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, keys.Current, 32, salt: [], info: DerivationInfo);
        _lifetime = lifetime;
    }

    public (string Token, SessionPrincipal Principal) Issue(AppUser user, DateTimeOffset now)
    {
        var expiresAt = now.Add(_lifetime);
        var csrf = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));

        var payload = JsonSerializer.SerializeToUtf8Bytes(new SessionPayload
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            IssuedAt = now.ToUnixTimeSeconds(),
            ExpiresAt = expiresAt.ToUnixTimeSeconds(),
            Csrf = csrf,
        });

        var token = $"{Base64Url.Encode(payload)}.{Base64Url.Encode(Sign(payload))}";

        return (token, new SessionPrincipal(
            user.Id, user.Email, user.Role, now, expiresAt, csrf));
    }

    public SessionPrincipal? Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        byte[] payload;
        byte[] signature;

        try
        {
            payload = Base64Url.Decode(parts[0]);
            signature = Base64Url.Decode(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
        {
            return null;
        }

        SessionPayload? session;
        try
        {
            session = JsonSerializer.Deserialize<SessionPayload>(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        return new SessionPrincipal(
            session.UserId,
            session.Email,
            session.Role,
            DateTimeOffset.FromUnixTimeSeconds(session.IssuedAt),
            DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt),
            session.Csrf);
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_key, payload);

    private sealed record SessionPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("u")]
        public Guid UserId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("e")]
        public string Email { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("r")]
        public string Role { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("iat")]
        public long IssuedAt { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("exp")]
        public long ExpiresAt { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("c")]
        public string Csrf { get; init; } = string.Empty;
    }

    private static class Base64Url
    {
        public static string Encode(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Decode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            return Convert.FromBase64String(padded);
        }
    }
}
