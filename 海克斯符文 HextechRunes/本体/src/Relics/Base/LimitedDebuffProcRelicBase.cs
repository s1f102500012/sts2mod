using MegaCrit.Sts2.Core.Entities.Relics;

namespace HextechRunes;

public abstract class LimitedDebuffProcRelicBase : HextechRelicBase
{
	private int _procsThisTurn;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedProcsThisTurn
	{
		get
		{
			EnsureTurnScopedStateCurrent(ResetProcs);
			return GetTurnProcCount(GetProcKey(), _procsThisTurn);
		}
		set
		{
			_procsThisTurn = Math.Max(0, value);
			UpdateDisplay();
			UpdateTurnScopedStateIdentity();
		}
	}

	protected virtual int MaxProcsPerTurn => 3;

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? Math.Max(0, MaxProcsPerTurn - GetTurnProcCount(GetProcKey(), _procsThisTurn)) : 0;

	public override Task BeforeCombatStart()
	{
		ResetProcs(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetProcs(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetProcs(combatState);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		EnsureTurnScopedStateCurrent(ResetProcs);
		string procKey = GetProcKey();
		if (!TryGetOwnedEnemyDebuffTarget(power, amount, applier, out Creature? target)
			|| HasTurnProcReachedLimit(procKey, _procsThisTurn, MaxProcsPerTurn))
		{
			return;
		}

		if (!TryConsumeTurnProc(procKey, ref _procsThisTurn, MaxProcsPerTurn))
		{
			return;
		}

		UpdateDisplay();
		Flash(target == null ? Array.Empty<Creature>() : [target]);
		await OnEnemyDebuffApplied(target!);
	}

	protected abstract Task OnEnemyDebuffApplied(Creature target);

	private void ResetProcs()
	{
		ResetProcs(null);
	}

	private void ResetProcs(HextechCombatState? combatState)
	{
		_procsThisTurn = 0;
		UpdateDisplay();
		UpdateTurnScopedStateIdentity(combatState);
	}

	private void UpdateDisplay()
	{
		Status = GetTurnProcCount(GetProcKey(), _procsThisTurn) == MaxProcsPerTurn - 1 ? RelicStatus.Active : RelicStatus.Normal;
		InvokeDisplayAmountChanged();
	}

	private string GetProcKey()
	{
		return GetStableTurnProcKey();
	}
}
