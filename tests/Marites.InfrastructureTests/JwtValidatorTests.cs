using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marites.Infrastructure.Access;

namespace Marites.InfrastructureTests;

/// <summary>
/// The signature, issuer, audience and expiry checks — each one refused on its own.
/// </summary>
/// <remarks>
/// The key set is served by a stub handler and the tokens are signed in the test with a
/// generated RSA key, so a green run means the validator checked a real signature rather than
/// that a mock said yes.
/// </remarks>
public sealed class JwtValidatorTests
{
    private const string Issuer = "https://accounts.google.com";
    private const string Audience = "test-client.apps.googleusercontent.com";

    [Fact]
    public async Task A_valid_token_yields_its_claims()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        var claims = await validator.ValidateAsync(keys.Token(), default);

        Assert.NotNull(claims);
        Assert.Equal("sub-1", claims!.Value.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task A_tampered_payload_fails_the_signature()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        var parts = keys.Token().Split('.');
        var tampered = JsonSerializer.Serialize(new
        {
            iss = Issuer,
            aud = Audience,
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            sub = "sub-attacker",
        });

        var token = $"{parts[0]}.{Base64Url(Encoding.UTF8.GetBytes(tampered))}.{parts[2]}";

        Assert.Null(await validator.ValidateAsync(token, default));
    }

    [Fact]
    public async Task A_token_for_another_audience_is_refused()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        Assert.Null(await validator.ValidateAsync(keys.Token(audience: "someone-else"), default));
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        Assert.Null(await validator.ValidateAsync(keys.Token(issuer: "https://evil.example"), default));
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        var expired = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

        Assert.Null(await validator.ValidateAsync(keys.Token(expiry: expired), default));
    }

    [Fact]
    public async Task A_token_issued_in_the_future_is_refused()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        var future = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        Assert.Null(await validator.ValidateAsync(keys.Token(issuedAt: future), default));
    }

    [Fact]
    public async Task An_unsigned_token_is_refused()
    {
        using var keys = new TestKeys();
        var validator = keys.Validator();

        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"none","kid":"k1"}"""));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iss = Issuer,
            aud = Audience,
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            sub = "sub-1",
        })));

        Assert.Null(await validator.ValidateAsync($"{header}.{payload}.", default));
    }

    [Fact]
    public async Task A_token_signed_by_an_unknown_key_is_refused()
    {
        using var keys = new TestKeys();
        using var other = new TestKeys();
        var validator = keys.Validator();

        // Signed by a key whose kid is not in the published set.
        Assert.Null(await validator.ValidateAsync(other.Token(kid: "not-published"), default));
    }

    private sealed class TestKeys : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);
        private readonly string _kid = $"kid-{Guid.NewGuid():N}";

        public JwtValidator Validator() =>
            new(new HttpClient(new JwksHandler(Json())), [Issuer], Audience, "https://keys.example/jwks");

        public string Token(
            string issuer = Issuer,
            string audience = Audience,
            long? expiry = null,
            long? issuedAt = null,
            string? kid = null)
        {
            var header = Base64Url(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { alg = "RS256", kid = kid ?? _kid, typ = "JWT" })));

            var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                iss = issuer,
                aud = audience,
                exp = expiry ?? DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
                iat = issuedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sub = "sub-1",
            })));

            var signature = _rsa.SignData(
                Encoding.ASCII.GetBytes($"{header}.{payload}"),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            return $"{header}.{payload}.{Base64Url(signature)}";
        }

        private string Json()
        {
            var parameters = _rsa.ExportParameters(false);

            return JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        kid = _kid,
                        use = "sig",
                        alg = "RS256",
                        n = Base64Url(parameters.Modulus!),
                        e = Base64Url(parameters.Exponent!),
                    },
                },
            });
        }

        public void Dispose() => _rsa.Dispose();
    }

    private sealed class JwksHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
