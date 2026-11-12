using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marites.Infrastructure.RabbitMq;
using RabbitMQ.Client;

namespace Marites.InfrastructureTests;

/// <summary>
/// The supervisor survives a broker-side connection drop, visibly.
/// </summary>
/// <remarks>
/// The drop is forced through the management API rather than simulated in code: a simulated drop
/// proves the code handles the shape it expects, which is the part least likely to be wrong.
/// </remarks>
public sealed class RabbitMqReconnectTests
{
    private const string Endpoint = "amqp://localhost:5672/";
    private const string Credentials = """{"amqp":"marites:marites"}""";

    [Fact]
    public async Task A_dropped_connection_is_reported_and_the_tap_resumes()
    {
        var factory = RabbitMqConnectionFactory.Create(Endpoint, Credentials);

        if (!await BrokerReachableAsync())
        {
            return;
        }

        var queue = $"marites.reconnect.{Guid.NewGuid():N}";
        var received = new List<string>();
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drops = new List<int>();

        await using (var setup = await factory.CreateConnectionAsync())
        await using (var channel = await setup.CreateChannelAsync())
        {
            // Not auto-delete: with no consumer during the drop the queue would be removed, and
            // the reconnect would then be consuming a queue that no longer exists — which looks
            // exactly like a reconnect that failed.
            await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: false);
        }

        await using var supervisor = new RabbitMqTapSupervisor(
            factory,
            queue,
            (body, _, _) =>
            {
                lock (received)
                {
                    received.Add(Encoding.UTF8.GetString(body.Span));
                    if (received.Count == 1) firstSeen.TrySetResult();
                    if (received.Count == 2) secondSeen.TrySetResult();
                }

                return Task.CompletedTask;
            },
            (dropCount, _) =>
            {
                lock (drops) drops.Add(dropCount);
            });

        supervisor.Start();

        await PublishAsync(factory, queue, "first");
        Assert.True(await Completes(firstSeen, TimeSpan.FromSeconds(10)), "The first message was not tapped.");

        // Force the drop from the broker side. The management database registers a connection
        // some seconds after it is established, and a fixed delay races that — it passed once and
        // failed the next run with drops=0. Polling for the connection to appear is the only
        // version that can tell "nothing to close" apart from "closed too early".
        Assert.True(
            await WaitForConnectionsAsync(TimeSpan.FromSeconds(20)),
            "The supervisor's connection never appeared in the management API, so no drop could be forced.");

        await CloseAllConnectionsAsync();

        var reconnected = await WaitForAsync(() => supervisor.DropCount > 0 && supervisor.IsConnected, TimeSpan.FromSeconds(20));
        Assert.True(reconnected, $"The supervisor did not report a drop and reconnect. drops={supervisor.DropCount} connected={supervisor.IsConnected} observed={string.Join(",", drops)}");

        await PublishAsync(factory, queue, "second");
        Assert.True(await Completes(secondSeen, TimeSpan.FromSeconds(20)), "The tap did not resume after reconnecting.");

        lock (drops)
        {
            Assert.NotEmpty(drops);
        }
    }

    private static async Task PublishAsync(ConnectionFactory factory, string queue, string body)
    {
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.BasicPublishAsync(string.Empty, queue, Encoding.UTF8.GetBytes(body));
    }

    private static async Task<bool> WaitForConnectionsAsync(TimeSpan timeout)
    {
        using var client = ManagementClient();
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var listing = await client.GetStringAsync("api/connections");

            if (JsonDocument.Parse(listing).RootElement.GetArrayLength() > 0)
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }

    private static HttpClient ManagementClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:15672/") };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("marites:marites")));

        return client;
    }

    private static async Task CloseAllConnectionsAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:15672/") };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("marites:marites")));

        var listing = await client.GetStringAsync("api/connections");

        foreach (var connection in JsonDocument.Parse(listing).RootElement.EnumerateArray())
        {
            var name = connection.GetProperty("name").GetString();
            if (name is not null)
            {
                await client.DeleteAsync($"api/connections/{Uri.EscapeDataString(name)}");
            }
        }
    }

    private static async Task<bool> Completes(TaskCompletionSource source, TimeSpan timeout) =>
        await Task.WhenAny(source.Task, Task.Delay(timeout)) == source.Task;

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(200);
        }

        return condition();
    }

    private static async Task<bool> BrokerReachableAsync()
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("localhost", 5672);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
