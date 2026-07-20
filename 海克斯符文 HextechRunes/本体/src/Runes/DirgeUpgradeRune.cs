namespace HextechRunes;

public sealed class DirgeUpgradeRune : CardUpgradeRuneBase<Dirge>
{
	private bool _preventedExhaustLastPlay;

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		_preventedExhaustLastPlay = false;
		if (card.Owner == Owner && card is Dirge && pileType == PileType.Exhaust)
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
