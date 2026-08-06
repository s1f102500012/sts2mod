namespace HextechRunes;

internal sealed class StatsOnStatsOnStatsEnemyHex : HextechEnemyHexEffect, IHextechEnemyMaxHpCoefficientProvider
{
	internal override MonsterHexKind Kind => MonsterHexKind.StatsOnStatsOnStats;

	internal override int PersistentOrder => 35;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => EnemyAttributeBoostValues.GetMultiplier(Kind, context);

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => EnemyAttributeBoostValues.GetMultiplier(Kind, context);

	internal override decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount) => EnemyAttributeBoostValues.GetMultiplier(Kind, context);

	internal override Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers) => EnemyAttributeBoostValues.ApplyPersistent(Kind, context, creature, maxHpBaseOverride, replayOneShotPowers);

	public decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature) => EnemyAttributeBoostValues.GetBonusFraction(Kind, context);
}
