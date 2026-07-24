namespace HextechRunes;

internal sealed class FossilStalkerEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.FossilStalker;

	internal override Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		if (!enemy.IsAlive)
		{
			return Task.CompletedTask;
		}

		return PowerCmd.Apply<SuckPower>(enemy, ResolveSuckAmount(context.GetStrengthTier(Kind)), enemy, null);
	}

	internal static int ResolveSuckAmount(int strengthTier)
	{
		return strengthTier switch
		{
			<= 1 => 1,
			2 => 2,
			_ => 3
		};
	}
}
