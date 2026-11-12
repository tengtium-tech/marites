namespace Marites.Domain.Operations;

/// <summary>
/// Progress of a replay. <c>Unresolved</c> is a state, not an error: a replay that failed
/// partway has already published some messages, and retrying it would send those twice
/// (recovery lifecycle §3).
/// </summary>
public sealed class ReplayJob
{
    private ReplayJob() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid QueueRefId { get; private set; }

    /// <summary><c>pending</c>, <c>running</c>, <c>halted</c>, <c>completed</c> or <c>failed</c> (VOCABULARY §6).</summary>
    public string State { get; private set; } = "pending";
    public int Total { get; private set; }
    public int Processed { get; private set; }
    public int Failed { get; private set; }
    public string? HaltReason { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
