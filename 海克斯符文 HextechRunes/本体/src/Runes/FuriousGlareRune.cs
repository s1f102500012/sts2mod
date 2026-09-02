namespace HextechRunes;

public sealed class FuriousGlareRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<VulnerablePower>(1m),
		new PowerVar<StrengthPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<StrengthPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| power is not VulnerablePower
			|| power.Owner.Side != CombatSide.Enemy
			|| applier != Owner.Creature
			|| amount <= 0m)
		{
			return Task.CompletedTask;
		}

		int strength = Math.Max(0, FloorToInt(amount * DynamicVars.Strength.BaseValue));
		if (strength <= 0)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<StrengthPower>(Owner.Creature, strength, Owner.Creature, cardSource);
	}
}
