using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class EntropyIncrease : EnchantmentModel
{
	public override bool ShouldGlowGold => true;

	public override bool CanEnchant(CardModel card)
	{
		return card.IsTransformable && base.CanEnchant(card);
	}

	public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
	{
		CardPile? deck = Card.Pile;
		if (EntropyTransformScope.IsActive
			|| oldPileType != PileType.None
			|| deck?.Type != PileType.Deck
			|| card.Pile != deck
			|| ReferenceEquals(card, Card))
		{
			return;
		}

		CardModel replacement = Card.Owner.RunState.CloneCard(card);
		CardPileAddResult? transformResult;
		using (EntropyTransformScope.Enter())
		{
			transformResult = await CardCmd.Transform(Card, replacement, CardPreviewStyle.HorizontalLayout);
		}

		if (transformResult is { success: true } result)
		{
			Log.Info(
				$"[{ModInfo.Id}] EntropyIncrease transformed {Card.Id.Entry} into "
				+ $"{result.cardAdded.Id.Entry} after obtaining {card.Id.Entry}.");
		}
	}
}

public sealed class EntropyDecrease : EnchantmentModel
{
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool PendingRemoval { get; set; }

	public override bool ShouldGlowRed => true;

	public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (cardPlay.Card != Card)
		{
			return Task.CompletedTask;
		}

		PendingRemoval = true;
		CardModel deckCard = Card.DeckVersion ?? Card;
		if (!ReferenceEquals(deckCard, Card)
			&& deckCard.Enchantment is EntropyDecrease deckEntropyDecrease)
		{
			deckEntropyDecrease.PendingRemoval = true;
		}

		return Task.CompletedTask;
	}

	// 战后移出牌组走附魔自己的模型回调,不再对 Hook.AfterCombatEnd 打补丁:牌组卡的附魔本来就在
	// RunState.IterateHookListeners 的枚举里(0.111 RunState.cs 第 550-566 行,牌组卡与其附魔排在最前),
	// 战斗副本的附魔排在其后。
	//
	// 「一次预览批量删除」靠第一个被回调的实例包办:它扫整个牌组一次性删完,其余实例回调时自己的牌
	// 已不在牌组,直接返回。
	public override async Task AfterCombatEnd(CombatRoom room)
	{
		if (!PendingRemoval || !HasCard)
		{
			return;
		}

		CardModel deckCard = Card.DeckVersion ?? Card;
		if (deckCard.Pile?.Type != PileType.Deck)
		{
			return;
		}

		Player owner = deckCard.Owner;
		if (owner == null)
		{
			return;
		}

		IReadOnlyList<CardModel> cardsToRemove = CollectPendingRemovalCards(owner.Deck.Cards);
		if (cardsToRemove.Count == 0)
		{
			return;
		}

		foreach (CardModel card in cardsToRemove)
		{
			Log.Info($"[{ModInfo.Id}] EntropyDecrease removing {card.Id.Entry} from the deck after combat.");
		}

		await CardPileCmd.RemoveFromDeck(cardsToRemove, showPreview: true);
	}

	// 纯函数:从一堆牌里挑出「本场已打出、待移出牌组」的熵减牌,保持给定顺序。
	internal static IReadOnlyList<CardModel> CollectPendingRemovalCards(IEnumerable<CardModel> cards)
	{
		return cards
			.Where(static card => card.Enchantment is EntropyDecrease { PendingRemoval: true })
			.ToArray();
	}
}
