namespace HextechRunes;

public sealed class DeathWarrantRune : HextechRelicBase
{
	internal const int CardsNeeded = 8;

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

			int remainder = GetCardsDrawnThisCombat() % CardsNeeded;
			return remainder == 0 ? CardsNeeded : CardsNeeded - remainder;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CardsNeeded", CardsNeeded)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public override bool IsAvailableForPlayer(Player player) => IsSilentPlayer(player);

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
		if (card.Owner != Owner)
		{
			return;
		}

		if (ShouldUseNetworkCombatHistory())
		{
			await ResolveDrawProgressFromHistory();
			return;
		}

		int previousCardsDrawn = _cardsDrawnThisCombat;
		_cardsDrawnThisCombat++;
		InvokeDisplayAmountChanged();
		if (ResolveThresholdCrossings(previousCardsDrawn, _cardsDrawnThisCombat) > 0)
		{
			await TriggerAllEnemyPoison();
		}
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

		_cardsDrawnThisCombat = cardsDrawn;
		InvokeDisplayAmountChanged();
		int triggers = ResolveThresholdCrossings(previousCardsDrawn, cardsDrawn);
		for (int i = 0; i < triggers; i++)
		{
			await TriggerAllEnemyPoison();
		}
	}

	private async Task TriggerAllEnemyPoison()
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		PoisonPower[] poisonPowers = combatState.HittableEnemies
			.Where(static enemy => enemy.IsAlive)
			.Select(static enemy => enemy.GetPower<PoisonPower>())
			.Where(static power => power is { Amount: > 0 })
			.Cast<PoisonPower>()
			.ToArray();
		if (poisonPowers.Length == 0)
		{
			return;
		}

		Flash(poisonPowers.Select(static power => power.Owner));
		foreach (PoisonPower poison in poisonPowers)
		{
			await TriggerPoisonCompat(poison, combatState);
		}
	}

	internal static int ResolveThresholdCrossings(int previousCardsDrawn, int cardsDrawn)
	{
		return Math.Max(0, cardsDrawn) / CardsNeeded
			- Math.Max(0, previousCardsDrawn) / CardsNeeded;
	}

	internal static Task TriggerPoisonCompat(PoisonPower poison, HextechCombatState combatState)
	{
		// 0.110.0 才公开 PoisonPower.Trigger；调用两版本共有的回合触发入口可保持伤害、
		// 催化剂段数、层数递减以及病入膏肓拦截逻辑与原版一致。
		return poison.AfterSideTurnStart(poison.Owner.Side, [poison.Owner], combatState);
	}

	private int GetCardsDrawnThisCombat()
	{
		return ShouldUseNetworkCombatHistory()
			? CountOwnedCardsDrawnFromHistory()
			: _cardsDrawnThisCombat;
	}
}
