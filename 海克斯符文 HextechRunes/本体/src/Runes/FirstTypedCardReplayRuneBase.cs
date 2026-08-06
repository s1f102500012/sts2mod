namespace HextechRunes;

public abstract class FirstTypedCardReplayRuneBase : HextechRelicBase
{
	private bool _triggeredThisTurn;

	protected abstract CardType TargetCardType { get; }

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Replays", 1m)
	];

	public override Task BeforeCombatStart()
	{
		ResetTriggered(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTriggered(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetTriggered(combatState);
		}

		return Task.CompletedTask;
	}

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		EnsureTurnScopedStateCurrent(ResetTriggered);
		if (HasTurnProcTriggered(GetStableTurnProcKey(), _triggeredThisTurn) || !IsOwnedTargetType(card))
		{
			return playCount;
		}

		return playCount + DynamicVars["Replays"].IntValue;
	}

	public override Task AfterModifyingCardPlayCount(CardModel card)
	{
		EnsureTurnScopedStateCurrent(ResetTriggered);
		string procKey = GetStableTurnProcKey();
		if (!HasTurnProcTriggered(procKey, _triggeredThisTurn) && IsOwnedTargetType(card))
		{
			if (TryConsumeTurnProc(procKey, ref _triggeredThisTurn))
			{
				Flash();
			}
		}

		return Task.CompletedTask;
	}

	private bool IsOwnedTargetType(CardModel? card)
	{
		if (TargetCardType == CardType.Attack)
		{
			return IsOwnedAttack(card);
		}

		if (TargetCardType == CardType.Skill)
		{
			return IsOwnedSkill(card);
		}

		return card?.Owner == Owner && card.Type == TargetCardType;
	}

	private void ResetTriggered()
	{
		ResetTriggered(null);
	}

	private void ResetTriggered(HextechCombatState? combatState)
	{
		_triggeredThisTurn = false;
		UpdateTurnScopedStateIdentity(combatState);
	}
}
