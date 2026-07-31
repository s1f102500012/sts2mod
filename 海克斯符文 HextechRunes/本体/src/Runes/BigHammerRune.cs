namespace HextechRunes;

public sealed class BigHammerRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ForgeBonusPercent", 50m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromForge();

	public override bool IsAvailableForPlayer(Player player) => IsRegentPlayer(player);

	internal decimal ApplyForgeBonus(decimal amount, bool sourceAlreadyIncludesBonus)
	{
		return CalculateForgeAmount(amount, DynamicVars["ForgeBonusPercent"].BaseValue, sourceAlreadyIncludesBonus);
	}

	internal static decimal CalculateForgeAmount(decimal amount, decimal bonusPercent, bool sourceAlreadyIncludesBonus)
	{
		return sourceAlreadyIncludesBonus
			? amount
			: amount * (1m + bonusPercent / 100m);
	}
}
