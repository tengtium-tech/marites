namespace Marites.Domain.Capture;

/// <summary>
/// A watched source. A queue is never identified by name alone — <c>orders</c> exists in prod
/// and staging at once — so the identity includes the profile and the vhost (VOCABULARY §10).
/// </summary>
public sealed class QueueRef
{
    private QueueRef() { }

    public Guid Id { get; private set; }
    public Guid BrokerProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary><c>queue</c>, <c>exchange</c>, <c>topic</c> or <c>subscription</c>.</summary>
    public string SourceKind { get; private set; } = "queue";
    public string? Vhost { get; private set; }
    public bool IsDlq { get; private set; }
    public string? DeadLetterSource { get; private set; }
    public DateTimeOffset? BoundAt { get; private set; }
    public bool CaptureEnabled { get; private set; }
}
