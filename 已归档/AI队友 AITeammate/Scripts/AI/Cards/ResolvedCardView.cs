using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal sealed class ResolvedCardView
{
    public required string CardInstanceId { get; init; }

    public required string CardId { get; init; }

    public required string Name { get; init; }

    public required CardType Type { get; init; }

    public required TargetType Targeting { get; init; }

    public required int EffectiveCost { get; init; }

    public required int StarCost { get; init; }

    public bool HasXStarCost { get; init; }

    public required string Rarity { get; init; }

    public required CardMultiplayerConstraint MultiplayerConstraint { get; init; }

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool Exhaust { get; init; }

    public bool Ethereal { get; init; }

    public bool Retain { get; init; }

    public int ReplayCount { get; init; }

    public bool IsUpgraded { get; init; }

    public int UpgradeLevel { get; init; }

    public IReadOnlyList<NormalizedEffectDescriptor> Effects { get; init; } = [];
}
