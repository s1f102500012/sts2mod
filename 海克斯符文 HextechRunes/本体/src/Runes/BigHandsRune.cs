namespace HextechRunes;

public sealed class BigHandsRune : HextechRelicBase
{
	internal const decimal SummonMultiplier = 1.5m;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Multiplier", SummonMultiplier)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override decimal ModifySummonAmount(Player summoner, decimal amount, AbstractModel? source)
	{
		return summoner == Owner ? CalculateSummonAmount(amount) : amount;
	}

	internal static decimal CalculateSummonAmount(decimal amount)
	{
		return amount * SummonMultiplier;
	}
}
