namespace HextechRunes;

internal sealed class FirstAidKitEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.FirstAidKit;

	internal override int EnemyHealOrder => 20;

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return 1.25m;
	}

	internal override decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return 1.25m;
	}
}
