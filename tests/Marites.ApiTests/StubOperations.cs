using Marites.Application.Brokers;
using Marites.Application.Queues;

namespace Marites.ApiTests;

/// <summary>
/// A queue catalog that answers from a list the test controls, so the operations endpoints can
/// be exercised without a broker.
/// </summary>
public sealed class StubQueueCatalog : IQueueCatalog
{
    public List<QueueSummaryView> Queues { get; set; } = [];

    public Task<IReadOnlyList<QueueSummaryView>> ListAsync(
        Guid brokerId, string? search, bool? isDeadLetter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QueueSummaryView>>(
            Queues.AsEnumerable().ToList());

    public Task<QueueSummaryView?> FindAsync(Guid queueId, CancellationToken cancellationToken) =>
        Task.FromResult(Queues.FirstOrDefault(queue => queue.Id == queueId));
}

/// <summary>
/// An admin that answers from canned results and records what it was asked to do.
/// </summary>
/// <remarks>
/// The four operations are destructive; the stub lets the tests assert not only what the API
/// answered but whether it actually reached for the broker — a dry run that silently calls
/// discard would otherwise pass a result-shaped test.
/// </remarks>
public sealed class StubBrokerAdmin : IBrokerAdmin
{
    public PeekResult PeekResult { get; set; } = new([], false);
    public int DiscardedCount { get; set; }
    public long PurgedCount { get; set; }
    public RedriveResult RedriveResult { get; set; } = new(0, 0);
    public bool ThrowCapabilityOnPeek { get; set; }
    public List<string> Calls { get; } = [];

    public Task<PeekResult> PeekAsync(
        Guid brokerId, string queue, int maxMessages, CancellationToken cancellationToken)
    {
        Calls.Add($"peek:{queue}:{maxMessages}");

        // One-shot: tests run in a shared singleton, and a flag left set by one test must not
        // turn every later peek into a refusal. Throwing clears it.
        if (ThrowCapabilityOnPeek)
        {
            ThrowCapabilityOnPeek = false;
            throw new BrokerCapabilityException("refused");
        }

        return Task.FromResult(PeekResult);
    }

    public Task<int> DiscardAsync(
        Guid brokerId, string queue, IReadOnlyCollection<string> messageIds, CancellationToken cancellationToken)
    {
        Calls.Add($"discard:{queue}:{messageIds.Count}");
        return Task.FromResult(DiscardedCount);
    }

    public Task<long> PurgeAsync(Guid brokerId, string queue, CancellationToken cancellationToken)
    {
        Calls.Add($"purge:{queue}");
        return Task.FromResult(PurgedCount);
    }

    public Task<RedriveResult> RedriveAsync(
        Guid brokerId, string queue, string? targetExchange, string? targetRoutingKey,
        int maxMessages, CancellationToken cancellationToken)
    {
        Calls.Add($"redrive:{queue}:{targetExchange}:{targetRoutingKey}:{maxMessages}");
        return Task.FromResult(RedriveResult);
    }
}
