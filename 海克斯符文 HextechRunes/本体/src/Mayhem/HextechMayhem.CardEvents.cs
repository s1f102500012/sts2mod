namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (TrackPlayerAttackCardPlayedThisTurn(cardPlay)
			&& cardPlay.Card.Owner?.Creature.CombatState is HextechCombatState combatState)
		{
			RefreshPlayerAttackCostDoublingPreviews(HextechCombatCreatureHelper.GetAlivePlayerSideCreatures(combatState));
		}

		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, enemyHexContext) => effect.AfterCardPlayed(enemyHexContext, context, cardPlay));
	}

	public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterShuffle(context, choiceContext, shuffler));
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner?.Creature.CombatState?.RunState == RunState && card is WhiteHoleCard whiteHole)
		{
			await whiteHole.AfterDrawn();
		}

		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterCardDrawn(context, choiceContext, card, fromHandDraw));
	}

	// 记录每张会触发 Storm 的牌「开始打出时」玩家的 Storm 层数(在该牌 OnPlay 应用/叠加 StormPower 之前记录)。
	// 用于在 AfterCardPlayedLate 补发闪电时复刻原版 StormPower 的自排除:首次打出雷暴时此刻还没有
	// StormPower → 不记录 → 不会对雷暴自己发闪电;后续打出雷暴发的也是「打出前」的层数,与原版一致。
	private readonly Dictionary<CardModel, int> _stormLightningAtCardStart = new();

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		Player? owner = cardPlay.Card.Owner;
		if (owner != null
			&& StormUpgradeRune.ShouldTrigger(
				cardPlay.Card.Type,
				owner.GetRelic<StormUpgradeRune>() != null)
			&& owner.Creature.CombatState?.RunState == RunState
			&& owner.Creature.GetPower<StormPower>() is StormPower stormPower)
		{
			int lightning = Math.Max(0, (int)Math.Floor((decimal)stormPower.Amount));
			if (lightning > 0)
			{
				_stormLightningAtCardStart[cardPlay.Card] = lightning;
			}
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterCardPlayedLate(context, choiceContext, cardPlay));

		// 只对「打出前就已持有 Storm」且通过类型判定的牌补发闪电;发的是打出前记录的层数(排除雷暴自身首次触发)。
		if (!_stormLightningAtCardStart.Remove(cardPlay.Card, out int lightningCount) || lightningCount <= 0)
		{
			return;
		}

		Player? owner = cardPlay.Card.Owner;
		if (owner == null || owner.Creature.CombatState?.RunState != RunState)
		{
			return;
		}

		for (int i = 0; i < lightningCount; i++)
		{
			OrbModel orb = ModelDb.Orb<LightningOrb>().ToMutable();
			await OrbCmd.Channel(new BlockingPlayerChoiceContext(), orb, owner);
		}
	}

	public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterPlayerTurnStartLate(context, choiceContext, player));
	}

#if STS2_104_OR_NEWER
	public override async Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterAutoPrePlayPhaseEnteredLate(context, choiceContext, player));
	}
#else
	public override async Task BeforePlayPhaseStart(PlayerChoiceContext choiceContext, Player player)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforePlayPhaseStart(context, choiceContext, player));
	}
#endif
}
