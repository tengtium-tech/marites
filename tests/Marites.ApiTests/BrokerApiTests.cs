using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Marites.Infrastructure.Bookkeeping;
using Marites.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace Marites.ApiTests;

/// <summary>
/// The broker operations over HTTP, which is the only layer where "the route is where the
/// contract says it is" can be asserted. Every test in this class shares one fixture and runs
/// in sequence: the fixture sets process-wide environment variables, so parallel classes would
/// race for the same database.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BrokerApiTests
{
    private readonly BrokerApiFixture _fixture;

    public BrokerApiTests(BrokerApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_read_update_delete_uses_only_documented_statuses_and_never_returns_a_credential()
    {
        var client = _fixture.Client;

        var created = await CreateAsync(client, "sequence");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("\"1\"", created.Headers.ETag?.Tag);

        var createBody = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain("s3cret", createBody, StringComparison.OrdinalIgnoreCase);
        // credentialsUpdatedAt is a timestamp and legitimately contains the word; the secret must not appear.
        Assert.DoesNotContain("user:s3cret", createBody, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(createBody);
        var id = document.RootElement.GetProperty("id").GetString()!;

        var read = await client.GetAsync($"/api/v1/brokers/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("\"1\"", read.Headers.ETag?.Tag);
        Assert.DoesNotContain("s3cret", await read.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var update = await PatchAsync(client, id, "\"1\"", """{"name":"sequence-renamed"}""", "update-1");
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("\"2\"", update.Headers.ETag?.Tag);

        var delete = await DeleteAsync(client, id, "delete-1");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await client.GetAsync($"/api/v1/brokers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_and_changes_nothing()
    {
        var client = _fixture.Client;
        var id = await CreateAndGetIdAsync(client, "stale");

        var refused = await PatchAsync(client, id, "\"99\"", """{"name":"should-not-apply"}""", "stale-1");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var after = await client.GetAsync($"/api/v1/brokers/{id}");
        var body = await after.Content.ReadAsStringAsync();

        // The refusal must leave both the revision and the name untouched.
        Assert.Contains("\"revision\":1", body.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.DoesNotContain("should-not-apply", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_If_Match_is_a_precondition_failure_not_a_bad_request()
    {
        var client = _fixture.Client;
        var id = await CreateAndGetIdAsync(client, "no-if-match");

        var response = await PatchAsync(client, id, ifMatch: null, """{"name":"renamed"}""", "no-if-match-1");

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task A_repeated_key_replays_the_first_answer()
    {
        var client = _fixture.Client;
        const string body = """{"name":"replayed","providerKind":"rabbitmq","environment":"dev","credentialKind":"basic","endpoint":"amqp://h/v"}""";

        var first = await PostAsync(client, body, "replay-key");
        var second = await PostAsync(client, body, "replay-key");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_same_key_with_a_different_request_is_refused()
    {
        var client = _fixture.Client;

        var first = await PostAsync(
            client,
            """{"name":"key-once","providerKind":"rabbitmq","environment":"dev","credentialKind":"basic","endpoint":"amqp://h/v"}""",
            "reused-key");

        var second = await PostAsync(
            client,
            """{"name":"key-twice","providerKind":"rabbitmq","environment":"dev","credentialKind":"basic","endpoint":"amqp://h/v"}""",
            "reused-key");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("ERR-CMN-005", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_endpoint_is_stored_encrypted_and_returned_redacted()
    {
        var client = _fixture.Client;

        var created = await CreateAsync(client, "redacted", "amqp://user:s3cret@rabbit.internal:5672/orders");
        var body = await created.Content.ReadAsStringAsync();

        Assert.Contains("amqp://rabbit.internal:5672/orders", body, StringComparison.Ordinal);
        Assert.DoesNotContain("user", body, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_duplicate_name_is_refused()
    {
        var client = _fixture.Client;

        await CreateAsync(client, "duplicate");
        // A different key: the same key would replay the first answer instead of exercising
        // the name conflict.
        var again = await CreateAsync(client, "duplicate", key: "duplicate-second");

        Assert.Equal(ErrorCatalog.StatusFor("ERR-CMN-003"), (int)again.StatusCode);
        Assert.Contains("ERR-CMN-003", await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string name,
        string? endpoint = null,
        string? key = null)
    {
        var payload = new
        {
            name,
            providerKind = "rabbitmq",
            environment = "dev",
            credentialKind = "basic",
            endpoint = endpoint ?? "amqp://user:s3cret@rabbit.internal:5672/v",
            credentials = new Dictionary<string, string> { ["amqp"] = "user:s3cret" },
        };

        return PostAsync(client, JsonSerializer.Serialize(payload), key ?? $"create-{name}");
    }


    [Fact]
    public async Task An_unreachable_broker_refuses_creation_and_persists_nothing()
    {
        var client = _fixture.Client;
        var before = await CountBrokersAsync(client);

        var response = await CreateAsync(client, "unreachable-broker", "amqp://unreachable/v");

        // Both the status and the code come from the catalog: an assertion written against the
        // implementation would have passed while the code was wrong.
        Assert.Equal(ErrorCatalog.StatusFor("ERR-BRK-002"), (int)response.StatusCode);
        Assert.Contains("ERR-BRK-002", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(before, await CountBrokersAsync(client));
    }

    [Fact]
    public async Task Rejected_credentials_refuse_creation_and_persist_nothing()
    {
        var client = _fixture.Client;
        var before = await CountBrokersAsync(client);

        var response = await CreateAsync(client, "rejected-creds", "amqp://rejected/v");

        Assert.Equal(ErrorCatalog.StatusFor("ERR-BRK-004"), (int)response.StatusCode);
        Assert.Contains("ERR-BRK-004", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(before, await CountBrokersAsync(client));
    }

    [Fact]
    public async Task A_topology_denial_creates_a_failing_profile_with_its_checks()
    {
        var client = _fixture.Client;

        var response = await CreateAsync(client, "no-topology-broker", "amqp://no-topology/v");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // The resource and the evidence for its state arrive together (DEC-021).
        Assert.Equal("failing", root.GetProperty("preflightState").GetString());
        Assert.True(root.TryGetProperty("preflight", out var preflight));
        Assert.Equal("failing", preflight.GetProperty("state").GetString());

        var failing = preflight.GetProperty("checks").EnumerateArray()
            .Where(c => c.GetProperty("state").GetString() == "fail")
            .Select(c => c.GetProperty("id").GetString())
            .ToList();

        Assert.Contains("topology_write_permitted", failing);
    }

    private static async Task<int> CountBrokersAsync(HttpClient client)
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/v1/brokers"));
        return document.RootElement.GetArrayLength();
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string name, string endpoint) =>
        PostAsync(
            client,
            JsonSerializer.Serialize(new
            {
                name,
                providerKind = "rabbitmq",
                environment = "dev",
                credentialKind = "basic",
                endpoint,
                credentials = new Dictionary<string, string> { ["amqp"] = "u:p" },
            }),
            $"create-{name}");

    private static async Task<string> CreateAndGetIdAsync(HttpClient client, string name)
    {
        var created = await CreateAsync(client, name);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/brokers")
        {
            Content = JsonContent.Create(JsonDocument.Parse(body).RootElement),
        };

        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, string id, string? ifMatch, string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/brokers/{id}")
        {
            Content = JsonContent.Create(JsonDocument.Parse(body).RootElement),
        };

        request.Headers.Add("Idempotency-Key", key);

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string id, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/brokers/{id}");
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }
}

/// <summary>
/// One migrated store, one generated key, and one host started on a free port for the whole
/// class. Environment variables are process-wide, so this must stay the only fixture of its
/// kind in the assembly.
/// </summary>
/// <remarks>
/// The host is started through <see cref="ApiHost.Build"/> rather than
/// <c>WebApplicationFactory</c>: the entry point takes exactly one argument (api, worker, all
/// or migrate) and the factory invokes it with none, so the factory would never build a server.
/// Starting the real host also means these tests exercise Kestrel and the real route table,
/// which is what the contract-level cases are about.
/// </remarks>
public sealed class BrokerApiFixture : IAsyncLifetime
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"marites-api-{Guid.NewGuid():N}.db");

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("MARITES_BOOKKEEPING_PATH", _databasePath);
        Environment.SetEnvironmentVariable(
            "MARITES_KEK",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        await using (var context = new BookkeepingDbContext(
            new DbContextOptionsBuilder<BookkeepingDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .Options))
        {
            await new BookkeepingMigrator(context).MigrateAndSeedAsync("Default", "default", DateTimeOffset.UtcNow);
        }

        // DEC-021 makes creation depend on the broker; the stub keeps the API tests independent
        // of a container while the real probe is covered against the local stack.
        _app = ApiHost.Build(
            ["--urls", "http://127.0.0.1:0"],
            services => services.AddScoped<Marites.Application.Preflight.IBrokerProbe>(_ => new StubBrokerProbe()));
        await _app.StartAsync();

        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

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
}
