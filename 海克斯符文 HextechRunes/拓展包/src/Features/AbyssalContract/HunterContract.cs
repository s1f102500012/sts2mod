using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunesSponsorPack;

// 猎人:开局塞入蛇牙,每打出若干张技能自动打出一张蛇牙,每隔若干场战斗再把一张牌变成蛇牙。
internal sealed class HunterContract : AbyssalContractBase
{
	public override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromRelic<HunterContractChoiceRelic>();

	public override Task ApplyInitialEffect(AbyssalContractRune rune)
	{
		return rune.AddContractCards<Snakebite>(AbyssalContractRune.HunterSnakebiteCount);
	}

	public override async Task AfterCombatVictory(AbyssalContractRune rune, CombatRoom room)
	{
		// setter 自带 % HunterCombatInterval,所以「归零」就是「又满一轮」。
		rune.SavedHunterCompletedCombats = rune.SavedHunterCompletedCombats + 1;
		if (rune.SavedHunterCompletedCombats != 0)
		{
			return;
		}

		await TransformRandomCardIntoSnakebite(rune);
	}

	public override async Task AfterCardPlayed(
		AbyssalContractRune rune,
		PlayerChoiceContext choiceContext,
		CardPlay cardPlay)
	{
		if (rune.AutoPlayingSnakebite
			|| cardPlay.Card.Owner != rune.Owner
			|| cardPlay.Card.Type != CardType.Skill)
		{
			return;
		}

		rune.HunterSkillsPlayedThisCombat++;
		if (rune.HunterSkillsPlayedThisCombat < AbyssalContractRune.HunterSkillInterval)
		{
			return;
		}

		rune.HunterSkillsPlayedThisCombat -= AbyssalContractRune.HunterSkillInterval;
		Snakebite? snakebite = rune.Owner.PlayerCombatState?.AllCards
			.OfType<Snakebite>()
			.FirstOrDefault(static card => card.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard);
		if (snakebite == null)
		{
			return;
		}

		rune.AutoPlayingSnakebite = true;
		try
		{
			rune.Flash();
			await CardPileCmd.Add(snakebite, PileType.Play);
			await CardCmd.AutoPlay(choiceContext, snakebite, null);
		}
		finally
		{
			rune.AutoPlayingSnakebite = false;
		}
	}

	public override bool TryModifyEnergyCostInCombat(
		AbyssalContractRune rune,
		CardModel card,
		decimal originalCost,
		out decimal modifiedCost)
	{
		modifiedCost = originalCost;
		if (card.Owner != rune.Owner
			|| card is not Snakebite
			|| card.EnergyCost.CostsX)
		{
			return false;
		}

		modifiedCost = Math.Max(0m, originalCost - AbyssalContractRune.HunterSnakebiteCostReduction);
		return true;
	}

	private static async Task TransformRandomCardIntoSnakebite(AbyssalContractRune rune)
	{
		Player? owner = rune.Owner;
		if (owner == null)
		{
			return;
		}

		IReadOnlyList<CardModel> nonSnakebites = owner.Deck.Cards
			.Where(static card => card is not Snakebite)
			.ToArray();
		if (nonSnakebites.Count == 0)
		{
			return;
		}

		IReadOnlyList<CardModel> attacks = nonSnakebites
			.Where(static card => card.Type == CardType.Attack)
			.ToArray();
		IReadOnlyList<CardModel> candidates = attacks.Count > 0 ? attacks : nonSnakebites;
		CardModel? original = owner.PlayerRng.Transformations.NextItem(candidates);
		if (original == null)
		{
			return;
		}

		CardModel replacement = owner.RunState.CreateCard<Snakebite>(owner);
		rune.Flash();
		await CardCmd.Transform(original, replacement, CardPreviewStyle.HorizontalLayout);
	}
}
