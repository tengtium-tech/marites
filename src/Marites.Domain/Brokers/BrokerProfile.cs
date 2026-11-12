namespace Marites.Domain.Brokers;

/// <summary>
/// A connection to a broker the adopter runs. Marites is a client of that broker and never
/// provisions, hosts, upgrades or repairs it (ADR-001).
/// </summary>
/// <remarks>
/// The closed vocabularies — <c>provider_kind</c>, <c>credential_kind</c>, <c>preflight_state</c> —
/// are held as text here and owned by VOCABULARY §12, §13 and §7. They become typed value
/// objects when the provider registry lands with the first adapter (M1-1); inventing the enum
/// now would put a second definition of the vocabulary in the code before anything reads it.
/// </remarks>
public sealed class BrokerProfile
{
    private BrokerProfile()
    {
        // Materialisation only.
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary><c>rabbitmq</c>, <c>azure_service_bus</c> or <c>google_pubsub</c> (VOCABULARY §12).</summary>
    public string ProviderKind { get; private set; } = string.Empty;

    /// <summary>Free-form but conventionally <c>prod</c>, <c>staging</c>, <c>dev</c> or <c>test</c> (VOCABULARY §8).</summary>
    public string Environment { get; private set; } = string.Empty;

    /// <summary>Encrypted endpoint. Never returned by any endpoint (NFR-003).</summary>
    public byte[] EndpointEnc { get; private set; } = [];

    /// <summary>Management API address, where the provider has a separate one. Not a secret.</summary>
    public string? AdminUri { get; private set; }

    /// <summary>Selects the shape of <see cref="CredentialsEnc"/> (VOCABULARY §13).</summary>
    public string CredentialKind { get; private set; } = string.Empty;

    /// <summary>
    /// Provider-shaped JSON, encrypted with a per-row data key under the KEK (ADR-018).
    /// Null for ambient kinds, which store no secret at all.
    /// </summary>
    public byte[]? CredentialsEnc { get; private set; }

    public DateTimeOffset? CredentialsUpdatedAt { get; private set; }

    public string? NamespaceOrVhost { get; private set; }

    /// <summary>JSON. TLS options as the adapter needs them.</summary>
    public string TlsOpts { get; private set; } = "{}";

    /// <summary>JSON. Cached provider capabilities, so the UI gates without a live round-trip.</summary>
    public string CapabilitiesJson { get; private set; } = "{}";

    public bool IsDefault { get; private set; }

    /// <summary><c>unknown</c>, <c>passing</c>, <c>failing</c> or <c>degraded</c> (VOCABULARY §7).</summary>
    public string PreflightState { get; private set; } = "unknown";

    public DateTimeOffset? PreflightAt { get; private set; }

    /// <summary>Incremented on every update; a stale <c>If-Match</c> is refused with <c>ERR-CMN-006</c>.</summary>
    public int Revision { get; private set; } = 1;

    public static BrokerProfile Create(
        Guid tenantId,
        string name,
        string providerKind,
        string environment,
        string credentialKind,
        byte[] endpointEnc,
        string? adminUri,
        string? namespaceOrVhost,
        byte[]? credentialsEnc,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A broker profile needs a name.", nameof(name));
        }

        return new BrokerProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            ProviderKind = providerKind,
            Environment = environment,
            CredentialKind = credentialKind,
            EndpointEnc = endpointEnc,
            AdminUri = adminUri,
            NamespaceOrVhost = namespaceOrVhost,
            CredentialsEnc = credentialsEnc,
            CredentialsUpdatedAt = credentialsEnc is null ? null : now.ToUniversalTime(),
            TlsOpts = "{}",
            CapabilitiesJson = "{}",
            IsDefault = false,
            PreflightState = "unknown",
            Revision = 1,
        };
    }

    /// <summary>
    /// Applies a change and increments <see cref="Revision"/>. The increment is what makes
    /// <c>If-Match</c> meaningful: a client holding the previous value is now stale and must
    /// re-read rather than overwrite a change it never saw.
    /// </summary>
    public void Update(
        string? name,
        string? environment,
        string? adminUri,
        string? namespaceOrVhost,
        byte[]? endpointEnc,
        byte[]? credentialsEnc,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            Environment = environment;
        }

        if (adminUri is not null)
        {
            AdminUri = adminUri;
        }

        if (namespaceOrVhost is not null)
        {
            NamespaceOrVhost = namespaceOrVhost;
        }

        if (endpointEnc is not null)
        {
            EndpointEnc = endpointEnc;
        }

        if (credentialsEnc is not null)
        {
            // Supplying credentials rotates the stored secret (UpdateBrokerRequest).
            CredentialsEnc = credentialsEnc;
            CredentialsUpdatedAt = now.ToUniversalTime();
        }

        Revision++;
    }
}
