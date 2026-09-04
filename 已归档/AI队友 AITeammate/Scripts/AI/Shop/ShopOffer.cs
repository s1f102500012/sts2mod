using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AITeammate.Scripts;

internal sealed class ShopOffer
{
    public required string OfferId { get; init; }

    public required ShopOfferKind Kind { get; init; }

    public required string Name { get; init; }

    public required string ModelId { get; init; }

    public required int Cost { get; init; }

    public required bool IsStocked { get; init; }

    public required bool IsAffordable { get; init; }

    public required bool IsPurchaseLegalNow { get; init; }

    public required bool RequiresInventoryOpen { get; init; }

    public string? Rarity { get; init; }

    public string? Category { get; init; }

    public bool IsOnSale { get; init; }

    public ResolvedCardView? ResolvedCard { get; init; }

    public CardModel? RuntimeCardModel { get; init; }

    public RelicModel? RuntimeRelicModel { get; init; }

    public PotionModel? RuntimePotionModel { get; init; }

    public ShopRuntimeLocator? RuntimeLocator { get; init; }

    public MerchantEntry? Entry => RuntimeLocator?.Entry;

    public string Describe()
    {
        string details = string.Empty;
        if (!string.IsNullOrWhiteSpace(Rarity) || !string.IsNullOrWhiteSpace(Category))
        {
            details = $" rarity={Rarity ?? "n/a"} category={Category ?? "n/a"}";
        }

        return $"kind={Kind} id={ModelId} name={Name} cost={Cost} stocked={IsStocked} affordable={IsAffordable} legalNow={IsPurchaseLegalNow} requiresInventoryOpen={RequiresInventoryOpen} onSale={IsOnSale}{details}";
    }
}
