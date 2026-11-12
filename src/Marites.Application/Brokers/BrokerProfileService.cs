using System.Text;
using System.Text.Json;
using Marites.Application.Security;
using Marites.Domain.Brokers;

namespace Marites.Application.Brokers;

/// <summary>
/// The broker profile use cases. Everything a caller may see is shaped here — mapping,
/// redaction, and the revision rule — so an adapter cannot implement them differently.
/// </summary>
public sealed class BrokerProfileService
{
    private readonly IBrokerProfileRepository _repository;
    private readonly ICredentialProtector _protector;
    private readonly TimeProvider _clock;

    public BrokerProfileService(
        IBrokerProfileRepository repository,
        ICredentialProtector protector,
        TimeProvider clock)
    {
        _repository = repository;
        _protector = protector;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BrokerView>> ListAsync(
        string? environment,
        string? providerKind,
        CancellationToken cancellationToken)
    {
        var profiles = await _repository.ListAsync(environment, providerKind, cancellationToken);
        return profiles.Select(Map).ToList();
    }

    public async Task<BrokerView?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _repository.FindAsync(id, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<BrokerView> CreateAsync(BrokerInput input, CancellationToken cancellationToken)
    {
        var tenantId = await _repository.CurrentTenantIdAsync(cancellationToken);

        if (await _repository.NameExistsAsync(input.Name, Guid.Empty, cancellationToken))
        {
            throw new DuplicateBrokerNameException(input.Name);
        }

        var profile = BrokerProfile.Create(
            tenantId,
            input.Name,
            input.ProviderKind,
            input.Environment,
            input.CredentialKind,
            ProtectEndpoint(input.Endpoint),
            input.AdminUri,
            input.NamespaceOrVhost,
            ProtectCredentials(input.Credentials),
            _clock.GetUtcNow());

        _repository.Add(profile);
        await _repository.SaveAsync(cancellationToken);

        return Map(profile);
    }

    public async Task<BrokerView> UpdateAsync(
        Guid id,
        BrokerInput patch,
        int expectedRevision,
        CancellationToken cancellationToken)
    {
        var profile = await _repository.FindAsync(id, cancellationToken)
            ?? throw new BrokerNotFoundException(id);

        // Checked before anything is applied, so a stale caller changes nothing at all.
        if (profile.Revision != expectedRevision)
        {
            throw new RevisionConflictException(expectedRevision, profile.Revision);
        }

        if (!string.IsNullOrWhiteSpace(patch.Name)
            && await _repository.NameExistsAsync(patch.Name, id, cancellationToken))
        {
            throw new DuplicateBrokerNameException(patch.Name);
        }

        profile.Update(
            patch.Name,
            patch.Environment,
            patch.AdminUri,
            patch.NamespaceOrVhost,
            patch.Endpoint is null ? null : ProtectEndpoint(patch.Endpoint),
            ProtectCredentials(patch.Credentials),
            _clock.GetUtcNow());

        await _repository.SaveAsync(cancellationToken);

        return Map(profile);
    }

    /// <summary>
    /// Removes the profile row. It touches no broker: no adapter exists, so nothing was ever
    /// created there to clean up — and claiming otherwise would be the sort of quiet lie the
    /// product exists to avoid.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _repository.FindAsync(id, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        _repository.Remove(profile);
        await _repository.SaveAsync(cancellationToken);
        return true;
    }

    private BrokerView Map(BrokerProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.ProviderKind,
        profile.Environment,
        Redact(UnprotectEndpoint(profile.EndpointEnc)),
        profile.AdminUri,
        profile.NamespaceOrVhost,
        profile.IsDefault,
        profile.CredentialKind,
        profile.CredentialsUpdatedAt,
        profile.PreflightState,
        profile.Revision);

    private byte[] ProtectEndpoint(string? endpoint) =>
        _protector.Protect(Encoding.UTF8.GetBytes(endpoint ?? string.Empty));

    private string UnprotectEndpoint(byte[] stored) =>
        Encoding.UTF8.GetString(_protector.Unprotect(stored));

    private byte[]? ProtectCredentials(IReadOnlyDictionary<string, string>? credentials) =>
        credentials is null or { Count: 0 }
            ? null
            : _protector.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials)));

    /// <summary>
    /// Strips the userinfo from an endpoint so a response can show where a broker is without
    /// showing how to reach it. Redaction happens on the way out only, so a round-trip through
    /// the API never destroys the stored secret.
    /// </summary>
    internal static string Redact(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            return endpoint;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return endpoint;
        }

        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        return builder.Uri.ToString();
    }
}
