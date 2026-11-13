using System.Text;
using Marites.Application.Brokers;
using Marites.Infrastructure.Bookkeeping;
using Marites.Infrastructure.RabbitMq;
using Marites.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace Marites.InfrastructureTests;

/// <summary>
/// The operations, against a real broker: peek on a classic queue, the quorum refusal, discard,
/// purge and redrive.
/// </summary>
public sealed class RabbitMqAdminTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Peek_reads_a_classic_message_without_consuming_it()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        var (admin, brokerId, queue) = await ArrangeAsync(context, "classic", quorum: false);

        await PublishAsync(queue, "peek-me", "m-1");

        var peeked = await admin.PeekAsync(brokerId, queue, maxMessages: 5, CancellationToken.None);

        Assert.Single(peeked.Messages);
        Assert.Equal("m-1", peeked.Messages[0].MessageId);
        Assert.False(peeked.Messages[0].Redelivered);

        // Still there: a peek that consumes is not a peek.
        Assert.Equal(1u, await DepthAsync(queue));
    }

    [Fact]
    public async Task Peek_refuses_a_quorum_queue_rather_than_destroying_a_message()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        var (admin, brokerId, queue) = await ArrangeAsync(context, "quorum", quorum: true);

        await PublishAsync(queue, "doomed", "q-1");

        var error = await Assert.ThrowsAsync<BrokerCapabilityException>(
            () => admin.PeekAsync(brokerId, queue, maxMessages: 5, CancellationToken.None));

        Assert.Contains("quorum queue", error.Message);

        // The refusal is the point: the message must still be there afterwards.
        Assert.Equal(1u, await DepthAsync(queue));
    }

    [Fact]
    public async Task Discard_removes_only_the_named_message()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        var (admin, brokerId, queue) = await ArrangeAsync(context, "discard", quorum: false);

        await PublishAsync(queue, "keep", "d-1");
        await PublishAsync(queue, "drop", "d-2");

        var discarded = await admin.DiscardAsync(brokerId, queue, ["d-2"], CancellationToken.None);

        Assert.Equal(1, discarded);
        Assert.Equal(1u, await DepthAsync(queue));
    }

    [Fact]
    public async Task Purge_empties_the_queue_and_reports_the_count()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        var (admin, brokerId, queue) = await ArrangeAsync(context, "purge", quorum: false);

        await PublishAsync(queue, "a", "p-1");
        await PublishAsync(queue, "b", "p-2");
        await PublishAsync(queue, "c", "p-3");

        var purged = await admin.PurgeAsync(brokerId, queue, CancellationToken.None);

        Assert.Equal(3, purged);
        Assert.Equal(0u, await DepthAsync(queue));
    }

    [Fact]
    public async Task Redrive_moves_messages_to_the_target_queue()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        using var store = new TemporaryStore();
        await using var context = store.CreateContext();
        var (admin, brokerId, queue) = await ArrangeAsync(context, "redrive", quorum: false);
        var target = $"{queue}.target";

        await DeclareAsync(target, quorum: false);
        await PublishAsync(queue, "move-me", "r-1");

        var result = await admin.RedriveAsync(brokerId, queue, target, maxMessages: 10, CancellationToken.None);

        Assert.Equal(1, result.Published);
        Assert.Equal(1, result.Acknowledged);
        Assert.Equal(0u, await DepthAsync(queue));
        Assert.Equal(1u, await DepthAsync(target));
    }

    private static async Task<(RabbitMqAdmin Admin, Guid BrokerId, string Queue)> ArrangeAsync(
        BookkeepingDbContext context,
        string label,
        bool quorum)
    {
        var queue = $"marites.admin.{label}.{Guid.NewGuid():N}";

        await DeclareAsync(queue, quorum);

        // The context is the test's, not the arrangement's: the admin keeps a repository over it,
        // so disposing it here would leave the admin holding a dead context.
        await context.Database.MigrateAsync();

        var tenant = Marites.Domain.Tenancy.Tenant.Create("Default", "default", Now);
        context.Tenants.Add(tenant);

        var protector = new KekCredentialProtector(
            KekOptions.Parse(Convert.ToBase64String(new byte[32]), null));

        var profile = Marites.Domain.Brokers.BrokerProfile.Create(
            tenant.Id,
            label,
            "rabbitmq",
            "dev",
            "basic",
            protector.Protect(Encoding.UTF8.GetBytes("amqp://127.0.0.1:5672/")),
            "http://127.0.0.1:15672",
            "/",
            protector.Protect(Encoding.UTF8.GetBytes("""{"amqp":"marites:marites","admin":"marites:marites"}""")),
            Now);

        context.BrokerProfiles.Add(profile);
        await context.SaveChangesAsync();

        var admin = new RabbitMqAdmin(new BrokerProfileRepository(context), protector);

        return (admin, profile.Id, queue);
    }

    /// <summary>A missing broker is an environment fact, not a defect — CI has none.</summary>
    private static async Task<bool> BrokerReachableAsync()
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", 5672);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task DeclareAsync(string queue, bool quorum)
    {
        var factory = RabbitMqConnectionFactory.Create(
            "amqp://127.0.0.1:5672/", """{"amqp":"marites:marites"}""");

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue,
            durable: quorum,
            exclusive: false,
            // Quorum queues reject autoDelete; classic ones are declared the same way for parity.
            autoDelete: false,
            arguments: quorum
                ? new Dictionary<string, object?> { ["x-queue-type"] = "quorum", ["x-delivery-limit"] = 3 }
                : null);
    }

    private static async Task PublishAsync(string queue, string body, string messageId)
    {
        var factory = RabbitMqConnectionFactory.Create(
            "amqp://127.0.0.1:5672/", """{"amqp":"marites:marites"}""");

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.BasicPublishAsync(
            string.Empty,
            queue,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = messageId },
            body: Encoding.UTF8.GetBytes(body));
    }

    private static async Task<uint> DepthAsync(string queue)
    {
        var factory = RabbitMqConnectionFactory.Create(
            "amqp://127.0.0.1:5672/", """{"amqp":"marites:marites"}""");

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var declared = await channel.QueueDeclarePassiveAsync(queue);
        return declared.MessageCount;
    }
}
