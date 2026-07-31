using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

public sealed class SweepingBladeRune : HextechRelicBase
{
	private static readonly FieldInfo? AttackCommandSingleTargetField = TryGetField(typeof(AttackCommand), "_singleTarget");
	private static readonly FieldInfo? AttackCommandCombatStateField = TryGetField(typeof(AttackCommand), "_combatState");
	private SweepingBladeContext? _activeContext;
	private bool _isReplicatingPower;

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override Task BeforeAttack(AttackCommand command)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| command.Attacker != Owner.Creature
			|| command.ModelSource is not CardModel card
			|| !IsOwnedAttack(card)
			|| !card.Tags.Contains(CardTag.Strike)
			|| !command.IsSingleTargeted
			|| Owner.Creature.CombatState == null)
		{
			return Task.CompletedTask;
		}

		Creature? originalTarget = GetSingleTarget(command);
		if (originalTarget == null || _activeContext?.Matches(card, originalTarget) != true)
		{
			return Task.CompletedTask;
		}

		Flash();
		RetargetToAllOpponents(command, Owner.Creature.CombatState);
		return Task.CompletedTask;
	}

#if STS2_104_OR_NEWER
	public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
#else
	public override Task AfterAttack(AttackCommand command)
#endif
	{
		if (_activeContext == null
			|| command.ModelSource != _activeContext.Card
			|| command.Attacker != Owner?.Creature)
		{
			return Task.CompletedTask;
		}

#if STS2_105_OR_NEWER
		_activeContext.RecordAttackResults(command.Results.SelectMany(static results => results));
#else
		_activeContext.RecordAttackResults(command.Results);
#endif
		return Task.CompletedTask;
	}

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| cardPlay.Card.Owner != Owner
			|| !IsOwnedAttack(cardPlay.Card)
			|| !cardPlay.Card.Tags.Contains(CardTag.Strike)
			|| cardPlay.Target == null
			|| cardPlay.Target.Side == Owner.Creature.Side
			|| Owner.Creature.CombatState == null)
		{
			_activeContext = null;
			return Task.CompletedTask;
		}

		_activeContext = new SweepingBladeContext(cardPlay.Card, cardPlay.Target, Owner.Creature.CombatState);
		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (_activeContext?.Card == cardPlay.Card)
		{
			_activeContext = null;
		}

		return Task.CompletedTask;
	}

	public override async Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
	{
		if (_isReplicatingPower
			|| amount == 0m
			|| Owner == null
			|| _activeContext == null
			|| cardSource != _activeContext.Card
			|| applier != Owner.Creature
			|| target != _activeContext.OriginalTarget
			|| target.Side == Owner.Creature.Side)
		{
			return;
		}

		List<Creature> extraTargets = _activeContext.GetOtherHittableTargets().ToList();
		if (extraTargets.Count == 0)
		{
			return;
		}

		PowerModel? canonicalPower = GetCanonicalPowerForReplication(power);
		if (canonicalPower == null)
		{
			return;
		}

		_isReplicatingPower = true;
		try
		{
			foreach (Creature extraTarget in extraTargets)
			{
				PowerModel powerCopy = canonicalPower.ToMutable();
				decimal replicatedAmount = _activeContext.GetPowerAmountForTarget(power, extraTarget, amount);
				await PowerCmd.Apply(powerCopy, extraTarget, replicatedAmount, applier, cardSource);
			}
		}
		finally
		{
			_isReplicatingPower = false;
		}
	}

#if STS2_104_OR_NEWER
	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#else
	public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
#endif
	{
		if (_isReplicatingPower
			|| amount == 0m
			|| Owner == null
			|| _activeContext == null
			|| cardSource != _activeContext.Card
			|| applier != Owner.Creature
			|| power.Owner != _activeContext.OriginalTarget)
		{
			return;
		}

		decimal correctedAmount = _activeContext.GetCorrectedOriginalPowerAmount(power, amount);
		decimal correction = correctedAmount - amount;
		if (correction == 0m)
		{
			return;
		}

		PowerModel? canonicalPower = GetCanonicalPowerForReplication(power);
		if (canonicalPower == null)
		{
			return;
		}

		_isReplicatingPower = true;
		try
		{
			PowerModel powerCopy = canonicalPower.ToMutable();
			await PowerCmd.Apply(powerCopy, _activeContext.OriginalTarget, correction, applier, cardSource);
		}
		finally
		{
			_isReplicatingPower = false;
		}
	}

	private static PowerModel? GetCanonicalPowerForReplication(PowerModel power)
	{
		ModelId powerId = power.Id;
		PowerModel? canonicalPower = powerId == ModelId.none
			? null
			: ModelDb.GetByIdOrNull<PowerModel>(powerId);
		if (canonicalPower == null)
		{
			Log.Warn(
				$"[{ModInfo.Id}][SweepingBlade] Skipped power replication because its model is not registered: "
				+ $"power={powerId.Entry} type={power.GetType().FullName}.");
		}

		return canonicalPower;
	}

	private static Creature? GetSingleTarget(AttackCommand command)
	{
		return AttackCommandSingleTargetField?.GetValue(command) as Creature;
	}

	private static void RetargetToAllOpponents(AttackCommand command, object combatState)
	{
		if (AttackCommandSingleTargetField == null || AttackCommandCombatStateField == null)
		{
			return;
		}

		AttackCommandSingleTargetField.SetValue(command, null);
		AttackCommandCombatStateField.SetValue(command, combatState);
	}

	private sealed class SweepingBladeContext(CardModel card, Creature originalTarget, HextechCombatState combatState)
	{
		private readonly Dictionary<Creature, decimal> _lastAttackDamageByTarget = [];

		public CardModel Card { get; } = card;

		public Creature OriginalTarget { get; } = originalTarget;

		public HextechCombatState CombatState { get; } = combatState;

		public bool Matches(CardModel card, Creature target)
		{
			return Card == card && OriginalTarget == target;
		}

		public void RecordAttackResults(IEnumerable<DamageResult> results)
		{
			_lastAttackDamageByTarget.Clear();
			foreach (DamageResult result in results)
			{
				if (result.Receiver != null)
				{
					_lastAttackDamageByTarget[result.Receiver] = result.TotalDamage;
				}
			}
		}

		public IEnumerable<Creature> GetOtherHittableTargets()
		{
			return CombatState.HittableEnemies.Where(creature => creature != OriginalTarget);
		}

		public decimal GetPowerAmountForTarget(PowerModel power, Creature target, decimal fallbackAmount)
		{
			return ShouldUseAttackDamageAmount(power) && _lastAttackDamageByTarget.TryGetValue(target, out decimal damage)
				? damage
				: fallbackAmount;
		}

		public decimal GetCorrectedOriginalPowerAmount(PowerModel power, decimal fallbackAmount)
		{
			return ShouldUseAttackDamageAmount(power) && _lastAttackDamageByTarget.TryGetValue(OriginalTarget, out decimal damage)
				? damage
				: fallbackAmount;
		}

		private static bool ShouldUseAttackDamageAmount(PowerModel power)
		{
			return power is DoomPower;
		}
	}
}
