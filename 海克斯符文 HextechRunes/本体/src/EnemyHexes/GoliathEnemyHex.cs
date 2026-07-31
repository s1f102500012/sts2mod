namespace HextechRunes;

internal sealed class GoliathEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.Goliath;

	internal override int PersistentOrder => 10;

	internal override int EnemyHealOrder => 10;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return SustainMultiplier(context);
	}

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return SustainMultiplier(context);
	}

	internal override decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return SustainMultiplier(context);
	}

	internal override async Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(context.Tracking.GoliathApplied, creature, replayOneShotPowers))
		{
			await context.Modifier.ReapplyMonsterMaxHpCoefficients(creature, maxHpBaseOverride);
			context.UpdateEnemyScale(creature);
		}
	}

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature)
	{
		return context.TierValue(Kind, 0.20m, 0.30m, 0.40m);
	}

	private decimal SustainMultiplier(HextechEnemyHexContext context)
	{
		return 1m + context.TierValue(Kind, 0.15m, 0.20m, 0.25m);
	}
}
