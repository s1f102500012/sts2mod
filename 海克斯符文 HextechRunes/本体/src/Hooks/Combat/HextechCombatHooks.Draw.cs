namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static bool DrawPrefix(PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw, ref Task<IEnumerable<CardModel>> __result)
	{
		// 抽牌必经路径:prefix 抛异常会让整个 Draw 调用中断、抽牌任务链卡死(游戏卡住);
		// 判定阶段任何意外都放行原版抽牌。
		CardInspectionRune? cardInspectionRune;
		try
		{
			cardInspectionRune = player.GetRelic<CardInspectionRune>();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Draw] Draw prefix relic lookup failed; falling back to vanilla draw: {ex.GetType().Name}: {ex.Message}");
			return true;
		}

		if (cardInspectionRune != null && fromHandDraw && count > 0m && player.Creature.CombatState != null)
		{
			cardInspectionRune.Flash();
			__result = HextechSelectedDrawHelper.DrawSelectedFromDrawPile(
				choiceContext,
				player,
				(int)Math.Ceiling(count),
				fromHandDraw: true);
			return false;
		}

		return true;
	}
}
