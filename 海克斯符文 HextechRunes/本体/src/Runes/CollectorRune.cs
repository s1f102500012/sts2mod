namespace HextechRunes;

public sealed class CollectorRune : HextechRelicBase
{
	internal const decimal ExecutePercent = 10m;
	internal const int CountPerExecute = 20;

	private const string ExecutePercentVar = "ExecutePercent";
	private const string CountPerExecuteVar = "CountPerExecute";

	private readonly HashSet<Creature> _creditedExecutions = new(ReferenceEqualityComparer.Instance);
	private int _countThisCombat;
	private bool _executing;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCountThisCombat
	{
		get => _countThisCombat;
		set
		{
			_countThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount => !IsCanonical ? _countThisCombat : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar(ExecutePercentVar, ExecutePercent),
		new DynamicVar(CountPerExecuteVar, CountPerExecute)
	];

	public override async Task AfterDamageGiven(
		PlayerChoiceContext choiceContext,
		Creature? dealer,
		DamageResult result,
		ValueProp props,
		Creature target,
		CardModel? cardSource)
	{
		if (_executing
			|| Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0m
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		if (result.WasTargetKilled)
		{
			RecordExecution(target);
			return;
		}

		if (!target.IsAlive
			|| !IsBelowExecuteThreshold(target.CurrentHp, target.MaxHp, DynamicVars[ExecutePercentVar].BaseValue))
		{
			return;
		}

		_executing = true;
		try
		{
			await CreatureCmd.Kill(target);
			RecordExecution(target);
		}
		finally
		{
			_executing = false;
		}
	}

	public override Task BeforeCombatStart()
	{
		ResetCount();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
		{
			HextechGoldRewardHelper.AddFixedExtraGoldReward(room, Owner, _countThisCombat);
		}

		ResetCount();
		return Task.CompletedTask;
	}

	internal void RecordExecution(Creature target)
	{
		if (Owner == null
			|| target.Side == Owner.Creature.Side
			|| !HextechMonsterInteractionPolicy.IsTrueCombatDeath(target)
			|| !_creditedExecutions.Add(target))
		{
			return;
		}

		_countThisCombat += DynamicVars[CountPerExecuteVar].IntValue;
		InvokeDisplayAmountChanged();
		Flash();
	}

	internal static bool IsBelowExecuteThreshold(decimal currentHp, decimal maxHp, decimal executePercent)
	{
		return maxHp > 0m
			&& executePercent > 0m
			&& currentHp < maxHp * executePercent / 100m;
	}

	private void ResetCount()
	{
		_countThisCombat = 0;
		_creditedExecutions.Clear();
		InvokeDisplayAmountChanged();
	}
}
