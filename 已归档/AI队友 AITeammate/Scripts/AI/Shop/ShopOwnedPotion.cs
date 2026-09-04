namespace AITeammate.Scripts;

internal sealed class ShopOwnedPotion
{
    public required int SlotIndex { get; init; }

    public required string PotionId { get; init; }

    public required string Name { get; init; }

    public bool IsFoulPotion { get; init; }

    public bool IsUsableAtMerchant { get; init; }
}
