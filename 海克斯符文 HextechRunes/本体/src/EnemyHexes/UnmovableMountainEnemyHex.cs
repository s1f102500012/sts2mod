namespace HextechRunes;

internal sealed class UnmovableMountainEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.UnmovableMountain;

	internal override async Task ApplyOpeningCombatStartToEnemy(
		HextechEnemyHexContext context,
		Creature creature,
		CombatRoom room,
		bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(
			context.Tracking.UnmovableMountainApplied,
			creature,
			replayOneShotPowers))
		{
			await PowerCmd.Apply<BarricadePower>(creature, 1m, creature, null);
		}
	}

	internal override async Task BeforeEnemySideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players, IReadOnlyList<Creature> enemies)
	{
		foreach (Creature enemy in enemies)
		{
			decimal blockPercent = context.TierValue(Kind, 0.06m, 0.08m, 0.10m);
			int block = Math.Max(1, (int)Math.Floor(enemy.MaxHp * blockPercent));
			await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null);
		}
	}
}
