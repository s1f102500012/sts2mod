namespace HextechRunes;

internal interface IHextechEnemyMaxHpCoefficientProvider
{
	decimal GetMaxHpBonusFraction(HextechEnemyHexContext context, Creature creature);
}

internal static class HextechEnemyCoefficientHelper
{
	public static decimal CombineMultipliersByHex(
		IEnumerable<(MonsterHexKind Kind, decimal Multiplier)> contributions)
	{
		return CombineBonusFractionsByHex(
			contributions.Select(static contribution =>
				(contribution.Kind, contribution.Multiplier - 1m)));
	}

	public static decimal CombineBonusFractionsByHex(
		IEnumerable<(MonsterHexKind Kind, decimal BonusFraction)> contributions)
	{
		return contributions
			.GroupBy(static contribution => contribution.Kind)
			.Select(static group => 1m + group.Sum(static contribution => contribution.BonusFraction))
			.Aggregate(1m, static (product, multiplier) => product * multiplier);
	}
}
