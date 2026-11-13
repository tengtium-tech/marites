using Marites.Application.Brokers;
using Marites.Application.Queues;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Marites.Api.Queues;

/// <summary>OP-QUE-001 and OP-QUE-002.</summary>
public static class QueueEndpoints
{
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder routes)
    {
        var queues = routes.MapGroup("/queues").WithTags("Queues");

        queues.MapGet("/", ListQueuesAsync).WithName("listQueues");
        queues.MapGet("/{queueId:guid}", GetQueueAsync).WithName("getQueue");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<QueueSummaryView>>> ListQueuesAsync(
        Guid? brokerId,
        bool? isDeadLetter,
        string? search,
        IQueueCatalog catalog,
        IBrokerProfileRepository profiles,
        CancellationToken cancellationToken)
    {
        // `brokerId` is optional in the contract: without it, every configured broker is read.
        if (brokerId is { } id)
        {
            return TypedResults.Ok(
                await catalog.ListAsync(id, search, isDeadLetter, cancellationToken));
        }

        var all = new List<QueueSummaryView>();

        foreach (var profile in await profiles.ListAsync(null, null, cancellationToken))
        {
            all.AddRange(await catalog.ListAsync(profile.Id, search, isDeadLetter, cancellationToken));
        }

        return TypedResults.Ok<IReadOnlyList<QueueSummaryView>>(all);
    }

    private static async Task<IResult> GetQueueAsync(
        Guid queueId,
        IQueueCatalog catalog,
        CancellationToken cancellationToken)
    {
        var queue = await catalog.FindAsync(queueId, cancellationToken);

        // An id that resolves to nothing is the same answer as an entity the broker no longer
        // reports — DEC-022 makes both a 404 rather than a different error per cause.
        return queue is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: $"No queue {queueId}",
                extensions: new Dictionary<string, object?> { ["code"] = "ERR-CMN-002" })
            : TypedResults.Ok(queue);
    }
}
