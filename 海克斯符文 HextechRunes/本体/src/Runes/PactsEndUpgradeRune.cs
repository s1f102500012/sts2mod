namespace HextechRunes;

public sealed class PactsEndUpgradeRune : CardUpgradeRuneBase<PactsEnd>
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePerExhaust", 6m)
	];

	protected override bool IsAvailableForCharacter(Player player) => IsIroncladPlayer(player);

	public override decimal ModifyDamageAdditiveCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner?.PlayerCombatState == null
			|| cardSource is not PactsEnd
			|| !IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource))
		{
			return 0m;
		}

		int exhaustCount = PileType.Exhaust.GetPile(Owner).Cards.Count;
		return CalculateBonusDamage(exhaustCount, DynamicVars["DamagePerExhaust"].BaseValue);
	}

	internal static decimal CalculateBonusDamage(int exhaustCount, decimal damagePerCard)
	{
		return Math.Max(0, exhaustCount) * damagePerCard;
	}
}
