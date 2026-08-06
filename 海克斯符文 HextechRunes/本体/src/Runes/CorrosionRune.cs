namespace HextechRunes;

public sealed class CorrosionRune : HextechRelicBase
{
	internal const int TemporarySlowAmount = 6;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<HextechPlayerSlowPower>("SlowPower", TemporarySlowAmount)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechPlayerSlowPower>()
	];

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null
			|| target.Side != CombatSide.Enemy
			|| !target.IsAlive
			|| result.TotalDamage <= 0m
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		Flash([target]);
		await PowerCmd.Apply<HextechTemporarySlowPower>(
			target,
			DynamicVars["SlowPower"].BaseValue,
			Owner.Creature,
			cardSource);
	}
}
