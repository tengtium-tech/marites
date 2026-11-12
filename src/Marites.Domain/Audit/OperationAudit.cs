namespace Marites.Domain.Audit;

/// <summary>
/// One record of something that touched the broker or changed configuration.
/// </summary>
/// <remarks>
/// Append-only: nothing in the product updates or deletes a row, and the table carries SQLite
/// triggers that refuse both so a future repository cannot forget the rule (FR-016).
/// Payload evidence is deliberately not here — it lives in <c>operation_payload</c> on its own
/// clock, because a body inside a permanent row would outlive every retention promise (DEC-017).
/// </remarks>
public sealed class OperationAudit
{
    private OperationAudit()
    {
        // Materialisation only.
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Null when the operation had no human actor — a scheduled component has none by design.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary><c>human</c>, <c>agent</c> or <c>system</c>.</summary>
    public string ActorKind { get; private set; } = "human";

    /// <summary>Set when an agent acted as a person: the person, not the credential (ADR-021).</summary>
    public Guid? OnBehalfOf { get; private set; }

    /// <summary>Links a proposal to the operation that followed it.</summary>
    public Guid? ProposalId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Target { get; private set; } = string.Empty;

    public string ParamsJson { get; private set; } = "{}";

    public string Result { get; private set; } = string.Empty;

    public int? MessageCount { get; private set; }

    /// <summary>Survives evidence expiry, so the operation remains attributable after the bodies are gone.</summary>
    public string? PayloadHash { get; private set; }

    /// <summary>True when a replay carried an edited body. On the audit row because it must outlive the evidence.</summary>
    public bool PayloadEdited { get; private set; }

    /// <summary>The assistant a credential belonged to, when one was used (ADR-021).</summary>
    public string? Via { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? Ip { get; private set; }
}
