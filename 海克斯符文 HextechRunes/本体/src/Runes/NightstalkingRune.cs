namespace HextechRunes;

public sealed class NightstalkingRune : HextechRelicBase
{
	private int _cardsDrawnThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCardsDrawnThisCombat
	{
		get => IsNetworkMultiplayer() ? 0 : GetCardsDrawnThisCombat();
		set
		{
			_cardsDrawnThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical;

	public override int DisplayAmount
	{
		get
		{
			if (IsCanonical)
			{
				return 0;
			}

			int cardsNeeded = DynamicVars["CardsNeeded"].IntValue;
			int remainder = GetCardsDrawnThisCombat() % cardsNeeded;
			return remainder == 0 ? cardsNeeded : cardsNeeded - remainder;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsNeeded", 15m),
		new PowerVar<IntangiblePower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<IntangiblePower>()
	];

	public override Task BeforeCombatStart()
	{
		_cardsDrawnThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_cardsDrawnThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		if (ShouldUseNetworkCombatHistory())
		{
			await ResolveDrawProgressFromHistory();
			return;
		}

		_cardsDrawnThisCombat++;
		InvokeDisplayAmountChanged();
		if (_cardsDrawnThisCombat % DynamicVars["CardsNeeded"].IntValue != 0)
		{
			return;
		}

		await ApplyDrawThresholdReward();
	}

	public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (ShouldUseNetworkCombatHistory() && cardPlay.Card.Owner == Owner)
		{
			await ResolveDrawProgressFromHistory();
		}
	}

	public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
	{
		if (ShouldUseNetworkCombatHistory() && player == Owner)
		{
			await ResolveDrawProgressFromHistory();
		}
	}


	private async Task ResolveDrawProgressFromHistory()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		int cardsDrawn = CountOwnedCardsDrawnFromHistory();
		int previousCardsDrawn = _cardsDrawnThisCombat;
		if (cardsDrawn <= previousCardsDrawn)
		{
			return;
		}

		int cardsNeeded = DynamicVars["CardsNeeded"].IntValue;
		_cardsDrawnThisCombat = cardsDrawn;
		InvokeDisplayAmountChanged();
		int rewards = cardsDrawn / cardsNeeded - previousCardsDrawn / cardsNeeded;
		for (int i = 0; i < rewards; i++)
		{
			await ApplyDrawThresholdReward();
		}
	}

	private async Task ApplyDrawThresholdReward()
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<IntangiblePower>(Owner.Creature, DynamicVars["IntangiblePower"].BaseValue, Owner.Creature, null);
	}

	private int GetCardsDrawnThisCombat()
	{
		return ShouldUseNetworkCombatHistory()
			? CountOwnedCardsDrawnFromHistory()
			: _cardsDrawnThisCombat;
	}
}
