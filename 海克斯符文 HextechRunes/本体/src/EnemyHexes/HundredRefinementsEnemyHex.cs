namespace HextechRunes;

internal sealed class HundredRefinementsEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.HundredRefinements;

	internal override async Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		if (!enemy.IsAlive)
		{
			return;
		}

		await HextechPlayerSlowPower.ApplyAtZero(enemy, enemy, null);
	}

	internal override Task BeforePlayerSideTurnStart(
		HextechEnemyHexContext context,
		HextechCombatState combatState,
		IReadOnlyList<Creature> players)
	{
		HextechPlayerSlowPower.ResetEnemyHexSlowForRound(combatState.Enemies);
		return Task.CompletedTask;
	}

	internal override Task AfterEnemyDamageReceivedAny(
		HextechEnemyHexContext context,
		Creature target,
		DamageResult result,
		Creature? dealer,
		CardModel? cardSource)
	{
		HextechPlayerSlowPower? slow = target.GetPower<HextechPlayerSlowPower>();
		if (!target.IsAlive
			|| target.Side != CombatSide.Enemy
			|| target.CombatState?.RunState != context.RunState
			|| result.UnblockedDamage <= 0m
			|| slow == null)
		{
			return Task.CompletedTask;
		}

		slow.NormalizeEnemyReductionAmount();
		return HextechPowerCmdCompat.Apply<HextechPlayerSlowPower>(
			target,
			ResolveSlowReduction(context.GetStrengthTier(Kind)),
			dealer,
			cardSource,
			silent: true);
	}

	internal static int ResolveSlowReduction(int strengthTier)
	{
		return strengthTier switch
		{
			<= 1 => 3,
			2 => 5,
			_ => 8
		};
	}
}
