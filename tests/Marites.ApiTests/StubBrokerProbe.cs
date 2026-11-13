using Marites.Application.Preflight;

namespace Marites.ApiTests;

/// <summary>
/// A probe that answers from the submitted endpoint rather than from a broker.
/// </summary>
/// <remarks>
/// DEC-021 made creation depend on the broker, which would otherwise make every API test need a
/// container — and CI has none. The stub keeps the three outcomes deterministic and testable:
///
///   endpoint contains "unreachable"  → 503, nothing persisted
///   endpoint contains "rejected"     → 422, nothing persisted
///   endpoint contains "no-topology"  → live access fine, topology denied, saved as failing
///   anything else                    → all checks pass
///
/// The real probe is covered separately, against the local stack, where a broker exists.
/// </remarks>
public sealed class StubBrokerProbe : IBrokerProbe
{
    public Task<ProbeReport> RunAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        var endpoint = target.Endpoint;

        if (endpoint.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProbeReport(
                [
                    new PreflightCheck("management_api_reachable", "Management API reachable", "fail",
                        "The management API did not answer.", "Check the address and reachability."),
                    ..Skipped("Not attempted: the management API was never reached."),
                ],
                LiveAccessFailure.Unreachable));
        }

        if (endpoint.Contains("rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProbeReport(
                [
                    new PreflightCheck("management_api_reachable", "Management API reachable", "fail",
                        "The management API rejected the credentials (HTTP 401).",
                        "Correct the credentials, or grant the user access to the management API."),
                    ..Skipped("Not attempted: the management API was never reached."),
                ],
                LiveAccessFailure.Rejected));
        }

        var topology = endpoint.Contains("no-topology", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new ProbeReport(
            [
                new PreflightCheck("management_api_reachable", "Management API reachable", "pass"),
                new PreflightCheck("vhost_visible", "Vhost visible", "pass"),
                topology
                    ? new PreflightCheck("topology_write_permitted", "May declare shadow queues", "fail",
                        "The user holds no configure permission covering 'marites.'.",
                        "Grant configure, write and read on ^marites\\. to the user.")
                    : new PreflightCheck("topology_write_permitted", "May declare shadow queues", "pass"),
                new PreflightCheck("clock_skew", "Broker clock within tolerance", "pass"),
                ..Skipped("Not built yet."),
                topology
                    ? new PreflightCheck("shadow_declarable", "Shadow queue declarable", "fail",
                        "A probe queue could not be declared.",
                        "Grant configure, write and read on ^marites\\.")
                    : new PreflightCheck("shadow_declarable", "Shadow queue declarable", "pass"),
            ],
            LiveAccessFailure.None));
    }

    private static PreflightCheck[] Skipped(string detail) =>
    [
        new("dead_letter_configured", "Dead-letter topology readable", "skipped", detail),
    ];
}
