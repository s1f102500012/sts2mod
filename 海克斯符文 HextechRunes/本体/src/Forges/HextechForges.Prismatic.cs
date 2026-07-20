namespace HextechRunes;

public sealed class PrismaticLifeForge : HextechForgeBase, IHextechPercentHpForge
{
	private int _baseMaxHp;

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

	public decimal MaxHpPercentTotal => DynamicVars["MaxHpPercent"].BaseValue * StackAmount;

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MaxHpPercent", 30m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		await HextechMaxHpScaling.ReapplyScale(Owner);
	}
}

public sealed class AttackForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", 1.2m)
	];

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? StackedMultiplier(DynamicVars["DamageMultiplier"].BaseValue) : 1m;
	}
}

public sealed class ProtectionForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("SustainMultiplier", 1.2m)
	];

	public decimal SustainMultiplier => StackedMultiplier(DynamicVars["SustainMultiplier"].BaseValue);

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? SustainMultiplier : 1m;
	}
}

public sealed class EnergyForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new EnergyVar(1)
	];

	public override Task AfterEnergyResetLate(Player player)
	{
		if (Owner == null || player != Owner || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PlayerCmd.GainEnergy(Stacked(DynamicVars.Energy.BaseValue), Owner);
	}
}

public sealed class RitualForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<RitualPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RitualPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<RitualPower>(Owner.Creature, Stacked(DynamicVars["RitualPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class RegenForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<RegenPower>(4m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<RegenPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<RegenPower>(Owner.Creature, Stacked(DynamicVars["RegenPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class BufferForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<BufferPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<BufferPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<BufferPower>(Owner.Creature, Stacked(DynamicVars["BufferPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class SlipperyForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<SlipperyPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<SlipperyPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<SlipperyPower>(Owner.Creature, Stacked(DynamicVars["SlipperyPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class PrismaticArtifactForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ArtifactPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ArtifactPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<ArtifactPower>(Owner.Creature, Stacked(DynamicVars["ArtifactPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class FortuneForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new GoldVar(100)
	];

	internal int ExtraGoldRewardAmount => FloorToInt(Stacked(DynamicVars.Gold.BaseValue));

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner == null)
		{
			return Task.CompletedTask;
		}

		Flash();
		HextechGoldRewardHelper.AddFixedExtraGoldReward(
			room,
			Owner,
			ExtraGoldRewardAmount);
		return Task.CompletedTask;
	}
}

public sealed class VoidForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<VoidFormPower>(1m)
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<VoidFormPower>(Owner.Creature, Stacked(DynamicVars["VoidFormPower"].BaseValue), Owner.Creature, null);
	}
}
