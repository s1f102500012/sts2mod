namespace HextechRunes;

internal sealed class ProteinShakeEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ProteinShake;

	internal override int EnemyHealOrder => 30;

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return HextechMonsterSustainHelper.GetProteinShakeSustainMultiplier(target, context.ScalingPlayerCount);
	}

	internal override decimal ModifyEnemyHealAmount(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return amount * HextechMonsterSustainHelper.GetProteinShakeSustainMultiplier(creature, context.ScalingPlayerCount);
	}
}
