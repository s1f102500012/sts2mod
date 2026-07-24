namespace HextechRunes;

public sealed class NineDragonPowerRune : HextechRelicBase, IHextechMaxHpScalingRune
{
	private int _baseMaxHp;
	private int _stacks;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(0, value);
	}

	public int BaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(1, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStacks
	{
		get => _stacks;
		set
		{
			_stacks = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => true;

	public override int DisplayAmount => !IsCanonical ? _stacks : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<RegenPower>(1m),
		new DynamicVar("StackBonusPercent", 3m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RegenPower>()
	];

	public decimal SustainMultiplier => 1m + _stacks * DynamicVars["StackBonusPercent"].BaseValue / 100m;

	public decimal MaxHpScale => SustainMultiplier;

	internal float BodyScaleDelta => _stacks * (float)(DynamicVars["StackBonusPercent"].BaseValue / 100m);

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null && HextechMaxHpScaling.GetPrimary(Owner) is { } primary)
		{
			HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		}

		Grow();
		return Task.CompletedTask;
	}

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead || _stacks <= 0)
		{
			return Task.CompletedTask;
		}

		return PowerCmd.Apply<RegenPower>(Owner.Creature, _stacks * DynamicVars["RegenPower"].BaseValue, Owner.Creature, null);
	}

	public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
	{
		if (Owner == null || Owner.Creature.IsDead || !IsPotionUseOwnedByOrTargetingOwner(potion, target))
		{
			return;
		}

		IHextechMaxHpBaseHolder primary = HextechMaxHpScaling.GetPrimary(Owner) ?? this;
		HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		SavedStacks++;
		Flash();
		await HextechMaxHpScaling.ReapplyScale(Owner);
		Grow();
	}

	private void Grow()
	{
		HextechPlayerBodyScaleHelper.Update(Owner);
	}
}
