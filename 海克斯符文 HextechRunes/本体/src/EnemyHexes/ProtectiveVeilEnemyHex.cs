namespace HextechRunes;

internal sealed class ProtectiveVeilEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ProtectiveVeil;

	internal override async Task ApplyOpeningCombatStartToEnemy(
		HextechEnemyHexContext context,
		Creature creature,
		CombatRoom room,
		bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(
			context.Tracking.ProtectiveVeilApplied,
			creature,
			replayOneShotPowers))
		{
			await HextechEnemyPowerScalingHooks.Apply<ArtifactPower>(creature, context.TierValue(Kind, 1, 2, 3), creature, null);
		}
	}
}
