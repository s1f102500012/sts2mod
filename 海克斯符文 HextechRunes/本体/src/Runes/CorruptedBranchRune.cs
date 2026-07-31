using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

public sealed class CorruptedBranchRune : HextechRelicBase
{
	internal const string InnateMarkerSavedPropertyName = "SavedCorruptedBranchInnateMarker";

	private int _generatedCardsThisCombat;

	public override bool HasUponPickupEffect => true;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedGeneratedCardsThisCombat
	{
		get => _generatedCardsThisCombat;
		set => _generatedCardsThisCombat = Math.Max(0, value);
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedCorruptedBranchInnateMarker
	{
		get => 0;
		set { }
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Corruption>(),
		HoverTipFactory.FromKeyword(CardKeyword.Innate)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		CardModel card = Owner.RunState.CreateCard<Corruption>(Owner);
		ApplyPersistentInnate(card);
		CardPileAddResult result = await CardPileCmd.Add(card, PileType.Deck);
		SaveManager.Instance.MarkCardAsSeen(card);
		CardCmd.PreviewCardPileAdd(result, 2f);
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

	public override Task AfterCardEnteredCombat(CardModel card)
	{
		if (Owner == null || card.Owner != Owner)
		{
			return Task.CompletedTask;
		}

		if (CorruptedBranchInnateKeywordPersistence.IsTracked(card.DeckVersion))
		{
			CorruptedBranchInnateKeywordPersistence.Restore(card);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		if (!IsOwnedCard(card)
			|| Owner == null
			|| Owner.Creature.IsDead
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

	// 生成的卡牌类型权重:攻击 40% / 技能 20% / 能力 40%。技能占比刻意压低——
	// 消耗型技能会再触发本符文形成滚雪球,均匀分布下太容易无限。
	private static readonly (CardType Type, int Weight)[] GeneratedTypeWeights =
	[
		(CardType.Attack, 40),
		(CardType.Skill, 20),
		(CardType.Power, 40)
	];

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

		int procOrdinal = ConsumeCombatProcOrdinal(nameof(CorruptedBranchRune), ref _generatedCardsThisCombat);
		string?[] saltParts =
		[
			"corrupted-branch-exhaust",
			HextechStableRandom.PlayerKey(Owner),
			combatState.RoundNumber.ToString(),
			procOrdinal.ToString(),
			HextechStableRandom.CardKey(sourceCard)
		];

		// 两阶段:先按类型权重滚点(只计池内实际存在的类型),再在该类型子池内均匀抽取。
		(CardType Type, int Weight)[] presentTypes = GeneratedTypeWeights
			.Where(entry => pool.Any(card => card.Type == entry.Type))
			.ToArray();
		List<CardModel> typedPool = pool;
		if (presentTypes.Length > 0)
		{
			int totalWeight = presentTypes.Sum(static entry => entry.Weight);
			int roll = HextechStableRandom.Index((RunState)Owner.RunState, totalWeight, [.. saltParts, "card-type"]);
			CardType chosenType = presentTypes[^1].Type;
			foreach ((CardType type, int weight) in presentTypes)
			{
				if (roll < weight)
				{
					chosenType = type;
					break;
				}

				roll -= weight;
			}

			typedPool = pool.Where(card => card.Type == chosenType).ToList();
		}

		return PickStableGeneratedCard(
			combatState,
			typedPool,
			[.. saltParts, HextechStableRandom.CardPileKey(typedPool)]);
	}

	private static void ApplyPersistentInnate(CardModel card)
	{
		CorruptedBranchInnateKeywordPersistence.Track(card);
		CardCmd.ApplyKeyword(card, CardKeyword.Innate);
	}
}
