namespace HextechRunes;

public sealed class GoldenSpatulaRune : HextechRelicBase, IHextechSharedCombatVictoryRune, IHextechMaxHpScalingRune
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
			new DynamicVar("StackBonusPercent", 1m),
			new DynamicVar("StackOverloadThreshold", 10m)
		];

	public decimal SustainMultiplier => StackMultiplier;

	public decimal MaxHpScale => StackMultiplier;

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null && HextechMaxHpScaling.GetPrimary(Owner) is { } primary)
		{
			HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		}

		return Task.CompletedTask;
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
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
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? StackMultiplier : 1m;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? StackMultiplier : 1m;
	}

	private decimal StackMultiplier
	{
		get
		{
			if (_stacks <= 0)
			{
				return 1m;
			}

			return 1m + TotalBonusPercentFor(_stacks) / 100m;
		}
	}

	private decimal TotalBonusPercentFor(int stacks)
	{
		if (stacks <= 0)
		{
			return 0m;
		}

		decimal multiplier = stacks > DynamicVars["StackOverloadThreshold"].IntValue ? 3m : 1m;
		return stacks * DynamicVars["StackBonusPercent"].BaseValue * multiplier;
	}
}
