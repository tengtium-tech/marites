namespace Marites.Application.Queues;

/// <summary>One entity as the broker reports it, with the identity DEC-022 derives from it.</summary>
public sealed record QueueSummaryView(
    Guid Id,
    Guid BrokerId,
    string Name,
    string? Vhost,
    string SourceKind,
    bool IsDeadLetter,
    long? Depth,
    int? Consumers,
    long? Unacked,
    double? PublishRate,
    double? DeliverRate,
    double? AckRate,
    string CaptureState,
    bool DeadLetterConfigured);

/// <summary>Reads live queue metadata from a broker. Nothing here is stored.</summary>
public interface IQueueCatalog
{
    Task<IReadOnlyList<QueueSummaryView>> ListAsync(
        Guid brokerId,
        string? search,
        bool? isDeadLetter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one entity by its derived id, without requiring the caller to list first.
    /// </summary>
    /// <remarks>
    /// The id is not invertible, so this reads what the broker reports and recomputes until one
    /// matches. That costs a read per profile per call — acceptable while one profile is typical,
    /// and the reason this is a documented cost rather than an accident.
    /// </remarks>
    Task<QueueSummaryView?> FindAsync(Guid queueId, CancellationToken cancellationToken);
}
