using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

public sealed class CompactUpgradeRune : CardUpgradeRuneBase<Compact>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is Compact && card.Owner?.GetRelic<CompactUpgradeRune>() != null;
	}

	internal static async Task PlayUpgraded(PlayerChoiceContext choiceContext, Compact card, CardPlay cardPlay)
	{
		var owner = card.Owner!;
		var combatState = card.CombatState!;
		PlayerCombatState? playerCombatState = owner.PlayerCombatState;
		if (playerCombatState == null)
		{
			return;
		}

		await CreatureCmd.GainBlock(owner.Creature, card.DynamicVars.Block, cardPlay);

		List<CardTransformation> transformations = new();
		foreach (CardPile pile in playerCombatState.AllPiles)
		{
			foreach (CardModel statusCard in pile.Cards)
			{
				if (!statusCard.IsTransformable || statusCard.Type != CardType.Status)
				{
					continue;
				}

				CardModel fuel = combatState.CreateCard<Fuel>(owner);
				if (card.IsUpgraded)
				{
					CardCmd.Upgrade(fuel);
				}

				transformations.Add(new CardTransformation(statusCard, fuel));
			}
		}

		if (transformations.Count == 0)
		{
			return;
		}

		owner.GetRelic<CompactUpgradeRune>()?.Flash();
		await CardCmd.Transform(transformations, null, CardPreviewStyle.None);
	}

	[HarmonyPatch(typeof(Compact), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.compact", "升级压缩", Rune = typeof(CompactUpgradeRune))]
	private static class CompactPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(Compact __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!CompactUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = CompactUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
