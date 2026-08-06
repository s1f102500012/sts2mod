using MegaCrit.Sts2.Core.CardSelection;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static bool StormBeforeCardPlayedPrefix(StormPower __instance, ref Task __result)
	{
		if (ShouldUseHextechStormHandling(__instance))
		{
			__result = Task.CompletedTask;
			return false;
		}

		return true;
	}

	private static bool StormAfterCardPlayedPrefix(StormPower __instance, ref Task __result)
	{
		if (ShouldUseHextechStormHandling(__instance))
		{
			__result = Task.CompletedTask;
			return false;
		}

		return true;
	}

	private static bool EntropyAfterPlayerTurnStartPrefix(EntropyPower __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__instance.Owner?.Player?.GetRelic<MysteryRune>() == null)
		{
			return true;
		}

		__result = SafeEntropyAfterPlayerTurnStart(__instance, choiceContext, player);
		return false;
	}

	private static async Task SafeEntropyAfterPlayerTurnStart(EntropyPower entropyPower, PlayerChoiceContext choiceContext, Player player)
	{
		if (player != entropyPower.Owner.Player)
		{
			return;
		}

		IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
			choiceContext,
			player,
			new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, entropyPower.Amount),
			CardTransformUpgradeHelper.CanTransformToRandomCard,
			entropyPower);

		List<CardModel> selectedCards = selected.ToList();
		for (int i = 0; i < selectedCards.Count; i++)
		{
			CardModel card = selectedCards[i];
			if (CardTransformUpgradeHelper.CanTransformToRandomCard(card))
			{
				await CardTransformUpgradeHelper.TransformToStableRandom(
					card,
					(RunState)player.RunState,
					"entropy-transform-replacement",
					i,
					saltParts:
					[
						HextechStableRandom.PlayerKey(player),
						player.Creature.CombatState?.RoundNumber.ToString() ?? "-1",
						entropyPower.Amount.ToString(),
						HextechStableRandom.CardPileKey(selectedCards)
					]);
			}
		}
	}

	private static bool ShouldUseHextechStormHandling(StormPower stormPower)
	{
		Player? owner = stormPower.Owner?.Player;
		return ShouldUseHextechStormHandling(
			owner?.Creature.CombatState?.RunState is RunState runState
				&& HextechMayhemModifier.FindIn(runState) != null,
			owner?.GetRelic<StormUpgradeRune>() != null);
	}

	internal static bool ShouldUseHextechStormHandling(bool hasMayhemModifier, bool hasStormUpgradeRune)
	{
		return hasMayhemModifier && hasStormUpgradeRune;
	}
}
