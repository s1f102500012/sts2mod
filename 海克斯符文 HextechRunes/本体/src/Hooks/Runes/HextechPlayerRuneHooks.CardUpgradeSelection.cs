namespace HextechRunes;

internal static partial class HextechPlayerRuneHooks
{
	private static bool BodySlamOnPlayPrefix(
		BodySlam __instance,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay,
		ref Task __result)
	{
		if (__instance.Owner?.GetRelic<BodySlamUpgradeRune>() is not BodySlamUpgradeRune rune)
		{
			return true;
		}

		__result = rune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool WroughtInWarOnPlayPrefix(
		WroughtInWar __instance,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay,
		ref Task __result)
	{
		if (__instance.Owner?.GetRelic<WroughtInWarUpgradeRune>() is not WroughtInWarUpgradeRune rune)
		{
			return true;
		}

		__result = rune.PlayUpgraded(choiceContext, __instance, cardPlay);
		return false;
	}

	private static bool DecisionsDecisionsOnPlayPrefix(
		DecisionsDecisions __instance,
		PlayerChoiceContext choiceContext,
		ref Task __result)
	{
		if (__instance.Owner?.GetRelic<DecisionsDecisionsUpgradeRune>() is not DecisionsDecisionsUpgradeRune rune)
		{
			return true;
		}

		__result = rune.PlayUpgraded(choiceContext, __instance);
		return false;
	}

	private static void DecisionsDecisionsFromHandPrefix(
		AbstractModel source,
		ref Func<CardModel, bool> filter)
	{
		if (source is not DecisionsDecisions card
			|| card.Owner?.GetRelic<DecisionsDecisionsUpgradeRune>() is not DecisionsDecisionsUpgradeRune rune)
		{
			return;
		}

		rune.Flash();
		filter = DecisionsDecisionsUpgradeRune.CanSelectCard;
	}
}
