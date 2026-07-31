namespace HextechRunes;

internal sealed class LagavulinMatriarchEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.LagavulinMatriarch;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (combatState.RunState != context.RunState || players.Count == 0)
		{
			return;
		}

		if (!context.TryConsumeRoundInterval(
			Kind,
			combatState,
			context.TierValue(Kind, 3, 2, 1)))
		{
			return;
		}

		await context.RunGroupedPlayerDebuffBurst(async () =>
		{
			await PowerCmd.Apply<StrengthPower>(players, -1, null, null);
			await PowerCmd.Apply<DexterityPower>(players, -1, null, null);
		});
	}
}
