namespace HextechRunes;

internal sealed class HundredRefinementsEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.HundredRefinements;

	internal override Task AfterEnemyDamageReceivedAny(
		HextechEnemyHexContext context,
		Creature target,
		DamageResult result,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (!target.IsAlive
			|| target.Side != CombatSide.Enemy
			|| target.CombatState?.RunState != context.RunState
			|| result.UnblockedDamage <= 0m)
		{
			return Task.CompletedTask;
		}

		return HextechPowerCmdCompat.Apply<HextechTemporarySlowPower>(
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
			<= 1 => -3,
			2 => -5,
			_ => -8
		};
	}
}
