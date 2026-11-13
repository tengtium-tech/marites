namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// Decides where a redriven message is published, from the message's own death record.
/// </summary>
/// <remarks>
/// RabbitMQ writes an <c>x-death</c> header: a list, most recent death first, each entry naming
/// the <c>exchange</c> the message was published to and the <c>routing-keys</c> it carried when
/// it died. That record is the only place "the original exchange and routing key" exists — there
/// is no broker API to ask. A message with no usable record falls back to the queue it was read
/// from, via the default exchange, because a forward action to a known place beats a drop.
/// </remarks>
public static class ReplayTarget
{
    public static (string Exchange, string RoutingKey) Resolve(
        string? targetExchange,
        string? targetRoutingKey,
        IDictionary<string, object?>? headers,
        string sourceQueue)
    {
        // An explicit routing key is a complete address: the exchange is the default one when
        // the caller omits it, which is exactly how a message is sent straight to a queue.
        // The empty-string exchange is therefore a deliberate choice, not a missing one.
        if (targetRoutingKey is not null)
        {
            return (targetExchange ?? string.Empty, targetRoutingKey);
        }

        var (exchange, routingKey) = FromDeathRecord(headers);

        if (exchange is not null && routingKey is not null)
        {
            return (exchange, routingKey);
        }

        return (string.Empty, sourceQueue);
    }

    /// <summary>The exchange and first routing key of the most recent death, or nulls.</summary>
    public static (string? Exchange, string? RoutingKey) FromDeathRecord(
        IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-death", out var death) || death is not IEnumerable<object> entries)
        {
            return (null, null);
        }

        foreach (var entry in entries)
        {
            if (entry is not IReadOnlyDictionary<string, object?> record)
            {
                continue;
            }

            var exchange = record.TryGetValue("exchange", out var e) ? e as string : null;
            string? routingKey = null;

            if (record.TryGetValue("routing-keys", out var keys) && keys is IEnumerable<object> list)
            {
                routingKey = list.OfType<string>().FirstOrDefault();
            }

            if (exchange is not null)
            {
                return (exchange, routingKey);
            }
        }

        return (null, null);
    }
}
