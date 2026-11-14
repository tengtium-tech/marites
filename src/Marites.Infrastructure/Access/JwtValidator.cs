using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Marites.Infrastructure.Access;

/// <summary>
/// A compact JWT validator for RS256 tokens against a published key set.
/// </summary>
/// <remarks>
/// Hand-rolled on <see cref="RSA"/> rather than a token library because the product needs
/// exactly one algorithm, two providers and no inbound claim mapping — and because a dependency
/// that quietly accepts `alg: none`, or an audience as a substring, is a liability in the one
/// place where being wrong is total. The checks here are explicit and each one has a test.
/// </remarks>
public sealed class JwtValidator
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient _http;
    private readonly IReadOnlySet<string> _issuers;
    private readonly string _audience;
    private readonly string _jwksUri;
    private readonly ConcurrentDictionary<string, (RSAParameters Parameters, DateTimeOffset FetchedAt)> _keys = new();

    public JwtValidator(HttpClient http, IEnumerable<string> issuers, string audience, string jwksUri)
    {
        _http = http;
        _issuers = issuers.ToHashSet(StringComparer.Ordinal);
        _audience = audience;
        _jwksUri = jwksUri;
    }

    /// <summary>The verified claims, or null when any check fails. Never throws for a bad token.</summary>
    public async Task<JsonElement?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        JsonElement header;
        JsonElement payload;

        try
        {
            using var headerDocument = JsonDocument.Parse(Base64Url.Decode(parts[0]));
            header = headerDocument.RootElement.Clone();

            using var payloadDocument = JsonDocument.Parse(Base64Url.Decode(parts[1]));
            payload = payloadDocument.RootElement.Clone();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }

        if (!header.TryGetProperty("alg", out var algorithm)
            || algorithm.GetString() != "RS256"
            || !header.TryGetProperty("kid", out var kidElement)
            || kidElement.GetString() is not { Length: > 0 } kid)
        {
            return null;
        }

        var key = await KeyAsync(kid, cancellationToken);
        if (key is null)
        {
            return null;
        }

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64Url.Decode(parts[2]);

        using var rsa = RSA.Create();
        rsa.ImportParameters(key.Value);

        if (!rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (!payload.TryGetProperty("iss", out var issuer) || !_issuers.Contains(issuer.GetString() ?? string.Empty))
        {
            return null;
        }

        if (!AudienceMatches(payload))
        {
            return null;
        }

        if (!payload.TryGetProperty("exp", out var expiry) || expiry.GetInt64() < now.Subtract(ClockSkew).ToUnixTimeSeconds())
        {
            return null;
        }

        if (payload.TryGetProperty("iat", out var issuedAt) && issuedAt.GetInt64() > now.Add(ClockSkew).ToUnixTimeSeconds())
        {
            return null;
        }

        if (payload.TryGetProperty("nbf", out var notBefore) && notBefore.GetInt64() > now.Add(ClockSkew).ToUnixTimeSeconds())
        {
            return null;
        }

        return payload;
    }

    private bool AudienceMatches(JsonElement payload)
    {
        if (!payload.TryGetProperty("aud", out var audience))
        {
            return false;
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audience.GetString(), _audience, StringComparison.Ordinal),
            JsonValueKind.Array => audience.EnumerateArray()
                .Any(entry => string.Equals(entry.GetString(), _audience, StringComparison.Ordinal)),
            _ => false,
        };
    }

    private async Task<RSAParameters?> KeyAsync(string kid, CancellationToken cancellationToken)
    {
        if (_keys.TryGetValue(kid, out var cached) && cached.FetchedAt > DateTimeOffset.UtcNow.Subtract(KeyCacheLifetime))
        {
            return cached.Parameters;
        }

        // A miss is either a rotation or a forgery. Refreshing on a miss handles rotation; a
        // forgery simply never finds a key and is refused.
        await RefreshAsync(cancellationToken);

        return _keys.TryGetValue(kid, out var refreshed) ? refreshed.Parameters : null;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(_jwksUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!document.RootElement.TryGetProperty("keys", out var keys))
        {
            return;
        }

        var fetchedAt = DateTimeOffset.UtcNow;

        foreach (var key in keys.EnumerateArray())
        {
            if (key.TryGetProperty("kty", out var type) && type.GetString() != "RSA")
            {
                continue;
            }

            if (!key.TryGetProperty("kid", out var kid) || kid.GetString() is not { Length: > 0 } identifier)
            {
                continue;
            }

            if (!key.TryGetProperty("n", out var modulus) || !key.TryGetProperty("e", out var exponent))
            {
                continue;
            }

            _keys[identifier] = (new RSAParameters
            {
                Modulus = Base64Url.Decode(modulus.GetString() ?? string.Empty),
                Exponent = Base64Url.Decode(exponent.GetString() ?? string.Empty),
            }, fetchedAt);
        }
    }

    private static class Base64Url
    {
        public static byte[] Decode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            return Convert.FromBase64String(padded);
        }
    }
}
