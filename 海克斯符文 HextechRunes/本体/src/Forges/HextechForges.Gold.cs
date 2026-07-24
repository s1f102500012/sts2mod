namespace HextechRunes;

public sealed class ConstitutionForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m),
		new PowerVar<DexterityPower>(1m)
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, Stacked(DynamicVars.Strength.BaseValue), Owner.Creature, null);
		await PowerCmd.Apply<DexterityPower>(Owner.Creature, Stacked(DynamicVars.Dexterity.BaseValue), Owner.Creature, null);
	}
}

public sealed class DisasterForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<WeakPower>(1m),
		new PowerVar<VulnerablePower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>()
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead || Owner.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies
			.Where(static enemy => enemy.IsAlive)
			.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		await PowerCmd.Apply<WeakPower>(enemies, Stacked(DynamicVars.Weak.BaseValue), Owner.Creature, null);
		await PowerCmd.Apply<VulnerablePower>(enemies, Stacked(DynamicVars.Vulnerable.BaseValue), Owner.Creature, null);
	}
}

public sealed class GoldLifeForge : HextechForgeBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new MaxHpVar(20m)
	];

	public override Task AfterObtained()
	{
		if (Owner == null)
		{
			return Task.CompletedTask;
		}

		Flash();
		return CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars.MaxHp.BaseValue);
	}
}

// 血量锻造器:百分比版最大生命(棱彩"生命锻造器"30% 的 1/2),与固定值的生命锻造器并存。
public sealed class GoldHpForge : HextechForgeBase, IHextechPercentHpForge
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
		new DynamicVar("MaxHpPercent", 15m)
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

public sealed class GoldAttackForge : HextechForgeBase, IHextechDamageCoefficientForge
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", 1.1m)
	];

	public decimal DamageBonusFractionTotal => StackedMultiplier(DynamicVars["DamageMultiplier"].BaseValue) - 1m;

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return Owner != null && IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource)
			? HextechForgeCoefficientHelper.GetDamageMultiplier(Owner, this)
			: 1m;
	}
}

public sealed class GoldProtectionForge : HextechForgeBase, IHextechSustainCoefficientForge
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("SustainMultiplier", 1.1m)
	];

	public decimal SustainBonusFractionTotal => StackedMultiplier(DynamicVars["SustainMultiplier"].BaseValue) - 1m;

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return Owner != null && target == Owner.Creature
			? HextechForgeCoefficientHelper.GetSustainMultiplier(Owner, this)
			: 1m;
	}
}

public sealed class GoldFocusForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<FocusPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<FocusPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead || !IsDefectOwner)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<FocusPower>(Owner.Creature, Stacked(DynamicVars["FocusPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class DrawForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		return player == Owner ? count + Stacked(DynamicVars.Cards.BaseValue) : count;
	}
}

public sealed class RecoveryForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(1m)
	];

	public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (Owner == null || player != Owner || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash([Owner.Creature]);
		return CreatureCmd.Heal(Owner.Creature, Stacked(DynamicVars.Heal.BaseValue));
	}
}

public sealed class HourglassForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DamageVar(5m, ValueProp.Unpowered)
	];

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (Owner == null || player != Owner || Owner.Creature.IsDead || Owner.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies
			.Where(static enemy => enemy.IsAlive)
			.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		foreach (Creature enemy in enemies)
		{
			await HextechGameApiCompat.Damage(choiceContext, enemy, Stacked(DynamicVars.Damage.BaseValue), ValueProp.Unpowered, Owner.Creature, null);
		}
	}
}

public sealed class GoldUpgradeForge : HextechForgeBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(4)
	];

	public override Task AfterObtained()
	{
		if (Owner == null)
		{
			return Task.CompletedTask;
		}

			List<CardModel> cards = HextechStableRandom.PickDistinct(
				Owner.Deck.Cards
				.Where(static card => card != null && card.IsUpgradable)
				.ToList(),
				DynamicVars.Cards.IntValue,
				(RunState)Owner.RunState,
				HextechStableRandom.CardKey,
				"gold-upgrade-forge",
				HextechStableRandom.PlayerKey(Owner),
				Owner.Deck.Cards.Count.ToString());
		if (cards.Count == 0)
		{
			return Task.CompletedTask;
		}

		Flash();
		foreach (CardModel card in cards)
		{
			CardCmd.Upgrade(card);
		}

		return Task.CompletedTask;
	}
}

public sealed class SummonForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new SummonVar(2m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| !IsNecrobinderPlayer(player))
		{
			return;
		}

		Flash();
		await OstyCmd.Summon(choiceContext, player, Stacked(DynamicVars.Summon.BaseValue), this);
	}
}

public sealed class FleshForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<SleightOfFleshPower>(4m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<SleightOfFleshPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<SleightOfFleshPower>(Owner.Creature, Stacked(DynamicVars["SleightOfFleshPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class StarsForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new StarsVar(1)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsRegentPlayer(player);
	}

	public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (Owner == null || player != Owner || Owner.Creature.IsDead || !IsRegentOwner)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PlayerCmd.GainStars(Stacked(DynamicVars.Stars.BaseValue), Owner);
	}
}

public sealed class OrbSlotForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbSlots", 2m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		if (Owner == null || side != Owner.Creature.Side || combatState.RoundNumber > 1 || !IsDefectOwner)
		{
			return;
		}

		Flash();
		await OrbCmd.AddSlots(Owner, Math.Max(0, FloorToInt(Stacked(DynamicVars["OrbSlots"].BaseValue))));
	}
}

public sealed class VenomForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<EnvenomPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<EnvenomPower>(Owner.Creature, Stacked(DynamicVars["EnvenomPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class ShrinkForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ShrinkPower>(2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ShrinkPower>()
	];

	public override async Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead || Owner.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies
			.Where(static enemy => enemy.IsAlive)
			.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		await PowerCmd.Apply<ShrinkPower>(enemies, Stacked(DynamicVars["ShrinkPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class PlatingForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<PlatingPower>(6m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PlatingPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<PlatingPower>(Owner.Creature, Stacked(DynamicVars["PlatingPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class ThornsForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ThornsPower>(4m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<ThornsPower>()
	];

	public override Task BeforeCombatStart()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<ThornsPower>(Owner.Creature, Stacked(DynamicVars["ThornsPower"].BaseValue), Owner.Creature, null);
	}
}

public sealed class ArtifactForge : HextechForgeBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<ArtifactPower>(1m)
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
