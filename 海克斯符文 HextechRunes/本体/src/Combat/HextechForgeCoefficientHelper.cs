namespace HextechRunes;

internal interface IHextechDamageCoefficientForge
{
	decimal DamageBonusFractionTotal { get; }
}

internal interface IHextechSustainCoefficientForge
{
	decimal SustainBonusFractionTotal { get; }
}

internal static class HextechForgeCoefficientHelper
{
	public static decimal GetDamageMultiplier(Player player, IHextechDamageCoefficientForge source)
	{
		return GetGroupedMultiplier(
			player,
			source,
			static forge => forge.DamageBonusFractionTotal);
	}

	public static decimal GetSustainMultiplier(Player player)
	{
		return CombineBonusFractions(
			player.Relics
				.OfType<IHextechSustainCoefficientForge>()
				.Select(static forge => forge.SustainBonusFractionTotal));
	}

	public static decimal GetSustainMultiplier(Player player, IHextechSustainCoefficientForge source)
	{
		return GetGroupedMultiplier(
			player,
			source,
			static forge => forge.SustainBonusFractionTotal);
	}

	internal static decimal CombineBonusFractions(IEnumerable<decimal> bonusFractions)
	{
		return 1m + bonusFractions.Sum();
	}

	private static decimal GetGroupedMultiplier<TForge>(
		Player player,
		TForge source,
		Func<TForge, decimal> getBonusFraction)
		where TForge : class
	{
		List<TForge> forges = player.Relics.OfType<TForge>().ToList();
		if (forges.Count == 0 || !ReferenceEquals(forges[0], source))
		{
			return 1m;
		}

		return CombineBonusFractions(forges.Select(getBonusFraction));
	}
}
