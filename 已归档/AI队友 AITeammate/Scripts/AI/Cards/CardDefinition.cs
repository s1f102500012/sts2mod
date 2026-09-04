using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal sealed class CardDefinition
{
    public required string CardId { get; init; }

    public required string Name { get; init; }

    public required CardType Type { get; init; }

    public required TargetType Targeting { get; init; }

    public required int BaseCost { get; init; }

    public required string Rarity { get; init; }

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public bool Exhaust { get; init; }

    public bool Ethereal { get; init; }

    public bool Retain { get; init; }

    public int ReplayCount { get; init; }

    public IReadOnlyList<NormalizedEffectDescriptor> Effects { get; init; } = [];

    public CardUpgradeSpec UpgradeSpec { get; init; } = CardUpgradeSpec.Empty;
}
