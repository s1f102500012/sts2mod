namespace HextechRunes;

public sealed class CorrosiveWaveUpgradeRune : CardUpgradeRuneBase<CorrosiveWave>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<CorrosiveWave>(),
		HoverTipFactory.FromPower<CorrosiveWavePower>(),
		HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
	];

	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		if (card.Owner != Owner || !ShouldExhaust(card, pileType))
		{
			return (pileType, position);
		}

		Flash();
		return (PileType.Exhaust, position);
	}

	internal static bool ShouldExhaust(CardModel card, PileType resultPile)
	{
		return resultPile is not PileType.None && card is CorrosiveWave;
	}
}
