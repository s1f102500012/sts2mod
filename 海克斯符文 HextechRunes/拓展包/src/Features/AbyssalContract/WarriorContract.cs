using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunesSponsorPack;

// 战士:牌组只留攻击牌,换来固定力量;每打若干精英再加一层,阈值随已获层数递增。
internal sealed class WarriorContract : AbyssalContractBase
{
	public override IEnumerable<IHoverTip> ExtraHoverTips =>
		HoverTipFactory.FromRelic<WarriorContractChoiceRelic>();

	public override async Task ApplyInitialEffect(AbyssalContractRune rune)
	{
		await RemoveForbiddenCards(rune);
		await UpgradeCurrentStartingRelic(rune);
	}

	public override async Task BeforeCombatStart(AbyssalContractRune rune)
	{
		rune.Flash();
		await PowerCmd.Apply<StrengthPower>(
			rune.Owner.Creature,
			AbyssalContractRune.WarriorInitialStrength + rune.SavedWarriorStrengthBonuses,
			rune.Owner.Creature,
			null);
	}

	public override Task AfterCombatVictory(AbyssalContractRune rune, CombatRoom room)
	{
		if (room.RoomType != RoomType.Elite)
		{
			return Task.CompletedTask;
		}

		int eliteKills = rune.SavedWarriorEliteKills;
		int strengthBonuses = rune.SavedWarriorStrengthBonuses;
		bool gainedStrength = AbyssalContractRune.AdvanceWarriorEliteProgress(ref eliteKills, ref strengthBonuses);
		rune.SavedWarriorEliteKills = eliteKills;
		rune.SavedWarriorStrengthBonuses = strengthBonuses;
		if (gainedStrength)
		{
			rune.Flash();
		}

		return Task.CompletedTask;
	}

	public override bool ShouldAddToDeck(AbyssalContractRune rune, CardModel card)
	{
		return card.Owner != rune.Owner
			|| !AbyssalContractRune.IsWarriorForbiddenCardType(card.Type);
	}

	public override Task AfterAddToDeckPrevented(AbyssalContractRune rune, CardModel card)
	{
		if (card.Owner == rune.Owner
			&& AbyssalContractRune.IsWarriorForbiddenCardType(card.Type))
		{
			rune.Flash();
		}

		return Task.CompletedTask;
	}

	private static async Task RemoveForbiddenCards(AbyssalContractRune rune)
	{
		Player? owner = rune.Owner;
		if (owner == null)
		{
			return;
		}

		IReadOnlyList<CardModel> cards = owner.Deck.Cards
			.Where(static card => AbyssalContractRune.IsWarriorForbiddenCardType(card.Type))
			.ToArray();
		if (cards.Count > 0)
		{
			await CardPileCmd.RemoveFromDeck(cards, showPreview: true);
		}
	}
}
