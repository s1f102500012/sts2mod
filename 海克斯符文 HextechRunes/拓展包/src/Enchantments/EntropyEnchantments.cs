using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

public sealed class EntropyIncrease : EnchantmentModel
{
	public override bool ShouldGlowGold => true;

	public override bool CanEnchant(CardModel card)
	{
		return card.IsTransformable && base.CanEnchant(card);
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
			&& EnchantmentCompositionAdapter.Find(deckCard.Enchantment, typeof(EntropyDecrease)) is EntropyDecrease deckEntropyDecrease)
		{
			deckEntropyDecrease.PendingRemoval = true;
		}

		return Task.CompletedTask;
	}
}
