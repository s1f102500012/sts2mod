namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static bool DrawPrefix(PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw, ref Task<IEnumerable<CardModel>> __result)
	{
		// 抽牌必经路径:prefix 抛异常会让整个 Draw 调用中断、抽牌任务链卡死(游戏卡住);
		// 判定阶段任何意外都放行原版抽牌。
		CardInspectionRune? cardInspectionRune;
		NoNonsenseRune? noNonsenseRune;
		try
		{
			cardInspectionRune = player.GetRelic<CardInspectionRune>();
			noNonsenseRune = player.GetRelic<NoNonsenseRune>();
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

		if (noNonsenseRune == null || fromHandDraw || count <= 0m || player.Creature.CombatState == null)
		{
			return true;
		}

		int drawsPrevented = (int)Math.Ceiling(count);
		if (drawsPrevented <= 0)
		{
			__result = Task.FromResult<IEnumerable<CardModel>>(Array.Empty<CardModel>());
			return false;
		}

		__result = DrawNoNonsense(noNonsenseRune, drawsPrevented, player);
		return false;
	}

	private static async Task<IEnumerable<CardModel>> DrawNoNonsense(NoNonsenseRune noNonsenseRune, int drawsPrevented, Player player)
	{
		await noNonsenseRune.HandlePreventedNonHandDraw(drawsPrevented);
		await PowerCmd.Apply<StrengthPower>(player.Creature, drawsPrevented, player.Creature, null);
		return Array.Empty<CardModel>();
	}
}
