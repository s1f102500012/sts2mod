namespace HextechRunes;

internal sealed class MiserableFateEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.MiserableFate;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		int missingHpPerBlock = context.TierValue(Kind, 4, 3, 2);
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			int block = ResolveBlock(enemy.MaxHp, enemy.CurrentHp, missingHpPerBlock);
			if (block > 0)
			{
				await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null);
			}
		}
	}

	internal static int ResolveBlock(int maxHp, int currentHp, int missingHpPerBlock)
	{
		long missingHp = Math.Max(0L, (long)maxHp - currentHp);
		return (int)Math.Min(int.MaxValue, missingHp / Math.Max(1, missingHpPerBlock));
	}
}
