namespace HextechRunes;

internal sealed class AstralBodyEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.AstralBody;

	internal override int PersistentOrder => 20;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 0.9m;
	}

	internal override async Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		if (HextechCombatProcTracker.TryMarkPersistentHexApplied(context.Tracking.AstralBodyApplied, creature, replayOneShotPowers))
		{
			await context.Modifier.ReapplyMonsterMaxHpCoefficients(creature, maxHpBaseOverride);
		}
	}

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature)
	{
		return context.TierValue(Kind, 0.20m, 0.30m, 0.40m);
	}
}
