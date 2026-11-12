using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marites.Application.Brokers;
using Marites.Application.Preflight;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Marites.Api.Brokers;

/// <summary>OP-BRK-001 to OP-BRK-005.</summary>
public static class BrokerEndpoints
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string IfMatchHeader = "If-Match";

    public static IEndpointRouteBuilder MapBrokerEndpoints(this IEndpointRouteBuilder routes)
    {
        var brokers = routes.MapGroup("/brokers").WithTags("Brokers");

        brokers.MapGet("/", ListBrokersAsync).WithName("listBrokers");
        brokers.MapPost("/", CreateBrokerAsync).WithName("createBroker");
        brokers.MapGet("/{brokerId:guid}", GetBrokerAsync).WithName("getBroker");
        brokers.MapPatch("/{brokerId:guid}", UpdateBrokerAsync).WithName("updateBroker");
        brokers.MapDelete("/{brokerId:guid}", DeleteBrokerAsync).WithName("deleteBroker");
        brokers.MapPost("/{brokerId:guid}/preflight", RunPreflightAsync).WithName("runPreflight");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<BrokerView>>> ListBrokersAsync(
        string? environment,
        string? providerKind,
        BrokerProfileService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(environment, providerKind, cancellationToken));

    private static async Task<IResult> GetBrokerAsync(
        Guid brokerId,
        BrokerProfileService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var broker = await service.GetAsync(brokerId, cancellationToken);
        if (broker is null)
        {
            return NotFound(brokerId);
        }

        context.Response.Headers.ETag = Quote(broker.Revision);
        return TypedResults.Ok(broker);
    }

    private static async Task<IResult> CreateBrokerAsync(
        CreateBrokerBody body,
        BrokerProfileService service,
        IIdempotencyStore idempotency,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        var hash = Hash($"{body.Name}|{body.ProviderKind}|{body.Environment}|{body.Endpoint}");

        if (await ReplayAsync(idempotency, key!, "createBroker", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        try
        {
            var created = await service.CreateAsync(ToInput(body), cancellationToken);
            var payload = JsonSerializer.Serialize(created, JsonOptions);

            await idempotency.RecordAsync(key!, "createBroker", hash, 201, payload, cancellationToken);

            context.Response.Headers.ETag = Quote(created.Revision);
            return TypedResults.Created($"/api/v1/brokers/{created.Id}", created);
        }
        catch (DuplicateBrokerNameException)
        {
            return Conflict("ERR-BRK-002", "A broker profile with that name already exists.");
        }
    }

    private static async Task<IResult> UpdateBrokerAsync(
        Guid brokerId,
        UpdateBrokerBody body,
        BrokerProfileService service,
        IIdempotencyStore idempotency,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        // 428 rather than 400: the request is well formed, it simply lacks the precondition
        // that makes an update safe. A 400 would suggest the body was wrong.
        if (!context.Request.Headers.TryGetValue(IfMatchHeader, out var ifMatch) || string.IsNullOrWhiteSpace(ifMatch))
        {
            return PreconditionRequired();
        }

        if (!TryParseRevision(ifMatch!, out var expected))
        {
            return PreconditionRequired();
        }

        var hash = Hash($"{brokerId}|{body.Name}|{body.Environment}|{body.Endpoint}|{expected}");

        if (await ReplayAsync(idempotency, key!, "updateBroker", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        try
        {
            var updated = await service.UpdateAsync(brokerId, ToInput(body), expected, cancellationToken);
            var payload = JsonSerializer.Serialize(updated, JsonOptions);

            await idempotency.RecordAsync(key!, "updateBroker", hash, 200, payload, cancellationToken);

            context.Response.Headers.ETag = Quote(updated.Revision);
            return TypedResults.Ok(updated);
        }
        catch (BrokerNotFoundException)
        {
            return NotFound(brokerId);
        }
        catch (RevisionConflictException conflict)
        {
            return Conflict(
                "ERR-CMN-006",
                $"If-Match {conflict.Expected} is stale; the stored revision is {conflict.Actual}. Re-read and re-apply.");
        }
        catch (DuplicateBrokerNameException)
        {
            return Conflict("ERR-BRK-002", "A broker profile with that name already exists.");
        }
    }

    private static async Task<IResult> DeleteBrokerAsync(
        Guid brokerId,
        BrokerProfileService service,
        IIdempotencyStore idempotency,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        var hash = Hash(brokerId.ToString());

        if (await ReplayAsync(idempotency, key!, "deleteBroker", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        if (!await service.DeleteAsync(brokerId, cancellationToken))
        {
            return NotFound(brokerId);
        }

        await idempotency.RecordAsync(key!, "deleteBroker", hash, 204, string.Empty, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Must match the pipeline's options, including null omission. The recorded body is what a
    /// retry receives, so serialising it differently makes the replay differ from the original
    /// answer — which the idempotency test caught.
    /// </summary>
    private static async Task<IResult> RunPreflightAsync(
        Guid brokerId,
        PreflightService preflight,
        IIdempotencyStore idempotency,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var key) || string.IsNullOrWhiteSpace(key))
        {
            return MissingHeader(IdempotencyHeader);
        }

        var hash = Hash($"preflight|{brokerId}");

        if (await ReplayAsync(idempotency, key!, "runPreflight", hash, cancellationToken) is { } replay)
        {
            return replay;
        }

        var result = await preflight.RunAsync(brokerId, cancellationToken);
        if (result is null)
        {
            return NotFound(brokerId);
        }

        // A failing check is still a successful answer: 200 with state = failing, because a
        // preflight that cannot answer is less useful than one that answers with a remedy.
        await idempotency.RecordAsync(
            key!, "runPreflight", hash, 200, JsonSerializer.Serialize(result, JsonOptions), cancellationToken);

        return TypedResults.Ok(result);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Replays the first answer for this key, or returns null so the command runs. A repeat
    /// with a different request is <c>ERR-CMN-005</c>: one key may not mean two things.
    /// </summary>
    private static async Task<IResult?> ReplayAsync(
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
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency key reused with different parameters",
                extensions: new Dictionary<string, object?> { ["code"] = "ERR-CMN-005" });
        }

        return recorded.ResponseStatus == StatusCodes.Status204NoContent
            ? TypedResults.NoContent()
            : Results.Text(recorded.ResponseJson, "application/json", Encoding.UTF8, recorded.ResponseStatus);
    }

    private static string Quote(int revision) => $"\"{revision}\"";

    private static bool TryParseRevision(string header, out int revision) =>
        int.TryParse(header.Trim('"', 'W', '/'), out revision) && revision > 0;

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IResult MissingHeader(string name) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"The {name} header is required",
            extensions: new Dictionary<string, object?> { ["code"] = "ERR-CMN-003" });

    private static IResult PreconditionRequired() =>
        Results.Problem(
            statusCode: StatusCodes.Status428PreconditionRequired,
            title: "If-Match is required to update a broker profile",
            extensions: new Dictionary<string, object?> { ["code"] = "ERR-CMN-006" });

    private static IResult NotFound(Guid id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"No broker profile {id}",
            extensions: new Dictionary<string, object?> { ["code"] = "ERR-BRK-001" });

    private static IResult Conflict(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private static BrokerInput ToInput(CreateBrokerBody body) => new(
        body.Name, body.ProviderKind, body.Environment, body.CredentialKind,
        body.Endpoint, body.AdminUri, body.NamespaceOrVhost, body.Credentials);

    private static BrokerInput ToInput(UpdateBrokerBody body) => new(
        body.Name ?? string.Empty, string.Empty, body.Environment ?? string.Empty,
        string.Empty, body.Endpoint, body.AdminUri, body.NamespaceOrVhost, body.Credentials);

    public sealed record CreateBrokerBody(
        string Name,
        string ProviderKind,
        string Environment,
        string CredentialKind,
        string? Endpoint,
        string? AdminUri,
        string? NamespaceOrVhost,
        Dictionary<string, string>? Credentials);

    public sealed record UpdateBrokerBody(
        string? Name,
        string? Environment,
        string? Endpoint,
        string? AdminUri,
        string? NamespaceOrVhost,
        Dictionary<string, string>? Credentials);
}
