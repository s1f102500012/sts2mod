using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AITeammate.Scripts;

internal sealed class ShopRuntimeLocator
{
    public required string LocatorId { get; init; }

    public required string SlotGroup { get; init; }

    public required int SlotIndex { get; init; }

    public MerchantEntry? Entry { get; init; }
}
