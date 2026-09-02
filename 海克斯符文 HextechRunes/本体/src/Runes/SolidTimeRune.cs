namespace HextechRunes;

public sealed partial class SolidTimeRune : HextechRelicBase
{
	private bool _startedThisCombat;
	private string _removedCardsJson = "";

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public string SavedRemovedPowerCardsJson
	{
		get => _removedCardsJson;
		set => _removedCardsJson = value ?? "";
	}

	public override Task BeforeCombatStart()
	{
		_startedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_startedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		return TriggerStoredPowersAtCombatStart(choiceContext, player);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card.Type != CardType.Power
			|| !TryGetDeckPower(cardPlay.Card, out CardModel? deckCard))
		{
			return;
		}

		AppendStoredCard(deckCard!);
		Flash();
		await CardPileCmd.RemoveFromDeck(deckCard!, showPreview: false);
	}

	private async Task TriggerStoredPowersAtCombatStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (_startedThisCombat
			|| player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		_startedThisCombat = true;
		List<StoredCard> cards = DecodeStoredCards();
		if (cards.Count == 0)
		{
			return;
		}

		Flash();
		for (int i = 0; i < cards.Count; i++)
		{
			if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead)
			{
				break;
			}

			CardModel? card = CreateCombatCard(combatState, cards[i]);
			if (card == null)
			{
				continue;
			}

			card.SetToFreeThisCombat();
			Creature? target = PickTarget(card, combatState, i);
			await ApplyStoredPowerDirectly(choiceContext, card, target);
		}
	}

	private bool TryGetDeckPower(CardModel combatCard, out CardModel? deckCard)
	{
		deckCard = combatCard.DeckVersion;
		return deckCard != null
			&& deckCard.Owner == Owner
			&& deckCard.Pile?.Type == PileType.Deck
			&& deckCard.Type == CardType.Power
			&& Owner.Deck.Cards.Contains(deckCard);
	}
}
