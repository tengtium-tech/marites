using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marites.Api;
using Marites.Application.Access;
using Marites.Application.Brokers;
using Marites.Application.Preflight;
using Marites.Application.Queues;
using Marites.Domain.Access;
using Marites.Infrastructure.Access;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marites.ApiTests;

/// <summary>
/// OP-AUTH-001…003 and role enforcement, in google mode, against a host built for this class.
/// </summary>
/// <remarks>
/// The identity provider is stubbed: a token is a dictionary key, and validation is not under
/// test here — the exchange, the mapping refusal, the session, the CSRF check and the role
/// policies are. <c>JwtValidator</c> has its own tests with generated keys.
/// </remarks>
public sealed class AuthApiTests : IAsyncLifetime
{
    private static readonly string GoogleIssuer = GoogleIdentityProvider.Issuer;

    private readonly StubIdentityValidator _validator = new();
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"marites-auth-{Guid.NewGuid():N}.db");

    private WebApplication? _app;
    private CookieContainer _cookies = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("MARITES_BOOKKEEPING_PATH", _databasePath);
        Environment.SetEnvironmentVariable("MARITES_ADMIN_ADDR", "127.0.0.1:0");
        Environment.SetEnvironmentVariable("MARITES_AUTH_MODE", "google");
        Environment.SetEnvironmentVariable("MARITES_GOOGLE_CLIENT_ID", "test-client.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("MARITES_SESSION_HOURS", "8");
        Environment.SetEnvironmentVariable("MARITES_KEK", Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

        await using (var context = new Marites.Infrastructure.Bookkeeping.BookkeepingDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Marites.Infrastructure.Bookkeeping.BookkeepingDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .Options))
        {
            await new Marites.Infrastructure.Bookkeeping.BookkeepingMigrator(context)
                .MigrateAndSeedAsync("Default", "default", DateTimeOffset.UtcNow);
        }

        _app = ApiHost.Build(
            ["--urls", "http://127.0.0.1:0"],
            services =>
            {
                services.AddSingleton<IBrokerProbe>(_ => new StubBrokerProbe());
                services.AddSingleton<IIdentityTokenValidator>(_validator);
                services.AddSingleton(new StubQueueCatalog());
                services.AddSingleton<IQueueCatalog>(provider => provider.GetRequiredService<StubQueueCatalog>());
                services.AddSingleton(new StubBrokerAdmin());
                services.AddSingleton<IBrokerAdmin>(provider => provider.GetRequiredService<StubBrokerAdmin>());
            });

        await _app.StartAsync();

        _cookies = new CookieContainer();
        _client = new HttpClient(new HttpClientHandler { CookieContainer = _cookies })
        {
            BaseAddress = new Uri(_app.Urls.First()),
        };

        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

        await users.GrantAsync(GoogleIssuer, "sub-admin", "admintestuser001@gmail.com", AppRoles.Admin, default);
        await users.GrantAsync(GoogleIssuer, "sub-support", "supporttestuser001@gmail.com", AppRoles.Support, default);
        await users.GrantAsync(GoogleIssuer, "sub-viewer", "executivetestuser001@gmail.com", AppRoles.Viewer, default);

        _validator.Tokens["token-admin"] = new VerifiedIdentity(GoogleIssuer, "sub-admin", "admintestuser001@gmail.com");
        _validator.Tokens["token-support"] = new VerifiedIdentity(GoogleIssuer, "sub-support", "supporttestuser001@gmail.com");
        _validator.Tokens["token-viewer"] = new VerifiedIdentity(GoogleIssuer, "sub-viewer", "executivetestuser001@gmail.com");
        _validator.Tokens["token-unmapped"] = new VerifiedIdentity(GoogleIssuer, "sub-stranger", "stranger@example.com");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    // ------------------------------------------------------------------
    // Sign-in and session
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_mapped_google_account_receives_a_session_and_its_role()
    {
        var response = await SignInAsync("token-admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Admin", (string?)body["user"]!["role"]);
        Assert.Equal("admintestuser001@gmail.com", (string?)body["user"]!["email"]);
        Assert.NotEmpty((string?)body["csrfToken"] ?? string.Empty);

        var capabilities = body["user"]!["capabilities"]!.AsArray().Select(node => (string)node!).ToList();
        Assert.Contains("purge", capabilities);

        // The session cookie is HttpOnly and carries no Google token.
        var sessionCookie = _cookies.GetCookies(new Uri(_client.BaseAddress!, "/"))["marites_session"];
        Assert.NotNull(sessionCookie);
        Assert.True(sessionCookie!.HttpOnly);
    }

    [Fact]
    public async Task An_unmapped_google_account_is_refused_and_gets_no_session()
    {
        var response = await SignInAsync("token-unmapped");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("ERR-AUTH-002", (string?)body["code"]);
        Assert.Equal("sub-stranger", (string?)body["subject"]);

        Assert.Null(_cookies.GetCookies(new Uri(_client.BaseAddress!, "/"))["marites_session"]);
    }

    [Fact]
    public async Task An_invalid_token_is_401_not_403()
    {
        var response = await SignInAsync("token-that-does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("ERR-AUTH-001", await CodeAsync(response));
    }

    [Fact]
    public async Task The_session_read_needs_the_cookie_and_returns_the_csrf_token()
    {
        var anonymous = await _client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("ERR-AUTH-001", await CodeAsync(anonymous));

        await SignInAsync("token-viewer");

        var session = await _client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        var body = JsonNode.Parse(await session.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Viewer", (string?)body["user"]!["role"]);
        Assert.NotEmpty((string?)body["csrfToken"] ?? string.Empty);
    }

    // ------------------------------------------------------------------
    // Roles, direct API calls
    // ------------------------------------------------------------------

    [Fact]
    public async Task Viewer_may_read_and_may_not_write()
    {
        await SignInAsync("token-viewer");
        var csrf = CsrfToken();

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/capabilities")).StatusCode);

        var write = await WriteAsync(HttpMethod.Post, "/api/v1/brokers", BrokerBody("viewer-try"), csrf);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("ERR-AUTH-002", await CodeAsync(write));
    }

    [Fact]
    public async Task Support_may_peek_and_may_not_manage_brokers()
    {
        await SignInAsync("token-support");
        var csrf = CsrfToken();

        var queue = new QueueSummaryView(
            Guid.NewGuid(), Guid.NewGuid(), "auth.queue", "/", "queue", false,
            3, 1, 0, null, null, null, "disabled", true);

        _app!.Services.GetRequiredService<StubQueueCatalog>().Queues = [queue];
        _app.Services.GetRequiredService<StubBrokerAdmin>().PeekResult =
            new PeekResult([], false);

        var peek = await WriteAsync(
            HttpMethod.Post, "/api/v1/operations/peek",
            JsonSerializer.Serialize(new { queueId = queue.Id }), csrf);

        Assert.Equal(HttpStatusCode.OK, peek.StatusCode);

        var write = await WriteAsync(HttpMethod.Post, "/api/v1/brokers", BrokerBody("support-try"), csrf);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Admin_may_manage_brokers_when_the_csrf_token_is_present()
    {
        await SignInAsync("token-admin");

        var created = await WriteAsync(
            HttpMethod.Post, "/api/v1/brokers", BrokerBody("admin-ok"), CsrfToken());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task A_cookie_authenticated_write_without_the_csrf_token_is_refused()
    {
        await SignInAsync("token-admin");

        var refused = await WriteAsync(HttpMethod.Post, "/api/v1/brokers", BrokerBody("csrf-missing"), csrf: null);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("ERR-AUTH-002", await CodeAsync(refused));

        var detail = JsonNode.Parse(await refused.Content.ReadAsStringAsync())!.AsObject();
        Assert.Contains("CSRF", (string?)detail["title"]);
    }

    // ------------------------------------------------------------------
    // Logout
    // ------------------------------------------------------------------

    [Fact]
    public async Task Logout_revokes_every_session_that_user_holds()
    {
        await SignInAsync("token-admin");

        // A second "device": the same identity, its own cookie jar.
        var otherJar = new CookieContainer();
        using var otherClient = new HttpClient(new HttpClientHandler { CookieContainer = otherJar })
        {
            BaseAddress = _client.BaseAddress,
        };

        var second = await otherClient.PostAsJsonAsync("/api/v1/auth/google", new { idToken = "token-admin" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var logout = await WriteAsync(HttpMethod.Post, "/api/v1/auth/logout", "{}", CsrfToken());
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/auth/session")).StatusCode);

        var otherCookie = otherJar.GetCookies(new Uri(_client.BaseAddress!, "/"))["marites_session"]!.Value;
        using var check = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
        check.Headers.Add("Cookie", $"marites_session={otherCookie}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(check)).StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private Task<HttpResponseMessage> SignInAsync(string token) =>
        _client.PostAsJsonAsync("/api/v1/auth/google", new { idToken = token });

    private string CsrfToken() =>
        _cookies.GetCookies(new Uri(_client.BaseAddress!, "/"))["marites_csrf"]?.Value
        ?? throw new InvalidOperationException("No CSRF cookie; sign in first.");

    private Task<HttpResponseMessage> WriteAsync(HttpMethod method, string path, string body, string? csrf)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-Token", csrf);
        }

        return _client.SendAsync(request);
    }

    private static string BrokerBody(string name) => JsonSerializer.Serialize(new
    {
        name = $"{name}-{Guid.NewGuid():N}",
        providerKind = "rabbitmq",
        environment = "dev",
        credentialKind = "basic",
        endpoint = "amqp://ok.invalid/h",
    });

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (string?)JsonNode.Parse(await response.Content.ReadAsStringAsync())!["code"];
}

public sealed class StubIdentityValidator : IIdentityTokenValidator
{
    public Dictionary<string, VerifiedIdentity> Tokens { get; } = new(StringComparer.Ordinal);

    public Task<VerifiedIdentity?> ValidateAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.GetValueOrDefault(token));
}
