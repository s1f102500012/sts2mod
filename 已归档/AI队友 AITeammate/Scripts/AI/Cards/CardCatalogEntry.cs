using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal sealed class CardCatalogEntry
{
    public required string CardId { get; init; }

    public required string Name { get; init; }

    public required string PoolId { get; init; }

    public required CardType Type { get; init; }

    public required string Rarity { get; init; }

    public required CardMultiplayerConstraint MultiplayerConstraint { get; init; }

    public required TargetType TargetType { get; init; }

    public bool ShouldShowInCardLibrary { get; init; }

    public int BaseCost { get; init; }

    public bool HasXCost { get; init; }

    public int BaseStarCost { get; init; } = -1;

    public bool HasXStarCost { get; init; }

    public string BaseDescription { get; init; } = string.Empty;

    public string UpgradeDescriptionPreview { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<HoverTipRef> HoverTipRefs { get; init; } = [];

    public int MaxUpgradeLevel { get; init; }

    public CardFlags BaseFlags { get; init; } = new();

    public IReadOnlyDictionary<string, int> BaseDynamicVars { get; init; } = new Dictionary<string, int>();

    public CardUpgradeSpec UpgradeSpec { get; init; } = CardUpgradeSpec.Empty;

    public CardSemanticProfile SemanticProfile { get; init; } = new();
}
