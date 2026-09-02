using MegaCrit.Sts2.Core.Localization;

namespace HextechRunes;

public sealed class HextechBurnPower : HextechPowerBase
{
	private const decimal StackDecayPercent = 0.1m;
	private static readonly HextechScopedDepthGuard DamageResolutionGuard = new();

	internal static bool IsResolvingDamage => DamageResolutionGuard.IsActive;

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		if (Owner.Side == CombatSide.Player || side != Owner.Side)
		{
			return;
		}

		await ResolveBurn(new ThrowingPlayerChoiceContext(), blockable: false);
	}

	public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
	{
		if (Owner.Side != CombatSide.Player || side != Owner.Side)
		{
			return;
		}

		await ResolveBurn(choiceContext, blockable: true);
	}

	private async Task ResolveBurn(PlayerChoiceContext choiceContext, bool blockable)
	{
		if (Amount <= 0 || !Owner.IsAlive)
		{
			return;
		}

		int stacks = Amount;
		int percentHpLoss = Math.Max(1, (int)Math.Floor(Owner.CurrentHp * stacks / 100m));
		int hpLoss = Math.Max(stacks, percentHpLoss);
		int stackLoss = Math.Max(1, (int)Math.Ceiling(stacks * StackDecayPercent));
		Flash();
		await RunWithDamageResolutionGuard(async () =>
		{
			ValueProp valueProps = ValueProp.Unpowered;
			if (!blockable)
			{
				valueProps |= ValueProp.Unblockable;
			}

			await CreatureCmd.Damage(choiceContext, Owner, hpLoss, valueProps, null, null);
		});

		if (Owner.IsAlive)
		{
			await PowerCmd.Apply<HextechBurnPower>(Owner, -stackLoss, null, null);
		}
		else
		{
			await Cmd.CustomScaledWait(0.1f, 0.25f);
		}
	}

	internal static Task RunWithDamageResolutionGuard(Func<Task> action)
	{
		return DamageResolutionGuard.RunAsync(action);
	}
}

public sealed class HextechTemporaryStrengthPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<MasterOfDualityRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class HextechTemporaryDexterityPower : TemporaryDexterityPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<MasterOfDualityRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class HextechTemporaryStrengthLossPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<MasterOfDualityRune>();

	protected override bool IsVisibleInternal => false;

	protected override bool IsPositive => false;
}

public sealed class HextechTemporaryDexterityLossPower : TemporaryDexterityPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<MasterOfDualityRune>();

	protected override bool IsVisibleInternal => false;

	protected override bool IsPositive => false;
}

public sealed class HextechLethalTempoTemporaryStrengthPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<LethalTempoRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class HextechBloodPactTemporaryStrengthPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<BloodPactRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class HextechPowerShieldTemporaryStrengthPower : TemporaryStrengthPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<PowerShieldRune>();

	protected override bool IsVisibleInternal => false;
}

public sealed class HextechAttackReplayPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (!ShouldReplay(card))
		{
			return playCount;
		}

		return playCount + Amount;
	}

	public override async Task AfterModifyingCardPlayCount(CardModel card)
	{
		if (!ShouldReplay(card))
		{
			return;
		}

		Flash();
		await PowerCmd.Remove(this);
	}

	private bool ShouldReplay(CardModel card)
	{
		return Amount > 0m
			&& card.Owner?.Creature == Owner
			&& IllusoryWeaponRune.IsAttackForEffects(card, card.Owner);
	}
}

public sealed class HextechPlayerSlowPower : HextechPowerBase
{
	public override PowerType Type => PowerType.None;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override bool AllowNegative => true;

	public override int DisplayAmount => (int)decimal.Round(Amount, 0, MidpointRounding.AwayFromZero);

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target != Owner || Amount == 0m || (props & ValueProp.Unpowered) != 0)
		{
			return 1m;
		}

		return ResolveDamageMultiplier(Amount);
	}

	internal static decimal ResolveDamageMultiplier(decimal amount)
	{
		return Math.Max(0m, 1m + amount / 100m);
	}

	public override Task AfterModifyingDamageAmount(CardModel? cardSource)
	{
		if (Amount != 0m)
		{
			Flash();
		}

		return Task.CompletedTask;
	}
}

public sealed class HextechTemporarySlowPower : HextechPowerBase, ITemporaryPower
{
	private bool _shouldIgnoreNextInstance;

	public override PowerType Type => PowerType.None;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override bool AllowNegative => true;

	protected override bool IsVisibleInternal => false;

	public AbstractModel OriginModel => ModelDb.Power<HextechPlayerSlowPower>();

	public PowerModel InternallyAppliedPower => ModelDb.Power<HextechPlayerSlowPower>();

	public override LocString Title => ModelDb.Power<HextechPlayerSlowPower>().Title;

	public override LocString Description => ModelDb.Power<HextechPlayerSlowPower>().Description;

	public void IgnoreNextInstance()
	{
		_shouldIgnoreNextInstance = true;
	}

	public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_shouldIgnoreNextInstance)
		{
			_shouldIgnoreNextInstance = false;
			return;
		}

		await PowerCmd.Apply<HextechPlayerSlowPower>(target, amount, applier, cardSource, silent: true);
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power != this || amount == Amount)
		{
			return;
		}

		if (_shouldIgnoreNextInstance)
		{
			_shouldIgnoreNextInstance = false;
			return;
		}

		await PowerCmd.Apply<HextechPlayerSlowPower>(Owner, amount, applier, cardSource, silent: true);
	}

	public override async Task AfterSideTurnStart(CombatSide side, HextechCombatState combatState)
	{
		if (!ShouldExpireAtSide(side))
		{
			return;
		}

		await PowerCmd.Remove(this);
		await PowerCmd.Apply<HextechPlayerSlowPower>(Owner, -Amount, Owner, null, silent: true);
	}

	internal static bool ShouldExpireAtSide(CombatSide side)
	{
		return side == CombatSide.Player;
	}
}
