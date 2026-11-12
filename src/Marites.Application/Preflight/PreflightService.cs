using System.Text.Json;
using Marites.Application.Brokers;
using Marites.Application.Security;

namespace Marites.Application.Preflight;

/// <summary>
/// OP-BRK-006. Runs the probe and records the outcome on the profile, so the queue list can
/// show why capture is unavailable without re-running anything.
/// </summary>
public sealed class PreflightService
{
    private readonly IBrokerProfileRepository _repository;
    private readonly ICredentialProtector _protector;
    private readonly IBrokerProbe _probe;
    private readonly TimeProvider _clock;

    public PreflightService(
        IBrokerProfileRepository repository,
        ICredentialProtector protector,
        IBrokerProbe probe,
        TimeProvider clock)
    {
        _repository = repository;
        _protector = protector;
        _probe = probe;
        _clock = clock;
    }

    public async Task<PreflightResult?> RunAsync(Guid brokerId, CancellationToken cancellationToken)
    {
        var profile = await _repository.FindAsync(brokerId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var target = new ProbeTarget(
            Endpoint: System.Text.Encoding.UTF8.GetString(_protector.Unprotect(profile.EndpointEnc)),
            AdminUri: profile.AdminUri,
            CredentialsJson: profile.CredentialsEnc is null
                ? "{}"
                : System.Text.Encoding.UTF8.GetString(_protector.Unprotect(profile.CredentialsEnc)),
            Vhost: profile.NamespaceOrVhost);

        var checks = await _probe.RunAsync(target, cancellationToken);

        var state = checks.Any(c => c.State == "fail") ? "failing" : "passing";
        var now = _clock.GetUtcNow();

        profile.RecordPreflight(state, now);
        await _repository.SaveAsync(cancellationToken);

        return new PreflightResult(state, checks, now);
    }
}
