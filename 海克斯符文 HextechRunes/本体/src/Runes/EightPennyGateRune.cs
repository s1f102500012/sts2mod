namespace HextechRunes;

public sealed class EightPennyGateRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Replays", 1m)
	];

	public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPositionCompat(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
	{
		// 去向 None(复制品/能力牌)不抢改:改成消耗堆会留下滞留的幽灵实体(空白手牌位问题同源)。
		return pileType is not PileType.None && ShouldReplayAndExhaust(card) ? (PileType.Exhaust, position) : (pileType, position);
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (!ShouldReplayAndExhaust(card))
		{
			return playCount;
		}

		return playCount + DynamicVars["Replays"].IntValue;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (ShouldReplayAndExhaust(card))
		{
			Flash();
		}

		return Task.CompletedTask;
	}

	private bool ShouldReplayAndExhaust(CardModel card)
	{
		return card.Owner == Owner && card.Type is CardType.Attack or CardType.Skill;
	}
}
