namespace HextechRunes;

internal sealed class FrostWraithEnemyHex : HextechEnemyHexEffect
{
	internal const int TurnsNeeded = 3;
	internal const int TemporarySlowAmount = 50;

	internal override MonsterHexKind Kind => MonsterHexKind.FrostWraith;

	internal override async Task BeforeEnemySideTurnStart(
		HextechEnemyHexContext context,
		HextechCombatState combatState,
		IReadOnlyList<Creature> players,
		IReadOnlyList<Creature> enemies)
	{
		// 临时缓慢在玩家回合开始时清除，因此必须在敌方回合开始时施加。
		// 额外回合不推进 RoundNumber 且回合开始 hook 会重入，按回合防重。
		if (ShouldTriggerForRound(combatState.RoundNumber)
			&& players.Count > 0
			&& HextechCombatProcTracker.ConsumeGlobalProcInCombat(context.Tracking, $"round-once:{Kind}:{combatState.RoundNumber}") == 0)
		{
			await context.RunGroupedPlayerDebuffBurst(async () =>
			{
				await PowerCmd.Apply<HextechTemporarySlowPower>(players, TemporarySlowAmount, null, null);
			});
		}
	}

	internal static bool ShouldTriggerForRound(int roundNumber)
	{
		return roundNumber > 0 && roundNumber % TurnsNeeded == 0;
	}
}
