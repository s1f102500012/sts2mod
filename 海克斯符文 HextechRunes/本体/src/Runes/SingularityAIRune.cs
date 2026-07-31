namespace HextechRunes;

public sealed class SingularityAIRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, HextechCombatState combatState)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		// 此前缺这层过滤会把 Ancient 稀有度的禁忌魔典(ForbiddenGrimoire)纳入并生成 → 联机下打出时 power 应用两端
		// 分叉(client 上了 power、host 没上)→ StateDivergence 掉线。
		List<CardModel> powerPool = BuildStableCombatGenerationPool(
			Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint),
			static card => card.Type == CardType.Power);
		if (powerPool.Count == 0)
		{
			return;
		}

		CardModel? card = PickStableGeneratedCard(
			combatState,
			powerPool,
			"singularity-ai-player-power",
			HextechStableRandom.PlayerKey(Owner),
			combatState.RoundNumber.ToString(),
			CountOwnedCardsDrawnFromHistory().ToString());
		if (card == null)
		{
			return;
		}

		card.SetToFreeThisTurn();
		Flash();
		await HextechCardGeneration.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
	}
}
