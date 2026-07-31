namespace HextechRunes;

internal sealed class DivineInterventionEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.DivineIntervention;

	internal override Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (!context.TryConsumeRoundInterval(Kind, combatState, everyNRounds: 3))
		{
			return Task.CompletedTask;
		}

		IReadOnlyList<Creature> aliveEnemies = context.GetAliveEnemies(combatState);
		return aliveEnemies.Count > 0
			? PowerCmd.Apply<IntangiblePower>(aliveEnemies, 1m, null, null)
			: Task.CompletedTask;
	}
}
