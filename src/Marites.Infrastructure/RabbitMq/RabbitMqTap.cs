using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// A consumer on one queue. The product's core: without a tap there is nothing to observe.
/// </summary>
/// <remarks>
/// Manual acknowledgement. Auto-ack would tell the broker a message is handled the moment it is
/// delivered, so a crash mid-processing loses it silently — and the whole product is an argument
/// against silent loss.
/// </remarks>
public sealed class RabbitMqTap : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private RabbitMqTap(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public string Queue { get; private init; } = string.Empty;

    public bool IsOpen => _channel.IsOpen;

    /// <summary>
    /// Starts consuming. The callback receives the body and headers; it must not throw for an
    /// ordinary message, because a throw here leaves the message unacknowledged and the broker
    /// redelivers it forever.
    /// </summary>
    public static async Task<RabbitMqTap> StartAsync(
        ConnectionFactory factory,
        string queue,
        Func<ReadOnlyMemory<byte>, IReadOnlyDictionary<string, object?>, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
    {
        var connection = await factory.CreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // One unacknowledged message at a time: a tap is an observer, and prefetching a hundred
        // messages to hold them in memory would make Marites a load problem on the broker it is
        // supposed to be diagnosing.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                // Copied: the client hands back a mutable dictionary owned by the delivery, and
                // the callback should not be able to alter what the broker sent.
                IReadOnlyDictionary<string, object?> headers = delivery.BasicProperties.Headers is null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(delivery.BasicProperties.Headers);
                await onMessage(delivery.Body, headers, cancellationToken);
            }
            finally
            {
                // Acknowledged whether or not the callback succeeded: capture is an observation,
                // and holding a message hostage because our own handler failed would stop the
                // queue for everyone else.
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken);

        return new RabbitMqTap(connection, channel) { Queue = queue };
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
