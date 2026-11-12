using System.Text.Json;
using RabbitMQ.Client;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// Opens AMQP connections from a stored broker profile.
/// </summary>
/// <remarks>
/// This is the first code that authenticates as a consumer. Preflight proves the management API
/// accepts the credential; nothing before this proved the AMQP endpoint does, and the two can
/// disagree — a user may hold management access without vhost access to consume.
/// </remarks>
public static class RabbitMqConnectionFactory
{
    /// <summary>
    /// Builds a connection factory from a decrypted endpoint and credential JSON.
    /// </summary>
    /// <remarks>
    /// The endpoint arrives as an AMQP URI (<c>amqp://host:5672/vhost</c>), already decrypted by
    /// the caller. The credential slot is <c>user:password</c>, the shape VOCABULARY §13 defines
    /// for <c>basic</c>.
    /// </remarks>
    public static ConnectionFactory Create(string endpoint, string credentialsJson)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"The endpoint '{endpoint}' is not a URI.", nameof(endpoint));
        }

        var (user, password) = ReadCredentials(credentialsJson);

        return new ConnectionFactory
        {
            HostName = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5672,
            UserName = user,
            Password = password,
            VirtualHost = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "/"
                : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),

            // A tap must not silently reconnect into an unknown state; the adapter decides when
            // to reconnect, and it re-declares its consumers when it does.
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
        };
    }

    /// <summary>
    /// Reads the <c>amqp</c> slot, falling back to a single-slot profile. The <c>admin</c> slot
    /// is not used here: consuming uses the AMQP credential, which may differ from the one that
    /// reads the management API.
    /// </summary>
    private static (string User, string Password) ReadCredentials(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var slot = root.TryGetProperty("amqp", out var amqp) ? amqp.GetString()
            : root.TryGetProperty("admin", out var admin) ? admin.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(slot))
        {
            throw new ArgumentException("The profile holds no AMQP credential.", nameof(json));
        }

        var separator = slot.IndexOf(':', StringComparison.Ordinal);

        return separator < 0
            ? (slot, string.Empty)
            : (slot[..separator], slot[(separator + 1)..]);
    }
}
