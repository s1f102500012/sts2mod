using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class SurvivorUpgradeRune : CardUpgradeRuneBase<Survivor>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsSilentPlayer(player);
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is Survivor && card.Owner?.GetRelic<SurvivorUpgradeRune>() != null;
	}

	internal static async Task PlayUpgraded(PlayerChoiceContext choiceContext, Survivor card, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);

		int maxSelectable = PileType.Hand.GetPile(card.Owner).Cards.Count(handCard => !ReferenceEquals(handCard, card));
		if (maxSelectable <= 0)
		{
			return;
		}

		IEnumerable<CardModel> selected = await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			card.Owner,
			new CardSelectorPrefs(new LocString("cards", "survivorUpgradeRune.selectionScreenPrompt"), 0, maxSelectable),
			null,
			card);

		List<CardModel> cards = selected.ToList();
		if (cards.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, cards);
		await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block.BaseValue * cards.Count, card.DynamicVars.Block.Props, cardPlay);
		card.Owner.GetRelic<SurvivorUpgradeRune>()?.Flash();
	}

	[HarmonyPatch(typeof(Survivor), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.survivor", "升级幸存者", Rune = typeof(SurvivorUpgradeRune))]
	private static class SurvivorPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(Survivor __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!SurvivorUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = SurvivorUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
