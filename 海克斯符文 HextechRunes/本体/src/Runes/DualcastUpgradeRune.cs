namespace HextechRunes;

public sealed class DualcastUpgradeRune : CardUpgradeRuneBase<Dualcast>
{
	internal override bool GrantsCardOnPickup => false;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Dualcast>(),
		HoverTipFactory.FromCard<Quadcast>()
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsDefectPlayer(player);
	}

	internal override bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return deckCards.Any(static card => card is Dualcast or Quadcast);
	}

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		// 去向 None 的临时复制品必须继续销毁，否则回手后会残留重复实体。
		if (Owner != null
			&& card.Owner == Owner
			&& IsSupportedCard(card)
			&& CanReturnFromResultPile(pileType))
		{
			Flash();
			return (PileType.Hand, CardPilePosition.Bottom);
		}

		return (pileType, position);
	}

	internal static bool IsSupportedCard(CardModel card)
	{
		return card is Dualcast or Quadcast;
	}

	internal static bool CanReturnFromResultPile(PileType pileType)
	{
		return pileType is not PileType.None;
	}
}
