namespace Marites.Application.Brokers;

/// <summary>One message read from a queue, as the broker holds it.</summary>
public sealed record PeekedMessage(
    string MessageId,
    string RoutingKey,
    string? Exchange,
    string? ContentType,
    long Size,
    string? PayloadBase64,
    IReadOnlyDictionary<string, string> Headers,
    bool Redelivered);

public sealed record PeekResult(IReadOnlyList<PeekedMessage> Messages, bool Truncated);

public sealed record RedriveResult(int Published, int Acknowledged);

/// <summary>
/// The operations the adapter performs, as distinct from the ones it observes.
/// </summary>
/// <remarks>
/// No hold-before-command here. ADR-023's <c>operation_intent</c> is written by the operation
/// layer before it calls this port, because the port cannot know what a caller intends — and
/// putting the hold here would make every caller's retry semantics this port's problem.
/// </remarks>
public interface IBrokerAdmin
{
    Task<PeekResult> PeekAsync(Guid brokerId, string queue, int maxMessages, CancellationToken cancellationToken);

    Task<int> DiscardAsync(Guid brokerId, string queue, IReadOnlyCollection<string> messageIds, CancellationToken cancellationToken);

    Task<long> PurgeAsync(Guid brokerId, string queue, CancellationToken cancellationToken);

    /// <summary>
    /// Re-sends up to <paramref name="maxMessages"/> from <paramref name="queue"/> and acknowledges
    /// them. Publish goes to <paramref name="targetExchange"/> and
    /// <paramref name="targetRoutingKey"/> when given; otherwise each message goes back to the
    /// exchange and routing key its own death record names, falling back to the queue itself.
    /// </summary>
    Task<RedriveResult> RedriveAsync(
        Guid brokerId,
        string queue,
        string? targetExchange,
        string? targetRoutingKey,
        int maxMessages,
        CancellationToken cancellationToken);
}

/// <summary>
/// The provider cannot perform this operation safely here (ERR-CAP-001).
/// </summary>
/// <remarks>
/// Distinct from a failure: nothing went wrong. The broker is healthy and the request is valid,
/// but the operation would do something the product refuses to do — peek on a quorum queue, for
/// instance, which destroys the message rather than dead-lettering it (architecture overview §2).
/// </remarks>
public sealed class BrokerCapabilityException(string detail) : Exception(detail);

/// <summary>
/// The broker could not be contacted at all (ERR-BRK-002). Adapters translate their client
/// library's own exception types into this one, so the API layer owns a status code rather
/// than a vendor's type.
/// </summary>
public sealed class BrokerConnectionException(string detail, Exception inner) : Exception(detail, inner);
