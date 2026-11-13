using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marites.Application.Preflight;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// Preflight checks for RabbitMQ, read from the management API. Nothing here writes to the
/// broker: declaring a queue is a change, and a readiness check that changes things is not a
/// readiness check.
/// </summary>
public sealed class RabbitMqProbe : IBrokerProbe
{
    private const string ShadowPrefix = "marites.";

    public async Task<IReadOnlyList<PreflightCheck>> RunAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        var checks = new List<PreflightCheck>();
        var (user, password) = ReadCredentials(target.CredentialsJson);
        var vhost = string.IsNullOrWhiteSpace(target.Vhost) ? "/" : target.Vhost;

        if (string.IsNullOrWhiteSpace(target.AdminUri))
        {
            checks.Add(new PreflightCheck(
                "management_api_reachable", "Management API reachable", "fail",
                "The profile has no management API address, so permissions cannot be read.",
                "Add the management API address to the broker profile."));

            checks.AddRange(Skipped("Not attempted: the management API was never reached."));
            return checks;
        }

        using var client = new HttpClient { BaseAddress = new Uri(target.AdminUri.TrimEnd('/') + "/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        client.Timeout = TimeSpan.FromSeconds(10);

        var overview = await GetAsync(client, "api/overview", cancellationToken);
        if (overview is null)
        {
            checks.Add(new PreflightCheck(
                "management_api_reachable", "Management API reachable", "fail",
                $"{target.AdminUri} did not answer, or refused the stored credentials.",
                "Check the management API address and that the credential's user may sign in to it."));

            checks.AddRange(Skipped("Not attempted: the management API was never reached."));
            return checks;
        }

        checks.Add(new PreflightCheck("management_api_reachable", "Management API reachable", "pass"));

        var vhosts = await GetAsync(client, "api/vhosts", cancellationToken);
        var vhostVisible = vhosts is not null && vhosts.RootElement.EnumerateArray()
            .Any(v => v.TryGetProperty("name", out var name) && name.GetString() == vhost);

        checks.Add(vhostVisible
            ? new PreflightCheck("vhost_visible", $"Vhost '{vhost}' visible", "pass")
            : new PreflightCheck(
                "vhost_visible", $"Vhost '{vhost}' visible", "fail",
                $"The credential cannot see vhost '{vhost}'.",
                $"Grant the user access to vhost '{vhost}', or correct the vhost on the profile."));

        // The whole permission list, filtered here. `api/permissions/%2F` is the documented path
        // for the default vhost, but .NET unescapes %2F in a Uri path, so the request becomes
        // api/permissions// and 404s — a false failure that reads as a missing grant.
        var permissions = await GetAsync(client, "api/permissions", cancellationToken);
        var mayConfigure = permissions is not null && permissions.RootElement.EnumerateArray()
            .Any(p => p.TryGetProperty("user", out var u) && u.GetString() == user
                      && p.TryGetProperty("vhost", out var v) && v.GetString() == vhost
                      && p.TryGetProperty("configure", out var c)
                      && c.GetString() is { } pattern
                      && (pattern == ".*" || pattern.StartsWith(ShadowPrefix, StringComparison.Ordinal)));

        checks.Add(mayConfigure
            ? new PreflightCheck("topology_write_permitted", "May declare shadow queues", "pass")
            : new PreflightCheck(
                "topology_write_permitted", "May declare shadow queues", "fail",
                $"The user '{user}' holds no configure permission covering '{ShadowPrefix}'.",
                $"Grant configure, write and read on ^{ShadowPrefix.Replace(".", "\\.")} to '{user}' on vhost '{vhost}'."));

        // The broker's own clock, from the Date header of the management API response. It is
        // the only clock the broker exposes without a write, and skew is the failure mode that
        // makes message ages and delivery timestamps quietly wrong.
        var serverDate = await ServerDateAsync(client, "api/overview", cancellationToken);
        checks.Add(EvaluateClockSkew(serverDate, DateTimeOffset.UtcNow, ClockTolerance));

        // Two checks remain, and they are not the same kind of gap.
        checks.Add(new PreflightCheck(
            "dead_letter_configured", "Dead-letter topology readable", "skipped",
            "Not built yet: it is reported per queue, and no queue is watched until capture exists (M2)."));

        // RabbitMQ answers this question with topology_write_permitted: configure on 'marites.*'
        // is exactly the permission a declare needs, so a separate probe would test the same
        // grant twice. The check stays in the contract because it is not a duplicate elsewhere —
        // on Service Bus and Pub/Sub it means "a probe subscription can be created with a TTL or
        // bounded retention", which no other check answers.
        checks.Add(new PreflightCheck(
            "shadow_declarable", "Shadow queue declarable", "skipped",
            "Answered by topology_write_permitted on RabbitMQ: the same grant covers both."));
        return checks;
    }

    private static IEnumerable<PreflightCheck> Skipped(string detail) =>
    [
        new("dead_letter_configured", "Dead-letter topology readable", "skipped", detail),
        new("shadow_declarable", "Shadow queue declarable", "skipped", detail),
    ];

    /// <summary>Skew beyond a minute is enough to misorder evidence; beyond that, ages read wrong.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Judges the broker's clock against ours. Extracted so it can be exercised without a
    /// broker whose clock is actually wrong — which is not something a test can arrange.
    /// </summary>
    public static PreflightCheck EvaluateClockSkew(DateTimeOffset? serverDate, DateTimeOffset now, TimeSpan tolerance)
    {
        if (serverDate is null)
        {
            return new PreflightCheck(
                "clock_skew", "Broker clock within tolerance", "skipped",
                "The management API did not report a Date header, so the broker's clock is unknown.");
        }

        var skew = (serverDate.Value - now).Duration();

        return skew <= tolerance
            ? new PreflightCheck(
                "clock_skew", "Broker clock within tolerance", "pass",
                $"Skew is {skew.TotalSeconds:F0}s, within the {tolerance.TotalSeconds:F0}s tolerance.")
            : new PreflightCheck(
                "clock_skew", "Broker clock within tolerance", "fail",
                $"The broker's clock differs by {skew.TotalSeconds:F0}s, beyond the {tolerance.TotalSeconds:F0}s tolerance.",
                "Point both hosts at the same time source. Message ages and delivery timestamps read wrong while the clocks disagree.");
    }

    private static async Task<DateTimeOffset?> ServerDateAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);

            return response.Headers.Date ?? response.Content.Headers.LastModified;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private static async Task<JsonDocument?> GetAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);

            return response.IsSuccessStatusCode
                ? JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken))
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the credential slots. The management API needs the <c>admin</c> slot; a profile
    /// with a single slot uses it for both, which is the common case for a local broker.
    /// </summary>
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
