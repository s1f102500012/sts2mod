namespace HextechRunes;

public sealed class BloodlettingUpgradeRune : CardUpgradeRuneBase<Bloodletting>
{
	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(
		CardModel card,
		bool isAutoPlay,
		ResourceInfo resources,
		PileType pileType,
		CardPilePosition position)
	{
		// 去向 None(复制品)不抢改:复制的放血回手会留下滞留的幽灵实体(空白手牌位问题同源)。
		if (pileType is not PileType.None && card.Owner == Owner && card is Bloodletting)
		{
			Flash();
			return (PileType.Hand, CardPilePosition.Bottom);
		}

		return (pileType, position);
	}
}
