namespace Marites.Domain.Operations;

/// <summary>
/// The durable hold. Written and committed <em>before</em> the broker command, promoted to
/// audit plus evidence on success, marked abandoned if the command did not take effect
/// (ADR-023). It is a write-ahead log for operations, swept in minutes.
/// </summary>
public sealed class OperationIntent
{
    private OperationIntent() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "human";
    public Guid? OnBehalfOf { get; private set; }
    public Guid? ProposalId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string ParamsJson { get; private set; } = "{}";

    /// <summary>The bodies the command will destroy, held so they can be recorded afterwards.</summary>
    public string BodiesJson { get; private set; } = "[]";

    /// <summary><c>held</c>, <c>promoted</c> or <c>abandoned</c>.</summary>
    public string State { get; private set; } = "held";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
}
