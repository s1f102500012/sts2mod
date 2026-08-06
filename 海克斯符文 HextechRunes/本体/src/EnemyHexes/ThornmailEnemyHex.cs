namespace HextechRunes;

internal sealed class ThornmailEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Thornmail;

	internal override async Task ApplyOpeningCombatStartToEnemy(
		HextechEnemyHexContext context,
		Creature creature,
		CombatRoom room,
		bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(
			context.Tracking.ThornmailApplied,
			creature,
			replayOneShotPowers))
		{
			await HextechEnemyPowerScalingHooks.Apply<ThornsPower>(creature, context.TierValue(Kind, 0, 1, 2), creature, null);
		}
	}
}
