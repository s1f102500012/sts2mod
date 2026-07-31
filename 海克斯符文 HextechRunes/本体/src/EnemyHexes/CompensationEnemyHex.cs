namespace HextechRunes;

internal sealed class CompensationEnemyHex : HextechEnemyHexEffect
{
	private static CompensationEnemyHex? _effectWithPendingCompensation;

	private readonly List<PendingCompensation> _pendingCompensations = [];

	internal override MonsterHexKind Kind => MonsterHexKind.Compensation;

	internal override void ResetRunScopedState()
	{
		ClearPendingCompensationsForEffect();
	}

	internal override Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		ClearPendingCompensationsForEffect();
		return Task.CompletedTask;
	}

	internal override Task BeforeSideTurnStart(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		ClearPendingCompensationsForEffect();
		return Task.CompletedTask;
	}

	internal override Task AfterCombatVictory(HextechEnemyHexContext context, CombatRoom room)
	{
		ClearPendingCompensationsForEffect();
		return Task.CompletedTask;
	}

	internal override decimal ModifyHpLostAfterOsty(HextechEnemyHexContext context, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target.Side != CombatSide.Enemy
			|| target.CombatState?.RunState != context.RunState
			|| target.IsDead
			|| ShouldSkipDamageReplacement()
			|| amount <= 0m)
		{
			return amount;
		}

		long commandId = HextechCombatHooks.CurrentActualDamageCommandId;
		if (commandId == 0L)
		{
			return amount;
		}

		(decimal immediateDamage, int nextTurnDamage) = SplitDamage(amount);
		if (nextTurnDamage <= 0)
		{
			return amount;
		}

		EnqueuePendingCompensation(commandId, target, nextTurnDamage, dealer, cardSource);
		return immediateDamage;
	}

	internal override async Task AfterEnemyDamageReceivedAny(HextechEnemyHexContext context, Creature target, DamageResult result, Creature? dealer, CardModel? cardSource)
	{
		long commandId = HextechCombatHooks.CurrentActualDamageCommandId;
		if (commandId == 0L || !TryTakePendingCompensation(commandId, target, out PendingCompensation? pending))
		{
			return;
		}

		PendingCompensation compensation = pending!;
		if (!CanApplyPendingCompensation(context, target, compensation))
		{
			return;
		}

		Creature applier = compensation.Dealer is { IsAlive: true } ? compensation.Dealer : target;
		await HextechCombatHooks.RunWithCompensationReplacementGuard(
			() => PowerCmd.Apply<HextechNextTurnDamagePower>(target, compensation.Amount, applier, compensation.CardSource));
	}

	internal static void ClearPendingCompensations(long commandId)
	{
		_effectWithPendingCompensation?.ClearPendingCompensationsForCommand(commandId);
	}

	internal static (decimal ImmediateDamage, int NextTurnDamage) SplitDamage(decimal damage)
	{
		if (damage <= 0m)
		{
			return (damage, 0);
		}

		int nextTurnDamage = (int)Math.Min(Math.Floor(damage / 2m), 999999999m);
		return (damage - nextTurnDamage, nextTurnDamage);
	}

	internal static bool ShouldSkipDamageReplacement()
	{
		// 血肉戏法(Sleight of Flesh)在玩家给敌人施加 debuff 时会对该敌人造成一次伤害。
		// 这次伤害不能再被代偿延期,否则「血肉戏法伤害 → 下回合伤害(debuff) → 血肉戏法响应 → …」
		// 会无限递归直至栈溢出。源头切断这条边:代偿在血肉戏法响应期间不替换伤害。
		// 与「代偿施加下回合伤害时抑制血肉戏法响应」(RunWithCompensationReplacementGuard)构成双向防护。
		return HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse
			|| HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse
			|| HextechNextTurnDamagePower.IsResolvingDamage;
	}

	private void EnqueuePendingCompensation(long commandId, Creature target, decimal amount, Creature? dealer, CardModel? cardSource)
	{
		for (int i = _pendingCompensations.Count - 1; i >= 0; i--)
		{
			PendingCompensation pending = _pendingCompensations[i];
			if (pending.CommandId == commandId && pending.Target == target)
			{
				_pendingCompensations[i] = pending with
				{
					Amount = pending.Amount + amount,
					Dealer = dealer ?? pending.Dealer,
					CardSource = cardSource ?? pending.CardSource
				};
				_effectWithPendingCompensation = this;
				return;
			}
		}

		_pendingCompensations.Add(new PendingCompensation(commandId, target, amount, dealer, cardSource));
		_effectWithPendingCompensation = this;
	}

	private bool TryTakePendingCompensation(long commandId, Creature target, out PendingCompensation? pending)
	{
		for (int i = 0; i < _pendingCompensations.Count; i++)
		{
			pending = _pendingCompensations[i];
			if (pending.CommandId != commandId || pending.Target != target)
			{
				continue;
			}

			_pendingCompensations.RemoveAt(i);
			RemoveFromPendingRegistryIfEmpty();
			return true;
		}

		pending = null;
		return false;
	}

	private static bool CanApplyPendingCompensation(HextechEnemyHexContext context, Creature target, PendingCompensation compensation)
	{
		return compensation.Amount > 0m
			&& target.IsAlive
			&& target.CombatState?.RunState == context.RunState;
	}

	private void ClearPendingCompensationsForCommand(long commandId)
	{
		_pendingCompensations.RemoveAll(pending => pending.CommandId == commandId);
		RemoveFromPendingRegistryIfEmpty();
	}

	private void ClearPendingCompensationsForEffect()
	{
		_pendingCompensations.Clear();
		if (ReferenceEquals(_effectWithPendingCompensation, this))
		{
			_effectWithPendingCompensation = null;
		}
	}

	private void RemoveFromPendingRegistryIfEmpty()
	{
		if (_pendingCompensations.Count == 0 && ReferenceEquals(_effectWithPendingCompensation, this))
		{
			_effectWithPendingCompensation = null;
		}
	}

	private sealed record PendingCompensation(long CommandId, Creature Target, decimal Amount, Creature? Dealer, CardModel? CardSource);
}
