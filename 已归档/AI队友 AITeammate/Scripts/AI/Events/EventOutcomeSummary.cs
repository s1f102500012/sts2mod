using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class EventOutcomeSummary
{
    public int GoldDelta { get; init; }

    public int HpDelta { get; init; }

    public int MaxHpDelta { get; init; }

    public int PotionRewardCount { get; init; }

    public int CardRewardCount { get; init; }

    public int RemoveCount { get; init; }

    public int UpgradeCount { get; init; }

    public int TransformCount { get; init; }

    public int EnchantCount { get; init; }

    public int FixedCardCount => FixedCardIds.Count;

    public bool LeaveLike { get; init; }

    public bool ProceedLike { get; init; }

    public bool StartsCombat { get; init; }

    public bool HasRandomness { get; init; }

    public bool HasUnknownEffects { get; init; }

    public IReadOnlyList<string> RelicIds { get; init; } = [];

    public IReadOnlyList<string> PotionIds { get; init; } = [];

    public IReadOnlyList<string> FixedCardIds { get; init; } = [];

    public IReadOnlyList<string> CurseCardIds { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string Describe()
    {
        return $"goldDelta={GoldDelta} hpDelta={HpDelta} maxHpDelta={MaxHpDelta} relics={RelicIds.Count} potions={PotionRewardCount} cardRewards={CardRewardCount} fixedCards={FixedCardCount} remove={RemoveCount} upgrade={UpgradeCount} transform={TransformCount} enchant={EnchantCount} curses={CurseCardIds.Count} startsCombat={StartsCombat} leaveLike={LeaveLike} proceedLike={ProceedLike} randomness={HasRandomness} unknown={HasUnknownEffects} notes=[{string.Join("; ", Notes)}]";
    }
}
