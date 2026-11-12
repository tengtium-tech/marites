using System.Text;
using Marites.Infrastructure.RabbitMq;
using RabbitMQ.Client;

namespace Marites.InfrastructureTests;

/// <summary>
/// The tap: a message published to a queue is observed by a consumer, and acknowledged.
/// </summary>
/// <remarks>
/// Runs only when a broker is reachable. This is the first test that consumes rather than
/// connects — the difference between "the credential is accepted" and "Marites can see traffic".
/// </remarks>
public sealed class RabbitMqTapTests
{
    private const string Endpoint = "amqp://localhost:5672/";
    private const string Credentials = """{"amqp":"marites:marites"}""";

    [Fact]
    public async Task A_published_message_is_tapped()
    {
        var factory = RabbitMqConnectionFactory.Create(Endpoint, Credentials);

        if (!await ReachableAsync(factory))
        {
            return;
        }

        var queue = $"marites.test.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var setup = await factory.CreateConnectionAsync();
        await using (var channel = await setup.CreateChannelAsync())
        {
            await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: true);
        }

        await using var tap = await RabbitMqTap.StartAsync(
            factory,
            queue,
            (body, _, _) =>
            {
                received.TrySetResult(Encoding.UTF8.GetString(body.Span));
                return Task.CompletedTask;
            });

        await using (var publisher = await setup.CreateChannelAsync())
        {
            await publisher.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queue,
                body: Encoding.UTF8.GetBytes("""{"orderId":42}"""));
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(completed == received.Task, "The tap did not observe the message within ten seconds.");
        Assert.Contains("orderId", await received.Task, StringComparison.Ordinal);
        Assert.True(tap.IsOpen);
    }

    private static async Task<bool> ReachableAsync(ConnectionFactory factory)
    {
        try
        {
            await using var connection = await factory.CreateConnectionAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
