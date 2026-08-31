namespace HextechRunes;

public sealed class EchoRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsRegentPlayer(player);
	}

#if STS2_104_OR_NEWER
	public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
#else
	public override async Task AfterCardGeneratedForCombat(CardModel card, bool addedByPlayer)
#endif
	{
#if STS2_104_OR_NEWER
		bool addedByPlayer = creator == Owner;
#endif
		if (!addedByPlayer
			|| Owner == null
			|| Owner.Creature.IsDead
			|| card.Owner != Owner
			|| !TryGetEchoPile(card, out PileType pileType))
		{
			return;
		}

		CardModel echo = card.CreateClone();
		echo.AddKeyword(CardKeyword.Ethereal);
		echo.SetToFreeThisTurn();

		// echo 已从完成生成的原牌克隆，直接入堆即可。再次走 AddGeneratedCardToCombat 会递归触发
		// 整条生成钩子链；多次打出需要选择的生成牌时，两端可能因此构造出不同的分支任务树。
		Flash();
		await CardPileCmd.Add(echo, pileType, CardPilePosition.Bottom, this);
	}

	private static bool TryGetEchoPile(CardModel card, out PileType pileType)
	{
		pileType = card.Pile?.Type ?? PileType.None;
		return pileType is PileType.Hand or PileType.Draw or PileType.Discard;
	}
}
