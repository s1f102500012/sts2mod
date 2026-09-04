using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CardUpgradeSpec
{
    public static CardUpgradeSpec Empty { get; } = new()
    {
        EffectAmountAdjustments = new Dictionary<EffectAdjustmentKey, int>()
    };

    public int CostDelta { get; init; }

    public int? CostOverride { get; init; }

    public bool? Exhaust { get; init; }

    public bool? Ethereal { get; init; }

    public bool? Retain { get; init; }

    public int? ReplayCountOverride { get; init; }

    public IReadOnlyDictionary<EffectAdjustmentKey, int> EffectAmountAdjustments { get; init; } =
        new Dictionary<EffectAdjustmentKey, int>();
}
