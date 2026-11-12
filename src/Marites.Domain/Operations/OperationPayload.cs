namespace Marites.Domain.Operations;

/// <summary>
/// One row per message an operation touched — a snapshot, not a column, because discard and
/// purge delete the row a reference would point at (DEC-017, DEC-018).
/// </summary>
public sealed class OperationPayload
{
    private OperationPayload() { }

    public Guid Id { get; private set; }
    public Guid AuditId { get; private set; }
    public string? MessageId { get; private set; }

    /// <summary>HMAC of the body, so evidence remains attributable after it is swept.</summary>
    public string? ContentHash { get; private set; }
    public byte[]? PayloadBefore { get; private set; }

    /// <summary>Set only by a replay whose body was edited.</summary>
    public byte[]? PayloadAfter { get; private set; }
    public long PayloadSize { get; private set; }

    /// <summary><c>max(row retention, the evidence retention)</c> — evidence never dies before the messages it describes.</summary>
    public DateTimeOffset RetainedUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
