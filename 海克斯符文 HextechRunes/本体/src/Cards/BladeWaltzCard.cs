namespace HextechRunes;

public sealed class BladeWaltzCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.BladeWaltzCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords =>
	[
		CardKeyword.Exhaust
	];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(3m, ValueProp.Move),
		new DynamicVar("Hits", 9m),
		new PowerVar<IntangiblePower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<IntangiblePower>()
	];

	public BladeWaltzCard()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		HextechCombatState combatState = Owner.Creature.CombatState
			?? throw new InvalidOperationException("Blade Waltz played outside combat.");
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.WithHitCount(DynamicVars["Hits"].IntValue)
			.FromCardCompat(this)
			.TargetingRandomOpponents(combatState, allowDuplicates: true)
			.Execute(choiceContext);

		await PowerCmd.Apply<IntangiblePower>(Owner.Creature, DynamicVars["IntangiblePower"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		_ = Keywords;
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
