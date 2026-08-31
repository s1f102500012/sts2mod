using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
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
		if (EntropyEnchantmentHooks.IsTransformingEntropyCard
			|| oldPileType != PileType.None
			|| deck?.Type != PileType.Deck
			|| card.Pile != deck
			|| ReferenceEquals(card, Card))
		{
			return;
		}

		CardModel replacement = Card.Owner.RunState.CloneCard(card);
		CardPileAddResult? transformResult = await EntropyEnchantmentHooks.TransformWithoutDeckModification(
			() => CardCmd.Transform(Card, replacement, CardPreviewStyle.HorizontalLayout));
		if (transformResult is { success: true } result)
		{
			Log.Info(
				$"[{ModInfo.Id}][EntropyIncrease] Transformed {Card.Id.Entry} into "
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
			&& EnchantmentCompositionAdapter.Find(deckCard, typeof(EntropyDecrease)) is EntropyDecrease deckEntropyDecrease)
		{
			deckEntropyDecrease.PendingRemoval = true;
		}

		return Task.CompletedTask;
	}
}
