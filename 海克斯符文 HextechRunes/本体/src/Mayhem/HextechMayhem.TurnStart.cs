namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	// players 是全体存活玩家侧生物，playersTakingTurn 只含本次真正开始回合的玩家（队友额外回合时只有他）。
	// "每名玩家回合开始时"的敌方海克斯只作用于后者；按回合清空的记账仍整体清空——没参与的玩家这段时间
	// 不出牌，下个回合开始还会再清一次。
	private async Task BeforePlayerSideTurnStart(
		HextechCombatState combatState,
		IReadOnlyList<Creature> players,
		IReadOnlyList<Creature> playersTakingTurn,
		IReadOnlyList<Creature> participants)
	{
		_combatTracking.PreparePlayerSideTurnStart();
		_combatTracking.BeginPlayerTurnStart(combatState.Players
			.Where(player => HextechTurnParticipants.Includes(participants, player))
			.Select(static player => player.NetId));
		RefreshPlayerAttackCostDoublingPreviews(players);

		await ApplyToCurrentEnemiesIfNeeded();
		await ApplyDelayedEnemyHealingBlocks(combatState);
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			participants,
			(effect, context) => effect.BeforePlayerSideTurnStart(context, combatState, playersTakingTurn));
	}

	private async Task BeforeEnemySideTurnStart(HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		_combatTracking.PrepareEnemySideTurnStart();
		RefreshPlayerAttackCostDoublingPreviews(players);

		IReadOnlyList<Creature> enemies = HextechCombatCreatureHelper.GetAliveEnemies(combatState);
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.BeforeEnemySideTurnStart(context, combatState, players, enemies));
	}
}
