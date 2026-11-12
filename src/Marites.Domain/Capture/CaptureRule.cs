namespace Marites.Domain.Capture;

/// <summary>
/// What is kept for one queue. <c>capture_mode</c> decides whether a row exists at all;
/// <c>payload_policy</c> decides whether the body is kept inside it. Counting is affected by
/// neither (DEC-013, VOCABULARY §5, §16).
/// </summary>
public sealed class CaptureRule
{
    private CaptureRule() { }

    public Guid Id { get; private set; }
    public Guid QueueRefId { get; private set; }
    public string? BindPattern { get; private set; }
    public bool Enabled { get; private set; }

    /// <summary><c>rows</c> or <c>counts</c>.</summary>
    public string CaptureMode { get; private set; } = "rows";

    /// <summary><c>full</c>, <c>sampled</c> or <c>none</c>.</summary>
    public string PayloadPolicy { get; private set; } = "sampled";
    public int SampleRate { get; private set; } = 100;
    public int RowRetentionHours { get; private set; } = 72;
    public long? MaxRows { get; private set; }
    public long? MaxBytes { get; private set; }
    public int Revision { get; private set; } = 1;
}
