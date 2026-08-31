namespace HextechRunes;

public sealed class SomethingForNothingRune : HextechRelicBase
{
	private bool _discountTriggeredThisTurn;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1),
		new EnergyVar(1)
	];

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public bool SavedTriggeredThisTurn
	{
		get
		{
			EnsureTurnScopedStateCurrent(ResetTurnState);
			return HasTurnProcTriggered(nameof(SomethingForNothingRune), _discountTriggeredThisTurn);
		}
		set
		{
			_discountTriggeredThisTurn = value;
			UpdateTurnScopedStateIdentity();
		}
	}

	public override Task BeforeCombatStart()
	{
		ResetTurnState(null);
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetTurnState(null);
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, HextechCombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			ResetTurnState(combatState);
		}

		return Task.CompletedTask;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| cardPlay.Card.Owner != Owner)
		{
			return Task.CompletedTask;
		}

		decimal playedEnergyCost = HextechCombatHooks.GetEnergyCostForCurrentCardPlay(cardPlay.Card);
		if (IsZeroCostPlay(playedEnergyCost))
		{
			Flash();
			return CardPileCmd.Draw(context, DynamicVars.Cards.BaseValue, Owner, fromHandDraw: false);
		}

		EnsureTurnScopedStateCurrent(ResetTurnState);
		if (cardPlay.Card.EnergyCost.CostsX
			|| HasTurnProcTriggered(nameof(SomethingForNothingRune), _discountTriggeredThisTurn)
			|| !TryConsumeTurnProc(nameof(SomethingForNothingRune), ref _discountTriggeredThisTurn))
		{
			return Task.CompletedTask;
		}

		int currentCost = cardPlay.Card.EnergyCost.GetWithModifiers(CostModifiers.Local);
		int reducedCost = ReduceCost(currentCost, DynamicVars.Energy.IntValue);
		cardPlay.Card.EnergyCost.SetThisCombat(reducedCost, reduceOnly: true);
		cardPlay.Card.InvokeEnergyCostChanged();
		Flash();
		return Task.CompletedTask;
	}

	internal static bool IsZeroCostPlay(decimal energyCost)
	{
		return energyCost <= 0m;
	}

	internal static int ReduceCost(int currentCost, int reduction)
	{
		return Math.Max(0, currentCost - Math.Max(0, reduction));
	}

	private void ResetTurnState()
	{
		ResetTurnState(null);
	}

	private void ResetTurnState(HextechCombatState? combatState)
	{
		_discountTriggeredThisTurn = false;
		UpdateTurnScopedStateIdentity(combatState);
	}
}
