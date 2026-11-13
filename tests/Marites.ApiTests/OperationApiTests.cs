using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marites.Application.Brokers;
using Marites.Application.Queues;

namespace Marites.ApiTests;

/// <summary>
/// OP-OPR-001…004 over HTTP, against stubbed catalog and admin.
/// </summary>
/// <remarks>
/// The stubs answer without a broker, so every status and guard the contract declares is
/// assertable here. What the stub cannot prove — that the AMQP calls themselves work — is
/// covered by <c>RabbitMqAdminTests</c> against the local stack.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class OperationApiTests
{
    private static Guid ProfileId { get; } = Guid.NewGuid();

    private readonly BrokerApiFixture _fixture;

    public OperationApiTests(BrokerApiFixture fixture)
    {
        _fixture = fixture;
    }

    private static QueueSummaryView MakeQueue(Guid brokerId, string name, long? depth = 3) => new(
        Id: Guid.NewGuid(),
        BrokerId: brokerId,
        Name: name,
        Vhost: "/",
        SourceKind: "queue",
        IsDeadLetter: false,
        Depth: depth,
        Consumers: 1,
        Unacked: 0,
        PublishRate: null,
        DeliverRate: null,
        AckRate: null,
        CaptureState: "disabled",
        DeadLetterConfigured: true);

    private static string Body(object payload) => JsonSerializer.Serialize(payload);

    private Task<HttpResponseMessage> PostAsync(string url, string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("Idempotency-Key", key);
        return _fixture.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostWithoutKeyAsync(string url, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        return _fixture.Client.SendAsync(request);
    }

    /// <summary>
    /// Purge and replay read the broker profile for the production-environment check, so their
    /// queues must hang off a profile that actually exists — created through the API like any
    /// other tenant would.
    /// </summary>
    private async Task<Guid> CreateProfileAsync(string environment, string label)
    {
        var created = await PostAsync(
            "/api/v1/brokers",
            Body(new
            {
                name = $"{label}-{Guid.NewGuid():N}",
                providerKind = "rabbitmq",
                environment,
                credentialKind = "basic",
                endpoint = "amqp://ok.invalid/h",
            }),
            $"create-{label}-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var profile = JsonNode.Parse(await created.Content.ReadAsStringAsync())!.AsObject();
        return Guid.Parse((string)profile["id"]!);
    }

    // ------------------------------------------------------------------
    // Peek — OP-OPR-001
    // ------------------------------------------------------------------

    [Fact]
    public async Task Peek_requires_the_idempotency_key()
    {
        var queue = MakeQueue(ProfileId, "peek.key");
        _fixture.QueueCatalog.Queues = [queue];

        var response = await PostWithoutKeyAsync(
            "/api/v1/operations/peek", Body(new { queueId = queue.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ERR-CMN-001", await CodeAsync(response));
    }

    [Fact]
    public async Task Peek_of_an_unknown_queue_is_a_404()
    {
        var response = await PostAsync(
            "/api/v1/operations/peek", Body(new { queueId = Guid.NewGuid() }), "peek-404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("ERR-CMN-002", await CodeAsync(response));
    }

    [Fact]
    public async Task Peek_reports_its_own_destructiveness_and_the_messages()
    {
        var queue = MakeQueue(ProfileId, "peek.ok");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PeekResult = new PeekResult(
            [
                new PeekedMessage("m-1", "order.created", "orders", "application/json", 42,
                    Convert.ToBase64String("hello"u8.ToArray()),
                    new Dictionary<string, string> { ["x-death"] = "[]", ["correlation"] = "c-1" },
                    Redelivered: true),
            ],
            Truncated: false);

        var response = await PostAsync("/api/v1/operations/peek", Body(new { queueId = queue.Id, limit = 5 }), "peek-ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True((bool)root["destructive"]!);
        Assert.Single(root["messages"]!.AsArray());

        var message = root["messages"]![0]!.AsObject();
        Assert.Equal("m-1", (string?)message["messageId"]);
        Assert.Equal("order.created", (string?)message["routingKey"]);
        Assert.Equal("orders", (string?)message["exchange"]);
        Assert.Equal(42L, (long?)message["size"]);
        Assert.Equal(true, (bool?)message["redelivered"]);

        // One call, at the requested limit — a peek must not read past what the caller asked for.
        Assert.Equal($"peek:{queue.Name}:5", Assert.Single(_fixture.Admin.Calls));
    }

    [Fact]
    public async Task Peek_repeating_a_key_returns_the_recorded_outcome_as_AlreadyApplied()
    {
        var queue = MakeQueue(ProfileId, "peek.replay");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PeekResult = new PeekResult([], false);

        var first = await PostAsync(
            "/api/v1/operations/peek", Body(new { queueId = queue.Id }), "peek-repeat");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAsync("/api/v1/operations/peek", Body(new { queueId = queue.Id }), "peek-repeat");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var root = JsonNode.Parse(await second.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("AlreadyApplied", (string?)root["outcome"]);

        // The recorded answer is returned, not a second broker read.
        Assert.Single(_fixture.Admin.Calls);
    }

    [Fact]
    public async Task Peek_outside_the_limit_is_rejected()
    {
        var queue = MakeQueue(ProfileId, "peek.limit");
        _fixture.QueueCatalog.Queues = [queue];

        var response = await PostAsync("/api/v1/operations/peek", Body(new { queueId = queue.Id, limit = 101 }), "peek-limit");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-003", await CodeAsync(response));
    }

    // ------------------------------------------------------------------
    // Discard — OP-OPR-002
    // ------------------------------------------------------------------

    [Fact]
    public async Task Discard_without_the_typed_confirmation_is_refused_and_discards_nothing()
    {
        var queue = MakeQueue(ProfileId, "discard.confirm");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.DiscardedCount = 99;

        var response = await PostAsync("/api/v1/operations/discard", Body(new
                {
                    queueId = queue.Id,
                    messageIds = new[] { "m-1" },
                    dryRun = false,
                    confirmation = "a different queue entirely",
                }), "discard-wrong");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-001", await CodeAsync(response));
        Assert.Empty(_fixture.Admin.Calls);
    }

    [Fact]
    public async Task Discard_dry_run_previews_without_touching_the_broker()
    {
        var queue = MakeQueue(ProfileId, "discard.dry");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PeekResult = new PeekResult(
            [
                new PeekedMessage("m-1", "k", null, null, 10, null, new Dictionary<string, string>(), false),
                new PeekedMessage("m-2", "k", null, null, 10, null, new Dictionary<string, string>(), false),
            ],
            Truncated: false);

        var response = await PostAsync("/api/v1/operations/discard", Body(new
                {
                    queueId = queue.Id,
                    messageIds = new[] { "m-1", "m-missing" },
                    dryRun = true,
                }), "discard-dry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(1L, (long?)root["affected"]);
        Assert.True((bool)root["dryRun"]!);

        var warnings = root["warnings"]!.AsArray().Select(node => (string)node!).ToList();
        Assert.Contains(warnings, warning => warning.Contains("m-missing") || warning.Contains("1 of"));

        // The preview read; the discard never ran.
        Assert.Contains(_fixture.Admin.Calls, call => call.StartsWith("peek:", StringComparison.Ordinal));
        Assert.DoesNotContain(_fixture.Admin.Calls, call => call.StartsWith("discard:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Discard_confirmed_removes_the_named_messages()
    {
        var queue = MakeQueue(ProfileId, "discard.apply");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.DiscardedCount = 2;

        var response = await PostAsync("/api/v1/operations/discard", Body(new
                {
                    queueId = queue.Id,
                    messageIds = new[] { "m-1", "m-2" },
                    dryRun = false,
                    confirmation = queue.Name,
                }), "discard-apply");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(2L, (long?)root["affected"]);
        Assert.Contains(_fixture.Admin.Calls, call => call.StartsWith($"discard:{queue.Name}:2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Discard_over_the_batch_limit_is_rejected()
    {
        var queue = MakeQueue(ProfileId, "discard.limit");
        _fixture.QueueCatalog.Queues = [queue];

        var ids = Enumerable.Range(0, 501).Select(_ => $"m-{_}").ToArray();

        var response = await PostAsync("/api/v1/operations/discard", Body(new { queueId = queue.Id, messageIds = ids, dryRun = true }), "discard-limit");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-003", await CodeAsync(response));
    }

    // ------------------------------------------------------------------
    // Purge — OP-OPR-003
    // ------------------------------------------------------------------

    [Fact]
    public async Task Purge_dry_run_reports_the_depth_without_emptying_anything()
    {
        var brokerId = await CreateProfileAsync("dev", "purge-dry");
        var queue = MakeQueue(brokerId, "purge.dry", depth: 7);
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PeekResult = new PeekResult([], false);

        var response = await PostAsync("/api/v1/operations/purge", Body(new { queueId = queue.Id, dryRun = true }), "purge-dry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(7L, (long?)root["affected"]);

        Assert.Contains(_fixture.Admin.Calls, call => call.StartsWith("peek:", StringComparison.Ordinal));
        Assert.DoesNotContain(_fixture.Admin.Calls, call => call.StartsWith("purge:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Purge_without_confirmation_touches_nothing()
    {
        var brokerId = await CreateProfileAsync("dev", "purge-confirm");
        var queue = MakeQueue(brokerId, "purge.confirm");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PurgedCount = 99;

        var response = await PostAsync("/api/v1/operations/purge", Body(new { queueId = queue.Id, dryRun = false }), "purge-none");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-001", await CodeAsync(response));
        Assert.Empty(_fixture.Admin.Calls);
    }

    [Fact]
    public async Task Purge_confirmed_empties_the_queue()
    {
        var brokerId = await CreateProfileAsync("dev", "purge-apply");
        var queue = MakeQueue(brokerId, "purge.apply");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PurgedCount = 7;

        var response = await PostAsync("/api/v1/operations/purge", Body(new { queueId = queue.Id, dryRun = false, confirmation = queue.Name }), "purge-apply");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(7L, (long?)root["affected"]);
        Assert.Contains(_fixture.Admin.Calls, call => call.StartsWith($"purge:{queue.Name}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Purge_on_production_requires_the_environment_tag()
    {
        // A profile whose environment is prod: creation validates through the stub probe,
        // which passes any endpoint that does not name a failure.
        var created = await PostAsync("/api/v1/brokers", Body(new
                {
                    name = $"purge-prod-{Guid.NewGuid():N}",
                    providerKind = "rabbitmq",
                    environment = "prod",
                    credentialKind = "basic",
                    endpoint = "amqp://prod.invalid/h",
                }), "purge-prod-create");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var profile = JsonNode.Parse(await created.Content.ReadAsStringAsync())!.AsObject();
        var brokerId = Guid.Parse((string)profile["id"]!);

        var queue = MakeQueue(brokerId, "purge.prod");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PurgedCount = 1;

        var response = await PostAsync("/api/v1/operations/purge", Body(new { queueId = queue.Id, dryRun = false, confirmation = queue.Name }), "purge-prod");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-001", await CodeAsync(response));
        Assert.Empty(_fixture.Admin.Calls);

        var confirmed = await PostAsync("/api/v1/operations/purge", Body(new
                {
                    queueId = queue.Id,
                    dryRun = false,
                    confirmation = queue.Name,
                    environmentConfirmation = "prod",
                }), "purge-prod-ok");

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    // ------------------------------------------------------------------
    // Replay — OP-OPR-004
    // ------------------------------------------------------------------

    [Fact]
    public async Task Replay_dry_run_counts_what_would_move_and_warns_without_death_records()
    {
        var brokerId = await CreateProfileAsync("dev", "replay-dry");
        var queue = MakeQueue(brokerId, "replay.dry");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.PeekResult = new PeekResult(
            [
                new PeekedMessage("m-1", "k", "orders", null, 10, null,
                    new Dictionary<string, string> { ["x-death"] = "present" }, false),
                new PeekedMessage("m-2", "k", null, null, 10, null, new Dictionary<string, string>(), false),
            ],
            Truncated: false);

        var response = await PostAsync("/api/v1/operations/replay", Body(new { queueId = queue.Id, dryRun = true }), "replay-dry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(2L, (long?)root["wouldReplay"]);
        Assert.True((bool)root["dryRun"]!);

        var warnings = root["warnings"]!.AsArray().Select(node => (string)node!).ToList();
        Assert.Contains(warnings, warning => warning.Contains("1 message"));
        Assert.DoesNotContain(_fixture.Admin.Calls, call => call.StartsWith("redrive:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Replay_confirmed_returns_a_terminal_job_when_everything_moved()
    {
        var brokerId = await CreateProfileAsync("dev", "replay-apply");
        var queue = MakeQueue(brokerId, "replay.apply");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.RedriveResult = new RedriveResult(Published: 5, Acknowledged: 5);

        var response = await PostAsync("/api/v1/operations/replay", Body(new { queueId = queue.Id, dryRun = false, maxMessages = 5 }), "replay-apply");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var job = root["job"]!.AsObject();
        Assert.Equal("completed", (string?)job["state"]);
        Assert.Equal(5L, (long?)job["total"]);
        Assert.Equal(5L, (long?)job["processed"]);
        Assert.Equal(0L, (long?)job["failed"]);
    }

    [Fact]
    public async Task Replay_that_moved_an_uneven_count_is_Unresolved_never_a_plain_failure()
    {
        var brokerId = await CreateProfileAsync("dev", "replay-partial");
        var queue = MakeQueue(brokerId, "replay.partial");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.RedriveResult = new RedriveResult(Published: 5, Acknowledged: 3);

        var response = await PostAsync("/api/v1/operations/replay", Body(new { queueId = queue.Id, dryRun = false }), "replay-partial");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("ERR-OPR-004", (string?)root["code"]);
        Assert.Equal("Unresolved", (string?)root["outcome"]);
        Assert.False((bool?)root["retryable"]);
    }

    [Fact]
    public async Task Replay_on_production_requires_the_environment_tag()
    {
        var created = await PostAsync("/api/v1/brokers", Body(new
                {
                    name = $"replay-prod-{Guid.NewGuid():N}",
                    providerKind = "rabbitmq",
                    environment = "prod",
                    credentialKind = "basic",
                    endpoint = "amqp://prod.invalid/h",
                }), "replay-prod-create");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var profile = JsonNode.Parse(await created.Content.ReadAsStringAsync())!.AsObject();
        var brokerId = Guid.Parse((string)profile["id"]!);

        var queue = MakeQueue(brokerId, "replay.prod");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();

        var response = await PostAsync("/api/v1/operations/replay", Body(new { queueId = queue.Id, dryRun = false }), "replay-prod");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-OPR-001", await CodeAsync(response));
        Assert.Empty(_fixture.Admin.Calls);
    }

    [Fact]
    public async Task Replay_on_a_queue_the_provider_cannot_read_is_refused_not_failed()
    {
        var brokerId = await CreateProfileAsync("dev", "replay-cap");
        var queue = MakeQueue(brokerId, "replay.quorum");
        _fixture.QueueCatalog.Queues = [queue];
        _fixture.Admin.Calls.Clear();
        _fixture.Admin.ThrowCapabilityOnPeek = true;

        var response = await PostAsync("/api/v1/operations/replay", Body(new { queueId = queue.Id, dryRun = true }), "replay-cap");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ERR-CAP-001", await CodeAsync(response));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        return (string?)root["code"];
    }
}
