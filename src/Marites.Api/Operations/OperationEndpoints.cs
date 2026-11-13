using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Marites.Application.Brokers;
using Marites.Application.Queues;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Marites.Api.Operations;

/// <summary>OP-OPR-001…004 — the four live recovery operations.</summary>
/// <remarks>
/// Everything here acts on the live queue: no capture store exists before M2, so a dry run
/// reads the queue itself. The ADR-023 hold (operation_intent written before the command)
/// arrives with WP-M1-3; until then the guard that exists is validation before execution —
/// a typed confirmation, the environment tag on production — not a durable record of intent.
/// </remarks>
public static class OperationEndpoints
{
    private const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapOperationEndpoints(this IEndpointRouteBuilder routes)
    {
        var operations = routes.MapGroup("/operations").WithTags("Operations");

        operations.MapPost("/peek", PeekAsync).WithName("peekQueue");
        operations.MapPost("/discard", DiscardAsync).WithName("discardMessages");
        operations.MapPost("/purge", PurgeAsync).WithName("purgeQueue");
        operations.MapPost("/replay", ReplayAsync).WithName("replayMessages");

        return routes;
    }

    // ------------------------------------------------------------------
    // Peek — OP-OPR-001
    // ------------------------------------------------------------------

    private static async Task<IResult> PeekAsync(
        HttpContext context,
        PeekBody body,
        IQueueCatalog catalog,
        IBrokerAdmin admin,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        var hash = Hash(JsonSerializer.Serialize(body, JsonOptions));

        if (await RecordedAnswerAsync(idempotency, key!, "peekQueue", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var queue = await ResolveQueueAsync(catalog, body.QueueId, cancellationToken);
        if (queue is null)
        {
            return QueueNotFound(body.QueueId);
        }

        if (body.Limit is { } limit && limit is < 1 or > 100)
        {
            return LimitRejected("limit");
        }

        try
        {
            var result = await admin.PeekAsync(
                queue.BrokerId, queue.Name, body.Limit ?? 10, cancellationToken);

            var view = new PeekView(
                Messages: result.Messages
                    .Select(message => ToMessageView(message, body.IncludePayload ?? true))
                    .ToList(),
                // RabbitMQ peek is get-and-requeue: it holds and redelivers. That is measured
                // destructive, and RabbitMQ is the only adapter this build registers.
                Destructive: true);

            var json = JsonSerializer.Serialize(view, JsonOptions);
            await idempotency.RecordAsync(
                key!, "peekQueue", hash, 200, json, cancellationToken);

            return Results.Json(view, JsonOptions, statusCode: StatusCodes.Status200OK);
        }
        catch (BrokerCapabilityException capability)
        {
            return CannotPerform(capability.Message);
        }
        catch (BrokerConnectionException)
        {
            return Unreachable();
        }
    }

    // ------------------------------------------------------------------
    // Discard — OP-OPR-002
    // ------------------------------------------------------------------

    private static async Task<IResult> DiscardAsync(
        HttpContext context,
        DiscardBody body,
        IQueueCatalog catalog,
        IBrokerAdmin admin,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        if (body.MessageIds.Count == 0)
        {
            return Rejected("ERR-CMN-001", "At least one message id is required.");
        }

        if (body.MessageIds.Count > 500)
        {
            return Rejected("ERR-OPR-003", "A discard is limited to 500 messages per request.");
        }

        var hash = Hash(JsonSerializer.Serialize(body, JsonOptions));

        if (await RecordedAnswerAsync(idempotency, key!, "discardMessages", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var queue = await ResolveQueueAsync(catalog, body.QueueId, cancellationToken);
        if (queue is null)
        {
            return QueueNotFound(body.QueueId);
        }

        if (!body.DryRun && body.Confirmation != queue.Name)
        {
            return ConfirmationMismatch(queue.Name);
        }

        try
        {
            if (body.DryRun)
            {
                // The preview reads the same queue the command would act on: the matched
                // identifiers are what would be removed, and anything requested but absent is
                // said so rather than silently presumed.
                var preview = await admin.PeekAsync(
                    queue.BrokerId, queue.Name, Math.Max(body.MessageIds.Count, 10), cancellationToken);

                var found = preview.Messages
                    .Where(message => body.MessageIds.Contains(message.MessageId, StringComparer.Ordinal))
                    .ToList();

                var warnings = new List<string>();
                if (found.Count < body.MessageIds.Count)
                {
                    warnings.Add(
                        $"{body.MessageIds.Count - found.Count} of the requested messages were not found in the queue; "
                        + "they may have been consumed already.");
                }

                return await ResultAsync(
                    idempotency, key!, "discardMessages", hash,
                    new OperationView(
                        OperationId: Guid.NewGuid(),
                        DryRun: true,
                        Affected: found.Count,
                        Sample: found.Take(10).Select(m => ToMessageView(m, includePayload: true)).ToList(),
                        Warnings: warnings),
                    StatusCodes.Status200OK, cancellationToken);
            }

            var discarded = await admin.DiscardAsync(
                queue.BrokerId, queue.Name, body.MessageIds, cancellationToken);

            return await ResultAsync(
                idempotency, key!, "discardMessages", hash,
                new OperationView(
                    OperationId: Guid.NewGuid(),
                    DryRun: false,
                    Affected: discarded,
                    Sample: [],
                    Warnings: []),
                StatusCodes.Status200OK, cancellationToken);
        }
        catch (BrokerCapabilityException capability)
        {
            return CannotPerform(capability.Message);
        }
        catch (BrokerConnectionException)
        {
            return Unreachable();
        }
    }

    // ------------------------------------------------------------------
    // Purge — OP-OPR-003
    // ------------------------------------------------------------------

    private static async Task<IResult> PurgeAsync(
        HttpContext context,
        PurgeBody body,
        IQueueCatalog catalog,
        IBrokerProfileRepository profiles,
        IBrokerAdmin admin,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        var hash = Hash(JsonSerializer.Serialize(body, JsonOptions));

        if (await RecordedAnswerAsync(idempotency, key!, "purgeQueue", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var queue = await ResolveQueueAsync(catalog, body.QueueId, cancellationToken);
        if (queue is null)
        {
            return QueueNotFound(body.QueueId);
        }

        var profile = await profiles.FindAsync(queue.BrokerId, cancellationToken);
        if (profile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: $"No broker profile {queue.BrokerId}",
                extensions: new Dictionary<string, object?> { ["code"] = "ERR-BRK-001" });
        }

        // Purge empties a whole queue: the queue name alone is not enough on production, where
        // the operator must also name the environment they believe they are standing in.
        if (!body.DryRun && body.Confirmation != queue.Name)
        {
            return ConfirmationMismatch(queue.Name);
        }

        if (!body.DryRun && profile.Environment == "prod" && body.EnvironmentConfirmation != profile.Environment)
        {
            return ConfirmationMismatch(profile.Environment, environment: true);
        }

        try
        {
            if (body.DryRun)
            {
                var preview = await admin.PeekAsync(queue.BrokerId, queue.Name, 10, cancellationToken);

                var warnings = new List<string>();
                if (queue.Depth is null)
                {
                    warnings.Add("The broker did not report a depth for this queue.");
                }

                return await ResultAsync(
                    idempotency, key!, "purgeQueue", hash,
                    new OperationView(
                        OperationId: Guid.NewGuid(),
                        DryRun: true,
                        Affected: queue.Depth ?? preview.Messages.Count,
                        Sample: preview.Messages.Select(m => ToMessageView(m, includePayload: true)).ToList(),
                        Warnings: warnings),
                    StatusCodes.Status200OK, cancellationToken);
            }

            var purged = await admin.PurgeAsync(queue.BrokerId, queue.Name, cancellationToken);

            return await ResultAsync(
                idempotency, key!, "purgeQueue", hash,
                new OperationView(
                    OperationId: Guid.NewGuid(),
                    DryRun: false,
                    Affected: purged,
                    Sample: [],
                    Warnings: []),
                StatusCodes.Status200OK, cancellationToken);
        }
        catch (BrokerCapabilityException capability)
        {
            return CannotPerform(capability.Message);
        }
        catch (BrokerConnectionException)
        {
            return Unreachable();
        }
    }

    // ------------------------------------------------------------------
    // Replay — OP-OPR-004
    // ------------------------------------------------------------------

    private static async Task<IResult> ReplayAsync(
        HttpContext context,
        ReplayBody body,
        IQueueCatalog catalog,
        IBrokerProfileRepository profiles,
        IBrokerAdmin admin,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        if (body.MaxMessages is { } max && max is < 1 or > 1000)
        {
            return Rejected("ERR-OPR-003", "A replay is limited to 1000 messages per request.");
        }

        var hash = Hash(JsonSerializer.Serialize(body, JsonOptions));

        if (await RecordedAnswerAsync(idempotency, key!, "replayMessages", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var queue = await ResolveQueueAsync(catalog, body.QueueId, cancellationToken);
        if (queue is null)
        {
            return QueueNotFound(body.QueueId);
        }

        var profile = await profiles.FindAsync(queue.BrokerId, cancellationToken);
        if (profile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: $"No broker profile {queue.BrokerId}",
                extensions: new Dictionary<string, object?> { ["code"] = "ERR-BRK-001" });
        }

        // Replaying on production re-sends live business messages: the queue name alone is not
        // enough there, same rule as purge.
        if (!body.DryRun && profile.Environment == "prod" && body.EnvironmentConfirmation != profile.Environment)
        {
            return ConfirmationMismatch(profile.Environment, environment: true);
        }

        try
        {
            var maxMessages = body.MaxMessages ?? 100;

            if (body.DryRun)
            {
                var preview = await admin.PeekAsync(queue.BrokerId, queue.Name, maxMessages, cancellationToken);

                var warnings = new List<string>();
                if (preview.Truncated)
                {
                    warnings.Add("More messages remain in the queue than were previewed.");
                }

                var withoutDeathRecord = preview.Messages
                    .Count(message => !DeathRecorded(message));
                if (withoutDeathRecord > 0)
                {
                    warnings.Add(
                        $"{withoutDeathRecord} message(s) carry no death record and will be re-sent to the "
                        + "queue itself rather than an original exchange.");
                }

                return await ResultAsync(
                    idempotency, key!, "replayMessages", hash,
                    new ReplayView(
                        DryRun: true,
                        WouldReplay: preview.Messages.Count,
                        Warnings: warnings,
                        Job: null),
                    StatusCodes.Status200OK, cancellationToken);
            }

            // The redrive runs to completion inside this request in this slice. A count that
            // came out uneven means some messages were published and not acknowledged — or the
            // reverse — and that is Unresolved (ERR-OPR-004): never a plain failure, because a
            // retry would repeat the successful portion.
            var result = await admin.RedriveAsync(
                queue.BrokerId,
                queue.Name,
                body.Target?.Exchange,
                body.Target?.RoutingKey,
                maxMessages,
                cancellationToken);

            var resolved = result.Published == result.Acknowledged;

            var view = new ReplayView(
                DryRun: false,
                WouldReplay: result.Published,
                Warnings: [],
                Job: new ReplayJobView(
                    Id: Guid.NewGuid(),
                    State: resolved ? "completed" : "failed",
                    Total: result.Published,
                    Processed: result.Acknowledged,
                    Failed: result.Published - result.Acknowledged,
                    HaltReason: null,
                    CreatedBy: "api",
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow));

            if (!resolved)
            {
                var detail = $"The replay published {result.Published} and acknowledged {result.Acknowledged}. "
                    + "Reconcile the queue before doing anything else.";

                return Results.Problem(
                    statusCode: StatusCodes.Status202Accepted,
                    title: "The replay failed partway and may have re-sent some messages",
                    detail: detail,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "ERR-OPR-004",
                        ["outcome"] = "Unresolved",
                        ["retryable"] = false,
                    });
            }

            return await ResultAsync(
                idempotency, key!, "replayMessages", hash, view,
                StatusCodes.Status202Accepted, cancellationToken);
        }
        catch (BrokerCapabilityException capability)
        {
            return CannotPerform(capability.Message);
        }
        catch (BrokerConnectionException)
        {
            return Unreachable();
        }
    }

    // ------------------------------------------------------------------
    // Shared plumbing
    // ------------------------------------------------------------------

    private static bool DeathRecorded(PeekedMessage message) =>
        message.Headers.ContainsKey("x-death");

    private static MessageView ToMessageView(PeekedMessage message, bool includePayload) => new(
        MessageId: message.MessageId,
        RoutingKey: message.RoutingKey,
        Exchange: message.Exchange,
        ContentType: message.ContentType,
        Size: message.Size,
        Payload: includePayload ? message.PayloadBase64 : null,
        Headers: message.Headers.Count > 0 ? message.Headers : null,
        Redelivered: message.Redelivered);

    private static async Task<QueueSummaryView?> ResolveQueueAsync(
        IQueueCatalog catalog,
        Guid queueId,
        CancellationToken cancellationToken)
    {
        if (queueId == Guid.Empty)
        {
            return null;
        }

        return await catalog.FindAsync(queueId, cancellationToken);
    }

    private static async Task<IResult> ResultAsync(
        IIdempotencyStore idempotency,
        string key,
        string operation,
        string hash,
        object view,
        int statusCode,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(view, JsonOptions);
        await idempotency.RecordAsync(key, operation, hash, statusCode, json, cancellationToken);

        return Results.Json(view, JsonOptions, statusCode: statusCode);
    }

    private static IResult ConfirmationMismatch(string expected, bool environment = false) =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: environment
                ? "The typed environment does not match the target environment"
                : "The typed queue name does not match the target queue",
            detail: environment
                ? $"This operation targets production. Type '{expected}' exactly to confirm it."
                : $"Type '{expected}' exactly to confirm this operation.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "ERR-OPR-001",
                ["outcome"] = "Rejected",
                ["retryable"] = false,
                ["remedy"] = "Retype the name exactly.",
            });

    private static IResult CannotPerform(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The connected provider cannot perform this operation",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "ERR-CAP-001",
                ["outcome"] = "Rejected",
                ["retryable"] = false,
                ["remedy"] = "Read the provider capabilities and omit the control for this provider.",
            });

    private static IResult Rejected(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The request exceeds an operation limit",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["outcome"] = "Rejected",
                ["retryable"] = false,
            });

    private static IResult LimitRejected(string field) =>
        Rejected("ERR-OPR-003", $"The {field} is out of range.");

    private static IResult QueueNotFound(Guid queueId) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"No queue {queueId}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "ERR-CMN-002",
                ["outcome"] = "Rejected",
                ["retryable"] = false,
            });

    private static IResult Unreachable() =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The broker could not be reached",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "ERR-BRK-002",
                ["outcome"] = "Unavailable",
                ["retryable"] = true,
            });

    private static IResult MissingHeader(string name) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"The {name} header is required",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "ERR-CMN-001",
                ["outcome"] = "Rejected",
                ["retryable"] = false,
            });

    private static async Task<IResult?> RecordedAnswerAsync(
        IIdempotencyStore store,
        string key,
        string operation,
        string hash,
        CancellationToken cancellationToken)
    {
        var recorded = await store.FindAsync(key, operation, cancellationToken);
        if (recorded is null)
        {
            return null;
        }

        if (recorded.RequestHash != hash)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key reused with different parameters",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "ERR-CMN-005",
                    ["outcome"] = "Rejected",
                    ["retryable"] = false,
                });
        }

        // Contract conventions §3.1: a repeat returns the recorded outcome with
        // `outcome: AlreadyApplied`, mapped to 200 regardless of the original status.
        var body = JsonNode.Parse(recorded.ResponseJson)?.AsObject() ?? new JsonObject();
        body["outcome"] = "AlreadyApplied";

        return Results.Json(body, JsonOptions, statusCode: StatusCodes.Status200OK);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

// ------------------------------------------------------------------------
// Bodies — shapes the served contract declares
// ------------------------------------------------------------------------

public sealed record PeekBody(
    Guid QueueId,
    int? Limit,
    bool? IncludePayload);

public sealed record DiscardBody(
    Guid QueueId,
    IReadOnlyList<string> MessageIds,
    bool DryRun,
    string? Confirmation);

public sealed record PurgeBody(
    Guid QueueId,
    bool DryRun,
    string? Confirmation,
    string? EnvironmentConfirmation);

public sealed record ReplayBody(
    Guid QueueId,
    int? MaxMessages,
    bool DryRun,
    int? MaxPerMinute,
    ReplayTargetBody? Target,
    string? EnvironmentConfirmation);

public sealed record ReplayTargetBody(
    string? Exchange,
    string? RoutingKey);

// ------------------------------------------------------------------------
// Views — the contract's PeekedMessage, OperationResult, ReplayResult
// ------------------------------------------------------------------------

public sealed record MessageView(
    string MessageId,
    string RoutingKey,
    string? Exchange,
    string? ContentType,
    long Size,
    string? Payload,
    IReadOnlyDictionary<string, string>? Headers,
    bool Redelivered);

public sealed record PeekView(
    IReadOnlyList<MessageView> Messages,
    bool Destructive);

public sealed record OperationView(
    Guid OperationId,
    bool DryRun,
    long Affected,
    IReadOnlyList<MessageView> Sample,
    IReadOnlyList<string> Warnings);

public sealed record ReplayView(
    bool DryRun,
    long WouldReplay,
    IReadOnlyList<string> Warnings,
    ReplayJobView? Job);

public sealed record ReplayJobView(
    Guid Id,
    string State,
    long Total,
    long Processed,
    long Failed,
    string? HaltReason,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
