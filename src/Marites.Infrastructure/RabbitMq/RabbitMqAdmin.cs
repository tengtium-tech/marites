using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marites.Application.Brokers;
using Marites.Application.Security;
using RabbitMQ.Client;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// The operations, over AMQP.
/// </summary>
/// <remarks>
/// Deliberately not the management API: every queue-scoped path needs the vhost in the URL, and
/// .NET unescapes <c>%2F</c> in a Uri path, so <c>api/queues/%2F/orders/get</c> becomes
/// <c>api/queues//orders/get</c> and 404s. AMQP has no such trap. The one thing still read from
/// the management API is the queue type, which AMQP does not report.
/// </remarks>
public sealed class RabbitMqAdmin : IBrokerAdmin
{
    private readonly IBrokerProfileRepository _profiles;
    private readonly ICredentialProtector _protector;

    public RabbitMqAdmin(IBrokerProfileRepository profiles, ICredentialProtector protector)
    {
        _profiles = profiles;
        _protector = protector;
    }

    public async Task<PeekResult> PeekAsync(
        Guid brokerId,
        string queue,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var profile = await LoadAsync(brokerId, cancellationToken);

        // Measured at M1: five reject_requeue_true gets on a quorum queue with a delivery limit
        // left the queue empty and the dead-letter exchange empty. The message was destroyed, not
        // dead-lettered, so this is refused rather than rate-limited.
        if (await IsQuorumAsync(profile, queue, cancellationToken))
        {
            throw new BrokerCapabilityException(
                $"'{queue}' is a quorum queue. Reading a message from it with get-and-requeue can destroy it — "
                + "the message does not reach the dead-letter exchange. Peek is available on classic queues only; "
                + "for quorum queues, capture (M2) is the non-destructive way to see a body.");
        }

        var factory = Factory(profile);
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var messages = new List<PeekedMessage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < maxMessages; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);

            if (result is null)
            {
                break;
            }

            var message = Map(result);

            // Requeue puts the message back at the head, so the next get returns the same one.
            // Without this the loop would report one message five times and call it five.
            if (!seen.Add(message.MessageId))
            {
                await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                break;
            }

            messages.Add(message);

            // Put it back. On a classic queue this sets `redelivered` and counts toward no limit.
            await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }

        return new PeekResult(messages, Truncated: messages.Count == maxMessages);
    }

    public async Task<int> DiscardAsync(
        Guid brokerId,
        string queue,
        IReadOnlyCollection<string> messageIds,
        CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
        {
            return 0;
        }

        var profile = await LoadAsync(brokerId, cancellationToken);
        var factory = Factory(profile);

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var wanted = messageIds.ToHashSet(StringComparer.Ordinal);
        var discarded = 0;
        var held = new List<ulong>();

        // Held unacked rather than requeued: a requeue returns the message to the head, so
        // requeueing each non-match would return the same message forever and never reach the
        // second. Holding them keeps the queue moving; they go back at the end.
        //
        // The bound matters: every held message is one the broker cannot deliver to anyone else
        // while this runs.
        const int maxHeld = 500;

        try
        {
            while (discarded < wanted.Count && held.Count < maxHeld)
            {
                var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);

                if (result is null)
                {
                    break;
                }

                if (wanted.Contains(result.BasicProperties.MessageId ?? string.Empty))
                {
                    await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken);
                    discarded++;
                }
                else
                {
                    held.Add(result.DeliveryTag);
                }
            }
        }
        finally
        {
            // Whatever happened above, the messages that were not asked for go back.
            foreach (var deliveryTag in held)
            {
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        }

        return discarded;
    }

    public async Task<long> PurgeAsync(Guid brokerId, string queue, CancellationToken cancellationToken)
    {
        var profile = await LoadAsync(brokerId, cancellationToken);
        var factory = Factory(profile);

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        return await channel.QueuePurgeAsync(queue, cancellationToken);
    }

    public async Task<RedriveResult> RedriveAsync(
        Guid brokerId,
        string queue,
        string targetQueue,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var profile = await LoadAsync(brokerId, cancellationToken);
        var factory = Factory(profile);

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var published = 0;
        var acknowledged = 0;

        for (var i = 0; i < maxMessages; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);

            if (result is null)
            {
                break;
            }

            // Publish first, acknowledge second. A crash between the two duplicates rather than
            // loses — the safer direction for a tool whose whole argument is against silent loss.
            await channel.BasicPublishAsync(
                string.Empty,
                targetQueue,
                mandatory: false,
                basicProperties: new BasicProperties(result.BasicProperties),
                body: result.Body,
                cancellationToken: cancellationToken);

            published++;

            await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken);
            acknowledged++;
        }

        return new RedriveResult(published, acknowledged);
    }

    private async Task<Domain.Brokers.BrokerProfile> LoadAsync(Guid brokerId, CancellationToken cancellationToken) =>
        await _profiles.FindAsync(brokerId, cancellationToken) ?? throw new BrokerNotFoundException(brokerId);

    private ConnectionFactory Factory(Domain.Brokers.BrokerProfile profile)
    {
        var endpoint = Encoding.UTF8.GetString(_protector.Unprotect(profile.EndpointEnc));
        var credentials = profile.CredentialsEnc is null
            ? "{}"
            : Encoding.UTF8.GetString(_protector.Unprotect(profile.CredentialsEnc));

        return RabbitMqConnectionFactory.Create(endpoint, credentials);
    }

    /// <summary>The queue type, which AMQP does not report and the management API does.</summary>
    private async Task<bool> IsQuorumAsync(
        Domain.Brokers.BrokerProfile profile,
        string queue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.AdminUri))
        {
            // Without the management API the type is unknown. Refusing is the safe answer: a
            // destructive peek is worse than an unavailable one.
            return true;
        }

        var (user, password) = ReadCredentials(
            profile.CredentialsEnc is null
                ? "{}"
                : Encoding.UTF8.GetString(_protector.Unprotect(profile.CredentialsEnc)));

        using var client = new HttpClient { BaseAddress = new Uri(profile.AdminUri.TrimEnd('/') + "/") };
        client.Timeout = TimeSpan.FromSeconds(10);

        // Without the credentials this answers 401, and a 401 would be read as "unknown type" —
        // which this method treats as quorum and refuses. A missing header would have made peek
        // unavailable on every queue while looking like a capability decision.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

        using var response = await client.GetAsync("api/queues", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return true;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("name", out var name) && name.GetString() == queue)
            {
                return entry.TryGetProperty("type", out var type)
                       && string.Equals(type.GetString(), "quorum", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static (string User, string Password) ReadCredentials(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var slot = root.TryGetProperty("admin", out var admin) ? admin.GetString()
                : root.TryGetProperty("amqp", out var amqp) ? amqp.GetString()
                : null;

            if (slot is null)
            {
                return ("guest", "guest");
            }

            var separator = slot.IndexOf(':', StringComparison.Ordinal);
            return separator < 0 ? (slot, string.Empty) : (slot[..separator], slot[(separator + 1)..]);
        }
        catch (JsonException)
        {
            return ("guest", "guest");
        }
    }

    private static PeekedMessage Map(BasicGetResult result) => new(
        MessageId: result.BasicProperties.MessageId ?? string.Empty,
        RoutingKey: result.RoutingKey,
        ContentType: result.BasicProperties.ContentType,
        Size: result.Body.Length,
        PayloadBase64: Convert.ToBase64String(result.Body.Span),
        Headers: result.BasicProperties.Headers?.ToDictionary(
            header => header.Key,
            header => header.Value?.ToString() ?? string.Empty) ?? [],
        Redelivered: result.Redelivered);
}
