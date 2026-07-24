namespace HextechRunes;

public abstract class UniversalScopeRuneBase : HextechRelicBase
{
	private int _refundRollsThisCombat;

	protected abstract int ChancePercent { get; }

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ChancePercent", ChancePercent)
	];

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		// 必须在 series 的最后一次打出后才把牌移回手牌:重放(playCount>1)会让同一张牌在 Play
		// 牌堆里连续打多次,若在 IsFirstInSeries 就移走,后续重放找不到这张牌会崩溃。
		// 普通牌 IsFirstInSeries==IsLastInSeries,行为不变。
		// 不排除一般的自动打出:地狱狂徒(Hellraiser)抽到牌即自动打出(IsAutoPlay),这些卡组
		// 攻击牌同样应能被瞄准镜返还;返还到手牌不算抽牌、不会再触发自动打,无死循环。
		// 但要排除两类"返回手牌后只会卡死"的牌:
		// 1. Unplayable 牌——无法从手牌手动打出;
		// 2. 本 mod 的受管临时牌(自动巡逻的扫荡凝视、中和强化的分身等)——打完后由
		//    AutoPlayTransientCardAndCleanup 强制离场,返还会让模型被移除而手牌 UI 残留
		//    一张点不动的幽灵牌。两个判定都基于牌的固有属性/两端一致执行的战斗流程,
		//    联机确定性安全;且在 roll 之前 return,不影响 proc ordinal 计数的两端一致。
		if (Owner == null
			|| Owner.Creature.IsDead
			|| !cardPlay.IsLastInSeries
			|| !IsOwnedAttack(cardPlay.Card)
			|| cardPlay.Card.Keywords.Contains(CardKeyword.Unplayable)
			|| HextechAutoPlayHelper.IsTransientAutoPlayCard(cardPlay.Card))
		{
			return;
		}

		int rollOrdinal = ConsumeCombatProcOrdinal(GetType().Name, ref _refundRollsThisCombat);
		if (!RollTrigger(cardPlay, rollOrdinal))
		{
			return;
		}

		HextechCardPlayResourceSpend resourceSpend = HextechCombatHooks.GetResourceSpendForCurrentCardPlay(cardPlay.Card);
		Flash();
		if (cardPlay.Card.Pile?.Type == PileType.Play)
		{
			// 多级“升级：打击/防御”牌在移出 Play 堆时会经过完整的 AfterCardChangedPiles
			// 链。部分原版/第三方改牌逻辑会在这条链里按 canonical 状态重算牌，导致升级层数
			// 回落。先快照，等移牌(含满手时落入弃牌堆)彻底完成后只补回丢失的层数。
			int capturedUpgradeLevel = cardPlay.Card.CurrentUpgradeLevel;
			await CardPileCmd.Add(cardPlay.Card, PileType.Hand, CardPilePosition.Bottom, this);
			CardTransformUpgradeHelper.RestoreUpgradeLevel(cardPlay.Card, capturedUpgradeLevel);
		}

		if (resourceSpend.Energy > 0m)
		{
			await PlayerCmd.GainEnergy(resourceSpend.Energy, Owner);
		}

		if (resourceSpend.Stars > 0m)
		{
			await PlayerCmd.GainStars(resourceSpend.Stars, Owner);
		}
	}

	private bool RollTrigger(CardPlay cardPlay, int rollOrdinal)
	{
		if (Owner == null)
		{
			return false;
		}

		return HextechStableRandom.PercentChance(
			(RunState)Owner.RunState,
			DynamicVars["ChancePercent"].IntValue,
				"universal-scope-refund",
				GetType().Name,
				HextechStableRandom.PlayerKey(Owner),
				Owner.Creature.CombatState?.RoundNumber.ToString() ?? "-1",
				rollOrdinal.ToString(),
				HextechStableRandom.CardKey(cardPlay.Card));
	}
}

public sealed class UniversalScopeRune : UniversalScopeRuneBase
{
	protected override int ChancePercent => 15;
}
