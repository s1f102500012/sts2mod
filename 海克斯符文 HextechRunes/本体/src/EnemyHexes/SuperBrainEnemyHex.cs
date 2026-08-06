namespace HextechRunes;

internal sealed class SuperBrainEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SuperBrain;

	internal override async Task ApplyOpeningCombatStartToEnemy(
		HextechEnemyHexContext context,
		Creature creature,
		CombatRoom room,
		bool replayOneShotPowers)
	{
		if (!HextechCombatProcTracker.TryMarkPersistentHexApplied(
			context.Tracking.SuperBrainApplied,
			creature,
			replayOneShotPowers))
		{
			return;
		}

		decimal platingPercent = context.TierValue(Kind, 0.03m, 0.04m, 0.05m);
		int plating = (int)Math.Floor(creature.MaxHp * platingPercent);
		if (plating > 0)
		{
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(creature, plating, creature, null);
		}
	}
}
