using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class EventVisitState
{
    public required ulong PlayerId { get; init; }

    public required Player Player { get; init; }

    public required EventModel RuntimeEvent { get; init; }

    public required string EventId { get; init; }

    public required string EventTypeName { get; init; }

    public required string RoomType { get; init; }

    public required string SnapshotFingerprint { get; init; }

    public required uint PageIndex { get; init; }

    public required bool IsShared { get; init; }

    public required bool IsDeterministic { get; init; }

    public required bool IsFinished { get; init; }

    public required int Gold { get; init; }

    public required int CurrentHp { get; init; }

    public required int MaxHp { get; init; }

    public required DeckSummary DeckSummary { get; init; }

    public required IReadOnlyList<ResolvedCardView> DeckCards { get; init; }

    public required IReadOnlySet<string> RelicIds { get; init; }

    public required IReadOnlySet<string> ModifierIds { get; init; }

    public required string DescriptionText { get; init; }

    public string? DescriptionLocKey { get; init; }

    public required IReadOnlyList<EventOptionDescriptor> Options { get; init; }

    public string DescribeSummary()
    {
        return $"event={EventId} type={EventTypeName} room={RoomType} fingerprint={SnapshotFingerprint} page={PageIndex} shared={IsShared} deterministic={IsDeterministic} finished={IsFinished} gold={Gold} hp={CurrentHp}/{MaxHp} options={Options.Count}";
    }

    public string DescribeDeckSummary()
    {
        return $"deck cards={DeckSummary.CardCount} upgraded={DeckSummary.UpgradedCardCount} atk={DeckSummary.AttackCount} skill={DeckSummary.SkillCount} power={DeckSummary.PowerCount} dmgSrc={DeckSummary.FrontloadDamageSources} qDmg={DeckSummary.QualityDamageSources} blockSrc={DeckSummary.BlockSources} qDef={DeckSummary.QualityDefenseSources} drawSrc={DeckSummary.DrawSources} energySrc={DeckSummary.EnergySources} statusHandling={DeckSummary.StatusHandlingCards} scaling={DeckSummary.ScalingSources} engine={DeckSummary.EngineCards} orb={DeckSummary.OrbCards} focus={DeckSummary.FocusCards} orbSlot={DeckSummary.OrbSlotCards} powerPayoff={DeckSummary.PowerPayoffCards} recur={DeckSummary.RecursionCards} avgCost={DeckSummary.AverageCost:F2}";
    }
}
