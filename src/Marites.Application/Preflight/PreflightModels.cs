namespace Marites.Application.Preflight;

/// <summary>One line of a preflight run. The six ids are fixed by the contract.</summary>
public sealed record PreflightCheck(string Id, string Label, string State, string? Detail = null, string? Remedy = null);

/// <summary>
/// The result of a run. <c>state</c> is the worst check: any <c>fail</c> makes the run
/// <c>failing</c>; <c>degraded</c> is reserved for "metadata reachable but writes refused"
/// (VOCABULARY §7), which only a managed broker can produce.
/// </summary>
public sealed record PreflightResult(string State, IReadOnlyList<PreflightCheck> Checks, DateTimeOffset CheckedAt);

/// <summary>What a probe needs to reach a broker. Credentials are plaintext only for the run.</summary>
public sealed record ProbeTarget(string Endpoint, string? AdminUri, string CredentialsJson, string? Vhost);

/// <summary>Runs the checks against one provider. Implemented per adapter.</summary>
public interface IBrokerProbe
{
    Task<IReadOnlyList<PreflightCheck>> RunAsync(ProbeTarget target, CancellationToken cancellationToken);
}
