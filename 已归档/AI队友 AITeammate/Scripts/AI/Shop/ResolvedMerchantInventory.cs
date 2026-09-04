using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AITeammate.Scripts;

internal sealed class ResolvedMerchantInventory
{
    public required MerchantInventory Inventory { get; init; }

    public required ShopExecutionMode ExecutionMode { get; init; }

    public required string RoomVisitKey { get; init; }

    public required bool VisitCompleted { get; init; }
}
