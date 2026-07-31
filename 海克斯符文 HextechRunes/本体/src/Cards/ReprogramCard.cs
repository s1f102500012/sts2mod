namespace HextechRunes;

public sealed class ReprogramCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.ReprogramCardPortraitPath;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FocusPower>(1m),
		new PowerVar<StrengthPower>(1m),
		new PowerVar<DexterityPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<FocusPower>(),
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>()
	];

	public ReprogramCard()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<FocusPower>(Owner.Creature, -DynamicVars["FocusPower"].BaseValue, Owner.Creature, this);
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars["DexterityPower"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars["FocusPower"].UpgradeValueBy(1m);
		DynamicVars["StrengthPower"].UpgradeValueBy(1m);
		DynamicVars["DexterityPower"].UpgradeValueBy(1m);
	}
}
