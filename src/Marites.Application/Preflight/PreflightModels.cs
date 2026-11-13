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

/// <summary>
/// Why live metadata access could not be established. The two cases need different answers:
/// an unreachable broker is 503 and retryable, rejected credentials are 422 and the operator
/// must change something.
/// </summary>
public enum LiveAccessFailure
{
    None,
    Unreachable,
    Rejected,
}

/// <summary>The checks, plus whether the broker could be reached at all.</summary>
public sealed record ProbeReport(IReadOnlyList<PreflightCheck> Checks, LiveAccessFailure LiveAccess);

/// <summary>Runs the checks against one provider. Implemented per adapter.</summary>
public interface IBrokerProbe
{
    Task<ProbeReport> RunAsync(ProbeTarget target, CancellationToken cancellationToken);
}
