namespace HextechRunes;

public sealed class DeadwoodRune : HextechRelicBase
{
	private int _generatedCardsThisCombat;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedGeneratedCardsThisCombat
	{
		get => _generatedCardsThisCombat;
		set => _generatedCardsThisCombat = Math.Max(0, value);
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override Task BeforeCombatStart()
	{
		_generatedCardsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_generatedCardsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		if (!IsOwnedCard(card)
			|| Owner == null
			|| Owner.Creature.IsDead
			|| (!causedByEthereal && !card.Keywords.Contains(CardKeyword.Ethereal))
			|| Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		CardModel? generatedCard = CreateRandomCombatCard(combatState, card);
		if (generatedCard == null)
		{
			return;
		}

		Flash();
		await HextechCardGeneration.AddGeneratedCardToCombat(generatedCard, PileType.Hand, addedByPlayer: true);
	}

	private CardModel? CreateRandomCombatCard(HextechCombatState combatState, CardModel sourceCard)
	{
		if (Owner == null)
		{
			return null;
		}

		List<CardModel> pool = BuildStableCombatGenerationPool(
			Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint));
		if (pool.Count == 0)
		{
			return null;
		}

		int procOrdinal = ConsumeCombatProcOrdinal(nameof(DeadwoodRune), ref _generatedCardsThisCombat);
		return PickStableGeneratedCard(
			combatState,
			pool,
			"deadwood-ethereal-exhaust",
			HextechStableRandom.PlayerKey(Owner),
			combatState.RoundNumber.ToString(),
			procOrdinal.ToString(),
			HextechStableRandom.CardKey(sourceCard),
			HextechStableRandom.CardPileKey(pool));
	}
}
