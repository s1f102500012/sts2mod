namespace HextechRunes;

internal sealed class GoldenSpatulaEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.GoldenSpatula;

	internal override int PersistentOrder => 30;

	internal override int EnemyHealOrder => 40;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m + context.TierValue(Kind, 0.25m, 0.30m, 0.45m);
	}

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return 0.5m;
	}

	internal override decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return 0.5m;
	}

	internal override async Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(context.Tracking.GoldenSpatulaApplied, creature, replayOneShotPowers))
		{
			await context.Modifier.ReapplyMonsterMaxHpCoefficients(creature, maxHpBaseOverride);
		}
	}

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature)
	{
		return context.TierValue(Kind, 0.25m, 0.30m, 0.45m);
	}
}
