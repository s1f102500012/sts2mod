using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

public sealed class HiddenGemUpgradeRune : CardUpgradeRuneBase<HiddenGem>
{
	internal const PileType ReplayTargetPile = PileType.Hand;

	private int _upgradedPlaysThisCombat;

	protected override bool IsAvailableForCharacter(Player player)
	{
		return true;
	}

	internal static bool ShouldUseUpgradedPlay(CardModel card)
	{
		return card is HiddenGem && card.Owner?.GetRelic<HiddenGemUpgradeRune>() != null;
	}

	internal static async Task PlayUpgraded(PlayerChoiceContext choiceContext, HiddenGem card, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", card.Owner.Character.CastAnimDelay);

		List<CardModel> drawCards = PileType.Draw.GetPile(card.Owner).Cards.ToList();
		if (drawCards.Count == 0)
		{
			return;
		}

		List<CardModel> candidates = drawCards.Where(IsEligibleReplayTarget).ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		List<CardModel> preferred = candidates
			.Where(static candidate => candidate.Type is CardType.Attack or CardType.Skill or CardType.Power)
			.ToList();
		IEnumerable<CardModel> pool = preferred.Count == 0 ? candidates : preferred;
		HiddenGemUpgradeRune? rune = card.Owner.GetRelic<HiddenGemUpgradeRune>();
		if (rune == null)
		{
			return;
		}

		int ordinal = rune.ConsumeCombatProcOrdinal(nameof(HiddenGemUpgradeRune), ref rune._upgradedPlaysThisCombat);
		CardModel selected = HextechStableRandom.Pick(
			pool,
			(RunState)card.Owner.RunState,
			HextechStableRandom.CardKey,
			"hidden-gem-upgrade-play",
			HextechStableRandom.PlayerKey(card.Owner),
			card.Owner.Creature.CombatState?.RoundNumber.ToString() ?? "-1",
			ordinal.ToString(),
			HextechStableRandom.CardKey(card),
			HextechStableRandom.CardPileKey(drawCards));
		selected.BaseReplayCount += card.DynamicVars["Replay"].IntValue;
		rune.Flash();
		await CardPileCmd.Add(selected, ReplayTargetPile);
	}

	internal static bool IsEligibleReplayTarget(CardModel candidate)
	{
		return !candidate.Keywords.Contains(CardKeyword.Unplayable)
			&& candidate.Type is not CardType.Status and not CardType.Curse
			&& candidate.GetEnchantedReplayCount() < 1;
	}

	[HarmonyPatch(typeof(HiddenGem), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.hidden-gem", "升级未掘宝石", Rune = typeof(HiddenGemUpgradeRune))]
	private static class HiddenGemPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(HiddenGem __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
		{
			if (!HiddenGemUpgradeRune.ShouldUseUpgradedPlay(__instance))
			{
				return true;
			}

			__result = HiddenGemUpgradeRune.PlayUpgraded(choiceContext, __instance, cardPlay);
			return false;
		}
	}
}
