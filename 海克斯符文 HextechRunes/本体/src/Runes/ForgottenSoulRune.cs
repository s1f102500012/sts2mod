namespace HextechRunes;

public sealed class ForgottenSoulRune : HextechRelicBase
{
	private bool _preventedExhaustLastPlay;

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		_preventedExhaustLastPlay = false;
		if (card.Owner == Owner
			&& pileType == PileType.Exhaust
			&& card.Type != CardType.Status
			&& card.Keywords.Contains(CardKeyword.Exhaust))
		{
			_preventedExhaustLastPlay = true;
			return (PileType.Discard, position);
		}

		return (pileType, position);
	}

	public override Task AfterModifyingCardPlayResultPileOrPositionCompat(CardModel card, PileType pileType, CardPilePosition position)
	{
		if (_preventedExhaustLastPlay && card.Owner == Owner)
		{
			Flash();
		}

		_preventedExhaustLastPlay = false;
		return Task.CompletedTask;
	}
}
