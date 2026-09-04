namespace BetterCharacterRelics;

internal static class RelicRules
{
    // 保留模组按全局轮数触发的契约；额外个人回合也属于同一轮。
    // 只校正原版贡献，保留其它 postfix 已加在结果里的修饰。
    internal static decimal AdjustDraw(decimal result, int? round, int turn,
        decimal vanillaTurns, decimal vanillaCards, int rounds)
    {
        decimal vanillaContribution = turn <= vanillaTurns ? vanillaCards : 0m;
        decimal intendedContribution = round.HasValue && round.Value <= rounds ? 3m : 0m;
        return result - vanillaContribution + intendedContribution;
    }
}
