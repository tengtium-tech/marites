using Marites.Domain.Brokers;

namespace Marites.Application.Brokers;

/// <summary>What a caller sends to create or change a broker profile.</summary>
public sealed record BrokerInput(
    string Name,
    string ProviderKind,
    string Environment,
    string CredentialKind,
    string? Endpoint,
    string? AdminUri,
    string? NamespaceOrVhost,
    IReadOnlyDictionary<string, string>? Credentials);

/// <summary>What a caller receives. Credentials are absent by construction, not by filtering.</summary>
public sealed record BrokerView(
    Guid Id,
    string Name,
    string ProviderKind,
    string Environment,
    string? Endpoint,
    string? AdminUri,
    string? NamespaceOrVhost,
    bool IsDefault,
    string CredentialKind,
    DateTimeOffset? CredentialsUpdatedAt,
    string PreflightState,
    int Revision);

/// <summary>The record was not found in the caller's tenant.</summary>
public sealed class BrokerNotFoundException(Guid id)
    : Exception($"No broker profile {id}.")
{
    public Guid BrokerId { get; } = id;
}

/// <summary>The caller's <c>If-Match</c> does not match the stored revision.</summary>
public sealed class RevisionConflictException(int expected, int actual)
    : Exception($"Revision {expected} is stale; the stored revision is {actual}.")
{
    public int Expected { get; } = expected;

    public int Actual { get; } = actual;
}

/// <summary>A profile with that name already exists in the tenant.</summary>
public sealed class DuplicateBrokerNameException(string name)
    : Exception($"A broker profile named '{name}' already exists.")
{
    public string Name { get; } = name;
}

/// <summary>
/// The persistence port. Entity-level on purpose: the service owns mapping, redaction and the
/// revision rule, so a second adapter cannot implement them differently.
/// </summary>
public interface IBrokerProfileRepository
{
    Task<Guid> CurrentTenantIdAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<BrokerProfile>> ListAsync(string? environment, string? providerKind, CancellationToken cancellationToken);

    Task<BrokerProfile?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid exceptId, CancellationToken cancellationToken);

    void Add(BrokerProfile profile);

    void Remove(BrokerProfile profile);

    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>Reads and writes recorded command outcomes.</summary>
public interface IIdempotencyStore
{
    Task<Marites.Domain.Idempotency.IdempotencyKey?> FindAsync(
        string key,
        string operation,
        CancellationToken cancellationToken);

    Task RecordAsync(
        string key,
        string operation,
        string requestHash,
        int responseStatus,
        string responseJson,
        CancellationToken cancellationToken);
}
