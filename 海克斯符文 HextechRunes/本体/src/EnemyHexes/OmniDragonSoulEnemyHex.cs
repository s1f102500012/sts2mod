namespace HextechRunes;

internal sealed class OmniDragonSoulEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.OmniDragonSoul;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (combatState.RunState != context.RunState || players.Count == 0)
		{
			return;
		}

		int stacks = context.TierValue(Kind, 1, 2, 3);
		int roll = HextechStableRandom.Index(
			context.RunState,
			3,
			"enemy-omni-dragon-soul-debuff",
			combatState.RoundNumber.ToString());

		await context.RunGroupedPlayerDebuffBurst(async () =>
		{
			switch (roll)
			{
				case 0:
					await PowerCmd.Apply<WeakPower>(players, stacks, null, null);
					break;
				case 1:
					await PowerCmd.Apply<FrailPower>(players, stacks, null, null);
					break;
				default:
					await PowerCmd.Apply<VulnerablePower>(players, stacks, null, null);
					break;
			}
		});
	}
}
