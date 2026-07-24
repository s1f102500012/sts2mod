namespace HextechRunes;

public sealed class TankEngineRune : HextechRelicBase, IHextechSharedCombatVictoryRune, IHextechMaxHpScalingRune
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
		new DynamicVar("HpGainPercent", 0.06m),
		new DynamicVar("ScalePercent", 6m)
	];

	public decimal MaxHpScale => 1m + _stacks * DynamicVars["HpGainPercent"].BaseValue;

	internal float BodyScaleDelta => _stacks * (float)(DynamicVars["ScalePercent"].BaseValue / 100m);

	public override Task AfterObtained()
	{
		Grow();
		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null && HextechMaxHpScaling.GetPrimary(Owner) is { } primary)
		{
			HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		}

		Grow();
		return Task.CompletedTask;
	}

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return;
		}

		await ApplySharedCombatVictory(room);
	}

	public async Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		IHextechMaxHpBaseHolder primary = HextechMaxHpScaling.GetPrimary(Owner) ?? this;
		HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		SavedStacks++;
		Flash(Array.Empty<Creature>());
		await HextechMaxHpScaling.ReapplyScale(Owner);
		Grow();
	}

	private void Grow()
	{
		HextechPlayerBodyScaleHelper.Update(Owner);
	}
}
