namespace HextechRunes;

public sealed class OblivionUpgradeRune : CardUpgradeRuneBase<Oblivion>
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Oblivion>(),
		HoverTipFactory.FromPower<OblivionPower>()
	];

	protected override bool IsAvailableForCharacter(Player player) => IsNecrobinderPlayer(player);
}
