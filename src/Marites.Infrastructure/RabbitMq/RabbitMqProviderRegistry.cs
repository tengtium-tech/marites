using Marites.Application.Providers;

namespace Marites.Infrastructure.RabbitMq;

/// <summary>
/// The providers this build can talk to. One entry today.
/// </summary>
/// <remarks>
/// The <c>can*</c> flags are set against code that exists, not against the provider's feature
/// list. <c>canPurge</c> and <c>canReplay</c> are false because nothing implements them yet —
/// they arrive with the operations console and capture respectively. When they do, this record
/// changes in the same commit as the code.
/// </remarks>
public sealed class RabbitMqProviderRegistry : IProviderRegistry
{
    private static readonly ProviderCapabilities RabbitMq = new(
        Kind: "rabbitmq",
        DisplayName: "RabbitMQ",
        SupportTier: "full",
        CanTap: true,
        CanPeek: true,
        CanPublish: true,
        CanDiscard: true,
        CanPurge: false,
        CanReplay: false,
        CanDeclareShadow: true,
        AddressingModel: "exchange_routing",
        DeadLetterModel: "dlx",
        CountingModel: "exact",
        ReplayModel: "republish",
        DiscardModel: "delete_one",
        SupportsHeaders: true,
        MaxPayloadBytes: 134_217_728);

    public IReadOnlyList<ProviderCapabilities> All { get; } = [RabbitMq];

    public ProviderCapabilities? Find(string kind) =>
        All.FirstOrDefault(p => string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase));
}
