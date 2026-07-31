namespace HextechRunes;

internal static class HextechEnemyHealModifier
{
	public static decimal Modify(HextechMayhemModifier modifier, Creature creature, decimal amount)
	{
		if (creature.Side != CombatSide.Enemy)
		{
			return amount;
		}

		HextechEnemyHexContext context = new(modifier);
		HextechEnemyHexEffect[] activeEffects = HextechEnemyHexEffects.GetActive(modifier).ToArray();
		decimal multiplier = HextechEnemyCoefficientHelper.CombineMultipliersByHex(
			activeEffects.Select(effect => (
				effect.Kind,
				effect.ModifyEnemyHealMultiplicative(context, creature, amount))));
		amount *= multiplier;
		foreach (HextechEnemyHexEffect effect in activeEffects.OrderBy(static effect => effect.EnemyHealOrder))
		{
			amount = effect.ModifyEnemyHealAmount(context, creature, amount);
		}

		return amount;
	}
}
