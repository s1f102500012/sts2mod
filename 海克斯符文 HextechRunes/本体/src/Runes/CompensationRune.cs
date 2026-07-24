namespace HextechRunes;

public sealed class CompensationRune : HextechRelicBase
{
	private static readonly HashSet<CompensationRune> RunesWithPendingCompensation = new();

	private readonly List<PendingCompensation> _pendingCompensations = [];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<DoomPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override Task BeforeCombatStart()
	{
		ClearPendingCompensationsForRune();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ClearPendingCompensationsForRune();
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		ClearPendingCompensationsForRune();
		return Task.CompletedTask;
	}

	public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| target != Owner.Creature
			|| Owner.Creature.IsDead
			|| amount <= 0m
			|| !IsNecrobinderPlayer(Owner)
			|| !IsInActiveCombat(Owner))
		{
			return amount;
		}

		long commandId = HextechCombatHooks.CurrentActualDamageCommandId;
		if (commandId == 0L)
		{
			return amount;
		}

		int doom = Math.Min((int)Math.Floor(amount), 999999999);
		if (doom <= 0)
		{
			return amount;
		}

		EnqueuePendingCompensation(commandId, target, doom, dealer, cardSource);
		return 0m;
	}

	public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null || target != Owner.Creature || !IsInActiveCombat(Owner))
		{
			return;
		}

		long commandId = HextechCombatHooks.CurrentActualDamageCommandId;
		if (commandId == 0L || !TryTakePendingCompensation(commandId, target, out PendingCompensation? pending))
		{
			return;
		}

		PendingCompensation compensation = pending!;
		Flash();
		await HextechCombatHooks.RunWithCompensationReplacementGuard(
			() => PowerCmd.Apply<DoomPower>(Owner.Creature, compensation.Amount, compensation.Dealer ?? Owner.Creature, compensation.CardSource));
	}

	internal static void ClearPendingCompensations(long commandId)
	{
		if (RunesWithPendingCompensation.Count == 0)
		{
			return;
		}

		CompensationRune[] runes = RunesWithPendingCompensation.ToArray();
		foreach (CompensationRune rune in runes)
		{
			rune.ClearPendingCompensationsForCommand(commandId);
		}
	}

	internal static bool IsActiveCombatContext(bool combatInProgress, bool currentRoomIsCombat, bool combatStateMatchesRun)
	{
		return combatInProgress && currentRoomIsCombat && combatStateMatchesRun;
	}

	private static bool IsInActiveCombat(Player owner)
	{
		return IsActiveCombatContext(
			CombatManager.Instance?.IsInProgress == true,
			owner.RunState.CurrentRoom is CombatRoom,
			ReferenceEquals(owner.Creature.CombatState?.RunState, owner.RunState));
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
				RunesWithPendingCompensation.Add(this);
				return;
			}
		}

		_pendingCompensations.Add(new PendingCompensation(commandId, target, amount, dealer, cardSource));
		RunesWithPendingCompensation.Add(this);
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

	private void ClearPendingCompensationsForCommand(long commandId)
	{
		_pendingCompensations.RemoveAll(pending => pending.CommandId == commandId);
		RemoveFromPendingRegistryIfEmpty();
	}

	private void ClearPendingCompensationsForRune()
	{
		_pendingCompensations.Clear();
		RunesWithPendingCompensation.Remove(this);
	}

	private void RemoveFromPendingRegistryIfEmpty()
	{
		if (_pendingCompensations.Count == 0)
		{
			RunesWithPendingCompensation.Remove(this);
		}
	}

	private sealed record PendingCompensation(long CommandId, Creature Target, decimal Amount, Creature? Dealer, CardModel? CardSource);
}
