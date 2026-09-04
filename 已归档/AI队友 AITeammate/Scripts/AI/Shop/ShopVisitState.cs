using MegaCrit.Sts2.Core.Entities.Players;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AITeammate.Scripts;

internal sealed class ShopVisitState
{
    public required ulong PlayerId { get; init; }

    public required Player Player { get; init; }

    public ulong? InventoryOwnerPlayerId { get; init; }

    public required MerchantInventory RuntimeInventory { get; init; }

    public required string RoomVisitKey { get; init; }

    public required ShopExecutionMode ExecutionMode { get; init; }

    public required bool VisitCompleted { get; init; }

    public required string RoomType { get; init; }

    public required string SnapshotFingerprint { get; init; }

    public required int Gold { get; init; }

    public required bool InventoryIsOpen { get; init; }

    public required int MaxPotionSlots { get; init; }

    public required int FilledPotionSlots { get; init; }

    public required bool HasOpenPotionSlots { get; init; }

    public required bool HasUsableFoulPotion { get; init; }

    public required bool HasCourier { get; init; }

    public required bool HasMembershipCard { get; init; }

    public required bool HasSozu { get; init; }

    public required bool HasHoarder { get; init; }

    public required bool CardRemovalAvailable { get; init; }

    public required DeckSummary DeckSummary { get; init; }

    public required IReadOnlyList<ResolvedCardView> DeckCards { get; init; }

    public required IReadOnlyList<ShopDeckCard> DeckEntries { get; init; }

    public required IReadOnlySet<string> RelicIds { get; init; }

    public required IReadOnlySet<string> ModifierIds { get; init; }

    public required IReadOnlyList<ShopOwnedPotion> OwnedPotions { get; init; }

    public required IReadOnlyList<ShopOffer> Offers { get; init; }

    public required IReadOnlyList<ShopAction> Actions { get; init; }

    public string DescribeSummary()
    {
        return $"room={RoomType} fingerprint={SnapshotFingerprint} roomKey={RoomVisitKey} mode={ExecutionMode} visitCompleted={VisitCompleted} gold={Gold} inventoryOwner={InventoryOwnerPlayerId?.ToString() ?? "none"} inventoryOpen={InventoryIsOpen} offers={Offers.Count} actions={Actions.Count} potions={FilledPotionSlots}/{MaxPotionSlots} foulPotion={HasUsableFoulPotion} courier={HasCourier} membership={HasMembershipCard} sozu={HasSozu} hoarder={HasHoarder} removalAvailable={CardRemovalAvailable}";
    }

    public string DescribeDeckSummary()
    {
        return $"deck cards={DeckSummary.CardCount} upgraded={DeckSummary.UpgradedCardCount} atk={DeckSummary.AttackCount} skill={DeckSummary.SkillCount} power={DeckSummary.PowerCount} dmgSrc={DeckSummary.FrontloadDamageSources} qDmg={DeckSummary.QualityDamageSources} blockSrc={DeckSummary.BlockSources} qDef={DeckSummary.QualityDefenseSources} drawSrc={DeckSummary.DrawSources} energySrc={DeckSummary.EnergySources} scaling={DeckSummary.ScalingSources} engine={DeckSummary.EngineCards} orb={DeckSummary.OrbCards} focus={DeckSummary.FocusCards} orbSlot={DeckSummary.OrbSlotCards} powerPayoff={DeckSummary.PowerPayoffCards} recur={DeckSummary.RecursionCards} avgCost={DeckSummary.AverageCost:F2}";
    }
}
