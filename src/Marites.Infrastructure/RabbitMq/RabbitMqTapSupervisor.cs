using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// Keeps a tap running across connection loss.
/// </summary>
/// <remarks>
/// The client's own automatic recovery is deliberately off: it reconnects silently, and a
/// consumer that resumes without saying so has no idea what it missed while it was away. This
/// supervisor reconnects <em>visibly</em> — it counts the drops, reports each one, and re-declares
/// the consumer, so a gap in observation is a fact the caller can see rather than infer.
/// </remarks>
public sealed class RabbitMqTapSupervisor : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly Func<ReadOnlyMemory<byte>, IReadOnlyDictionary<string, object?>, CancellationToken, Task> _onMessage;
    private readonly Action<int, Exception?> _onStateChanged;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    public RabbitMqTapSupervisor(
        ConnectionFactory factory,
        string queue,
        Func<ReadOnlyMemory<byte>, IReadOnlyDictionary<string, object?>, CancellationToken, Task> onMessage,
        Action<int, Exception?>? onStateChanged = null)
    {
        _factory = factory;
        Queue = queue;
        _onMessage = onMessage;
        _onStateChanged = onStateChanged ?? ((_, _) => { });
    }

    public string Queue { get; }

    /// <summary>How many times the connection has been lost since start. Zero means never.</summary>
    public int DropCount { get; private set; }

    public bool IsConnected { get; private set; }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_stopping.Token));

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoff = TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _factory.CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await channel.BasicQosAsync(0, prefetchCount: 1, global: false, cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        IReadOnlyDictionary<string, object?> headers = delivery.BasicProperties.Headers is null
                            ? new Dictionary<string, object?>()
                            : new Dictionary<string, object?>(delivery.BasicProperties.Headers);

                        await _onMessage(delivery.Body, headers, cancellationToken);
                    }
                    finally
                    {
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
                    }
                };

                await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, cancellationToken);

                IsConnected = true;
                backoff = TimeSpan.FromSeconds(1);
                _onStateChanged(DropCount, null);

                // Wait for the channel to close — that is the drop signal. Anything else here
                // would be guessing at a reason the client already knows.
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    closed.TrySetResult();
                    return Task.CompletedTask;
                };

                await closed.Task.WaitAsync(cancellationToken);

                // A broker-side close completes this task without throwing, so the catch below
                // never runs. Counting the drop here is what makes the reconnect visible — the
                // first version of this code reconnected silently on exactly this path.
                IsConnected = false;
                DropCount++;
                _onStateChanged(DropCount, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                IsConnected = false;
                DropCount++;
                _onStateChanged(DropCount, error);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Backoff, so a broker that is down is not hammered by a reconnect storm from every
            // tap on every instance at the same instant.
            try
            {
                await Task.Delay(backoff, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }

        IsConnected = false;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        _stopping.Dispose();
    }
}
