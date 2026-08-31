namespace HextechRunes;

internal sealed class GlassCannonEnemyHex : HextechEnemyHexEffect
{
	internal const decimal HealCapPercent = 0.7m;

	internal override MonsterHexKind Kind => MonsterHexKind.GlassCannon;

	internal override int PersistentOrder => 90;

	internal override int EnemyHealOrder => 50;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m + context.TierValue(Kind, 0.30m, 0.40m, 0.50m);
	}

	internal override decimal ModifyEnemyHealAmount(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		int healCap = (int)Math.Floor(creature.MaxHp * HealCapPercent);
		return Math.Min(amount, Math.Max(0, healCap - creature.CurrentHp));
	}

	internal override Task ApplyPersistentToEnemy(HextechEnemyHexContext context, Creature creature, int? maxHpBaseOverride, bool replayOneShotPowers)
	{
		int hpCap = Math.Max(1, (int)Math.Floor(creature.MaxHp * HealCapPercent));
		return creature.CurrentHp > hpCap ? CreatureCmd.SetCurrentHp(creature, hpCap) : Task.CompletedTask;
	}
}
