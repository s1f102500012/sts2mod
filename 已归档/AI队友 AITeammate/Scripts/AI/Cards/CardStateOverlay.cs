using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CardStateOverlay
{
    public int CostDelta { get; set; }

    public int? CostOverride { get; set; }

    public bool? Exhaust { get; set; }

    public bool? Ethereal { get; set; }

    public bool? Retain { get; set; }

    public int? ReplayCountOverride { get; set; }

    public Dictionary<EffectAdjustmentKey, int> EffectAmountAdjustments { get; } = new();
}
