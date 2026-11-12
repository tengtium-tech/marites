namespace Marites.Application.Providers;

/// <summary>
/// What this implementation can do with a provider, as the contract's <c>ProviderCapabilities</c>
/// defines it.
/// </summary>
/// <remarks>
/// Two different questions live in this record and it matters which is which:
/// <c>supportTier</c> describes what the <em>provider</em> offers (VOCABULARY §12 — RabbitMQ is
/// <c>full</c>), while the <c>can*</c> flags describe what <em>this code</em> does today. They
/// diverge on purpose: RabbitMQ can purge, Marites cannot yet, so <c>canPurge</c> is false while
/// the tier is full. Publishing true for something the code cannot do is the exact failure
/// <c>/capabilities</c> exists to prevent.
/// </remarks>
public sealed record ProviderCapabilities(
    string Kind,
    string DisplayName,
    string SupportTier,
    bool CanTap,
    bool CanPeek,
    bool CanPublish,
    bool CanDiscard,
    bool CanPurge,
    bool CanReplay,
    bool CanDeclareShadow,
    string AddressingModel,
    string DeadLetterModel,
    string CountingModel,
    string ReplayModel,
    string DiscardModel,
    bool SupportsHeaders = true,
    long? MaxPayloadBytes = null);

/// <summary>Which providers this build can talk to, and what it can do with each.</summary>
public interface IProviderRegistry
{
    IReadOnlyList<ProviderCapabilities> All { get; }

    ProviderCapabilities? Find(string kind);
}
