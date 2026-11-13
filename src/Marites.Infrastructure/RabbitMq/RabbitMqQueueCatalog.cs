using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marites.Application.Brokers;
using Marites.Application.Queues;
using Marites.Application.Security;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// Live queue metadata, read from the management API on every call.
/// </summary>
/// <remarks>
/// Nothing is cached and nothing is stored: the point of this endpoint is what the broker says
/// now, and a stored copy would be exactly the stale figure the dashboard must never show.
/// </remarks>
public sealed class RabbitMqQueueCatalog : IQueueCatalog
{
    private readonly IBrokerProfileRepository _profiles;
    private readonly ICredentialProtector _protector;

    public RabbitMqQueueCatalog(IBrokerProfileRepository profiles, ICredentialProtector protector)
    {
        _profiles = profiles;
        _protector = protector;
    }

    public async Task<IReadOnlyList<QueueSummaryView>> ListAsync(
        Guid brokerId,
        string? search,
        bool? isDeadLetter,
        CancellationToken cancellationToken)
    {
        var profile = await _profiles.FindAsync(brokerId, cancellationToken)
            ?? throw new BrokerNotFoundException(brokerId);

        var entities = await ReadAsync(profile, cancellationToken);

        var filtered = entities.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (isDeadLetter is { } deadLetter)
        {
            filtered = filtered.Where(e => e.IsDeadLetter == deadLetter);
        }

        return filtered.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }

    public async Task<QueueSummaryView?> FindAsync(Guid queueId, CancellationToken cancellationToken)
    {
        // The id is not invertible, so every profile the broker reports is consulted. With one
        // profile that is one read; the cost is stated in the port rather than hidden here.
        foreach (var profile in await _profiles.ListAsync(null, null, cancellationToken))
        {
            var match = (await ReadAsync(profile, cancellationToken))
                .FirstOrDefault(entity => entity.Id == queueId);

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<QueueSummaryView>> ReadAsync(
        Domain.Brokers.BrokerProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.AdminUri))
        {
            return [];
        }

        var (user, password) = ReadCredentials(
            profile.CredentialsEnc is null
                ? "{}"
                : Encoding.UTF8.GetString(_protector.Unprotect(profile.CredentialsEnc)));

        using var client = new HttpClient { BaseAddress = new Uri(profile.AdminUri.TrimEnd('/') + "/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        client.Timeout = TimeSpan.FromSeconds(10);

        // The whole list, filtered here: /api/queues/%2F loses its escaping in a .NET Uri.
        using var response = await client.GetAsync("api/queues", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var vhost = string.IsNullOrWhiteSpace(profile.NamespaceOrVhost) ? "/" : profile.NamespaceOrVhost;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return document.RootElement.EnumerateArray()
            .Where(queue => queue.TryGetProperty("vhost", out var v) && v.GetString() == vhost)
            .Select(queue => Map(profile.Id, vhost, queue))
            .ToList();
    }

    private static QueueSummaryView Map(Guid brokerId, string vhost, JsonElement queue)
    {
        var name = queue.GetProperty("name").GetString() ?? string.Empty;

        var hasDlx = queue.TryGetProperty("arguments", out var arguments)
                     && arguments.ValueKind == JsonValueKind.Object
                     && arguments.TryGetProperty("x-dead-letter-exchange", out _);

        return new QueueSummaryView(
            Id: QueueIdentity.Compute(brokerId, vhost, name, "queue"),
            BrokerId: brokerId,
            Name: name,
            Vhost: vhost,
            SourceKind: "queue",
            // Not derivable from the broker without a convention this build does not impose, so
            // it stays false rather than guessing from a name suffix.
            IsDeadLetter: false,
            Depth: queue.TryGetProperty("messages", out var depth) ? depth.GetInt64() : null,
            Consumers: queue.TryGetProperty("consumers", out var consumers) ? consumers.GetInt32() : null,
            Unacked: queue.TryGetProperty("messages_unacknowledged", out var unacked) ? unacked.GetInt64() : null,
            PublishRate: Rate(queue, "publish_details"),
            DeliverRate: Rate(queue, "deliver_get_details"),
            AckRate: Rate(queue, "ack_details"),
            // Nothing is captured yet: capture arrives in M2, and "disabled" is the honest value.
            CaptureState: "disabled",
            DeadLetterConfigured: hasDlx);
    }

    private static double? Rate(JsonElement queue, string details)
    {
        if (!queue.TryGetProperty("message_stats", out var stats)
            || !stats.TryGetProperty(details, out var node)
            || !node.TryGetProperty("rate", out var rate))
        {
            return null;
        }

        return rate.GetDouble();
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
}
