namespace HextechRunes;

public sealed class EarthAwakensRune : HextechRelicBase
{
	private bool _initialPowerAppliedThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedInitialPowerAppliedThisCombat
	{
		get => _initialPowerAppliedThisCombat;
		set => _initialPowerAppliedThisCombat = value;
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<RollingBoulderPower>(5m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RollingBoulderPower>()
	];

	public override Task BeforeCombatStart()
	{
		_initialPowerAppliedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_initialPowerAppliedThisCombat = false;
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead || _initialPowerAppliedThisCombat)
		{
			return Task.CompletedTask;
		}

		_initialPowerAppliedThisCombat = true;
		return ApplyRollingBoulderPower();
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		_initialPowerAppliedThisCombat = true;
		await ApplyRollingBoulderPower();
	}

	private async Task ApplyRollingBoulderPower()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<RollingBoulderPower>(Owner.Creature, DynamicVars["RollingBoulderPower"].BaseValue, Owner.Creature, null);
	}
}
