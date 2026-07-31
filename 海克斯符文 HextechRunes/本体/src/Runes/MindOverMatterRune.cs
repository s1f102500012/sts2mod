namespace HextechRunes;

public sealed class MindOverMatterRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
	];

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, HextechCombatState combatState)
	{
		if (player != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		List<CardModel> pool = BuildStableCombatGenerationPool(
			Owner.Character.CardPool
				.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint));
		if (pool.Count == 0)
		{
			return;
		}

		CardModel? card = PickStableGeneratedCard(
			combatState,
			pool,
			"mind-over-matter",
			HextechStableRandom.PlayerKey(Owner),
			combatState.RoundNumber.ToString(),
			CountOwnedCardsDrawnFromHistory().ToString());
		if (card == null)
		{
			return;
		}
		card.AddKeyword(CardKeyword.Ethereal);
		card.SetToFreeThisCombat();

		Flash();
		await HextechCardGeneration.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
	}
}
