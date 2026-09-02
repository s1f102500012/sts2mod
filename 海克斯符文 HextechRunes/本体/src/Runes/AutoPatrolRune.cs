namespace HextechRunes;

public sealed class AutoPatrolRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OstyHpPerCard", 10m),
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<SweepingGaze>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner
			|| Owner == null
			|| Owner.Creature.IsDead
			|| !Owner.IsOstyAlive
			|| Owner.Osty is not { } osty
			|| Owner.Creature.CombatState is not HextechCombatState combatState
			|| !CombatManager.Instance.IsInProgress
			|| CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		int count = Math.Max(0, FloorToInt(osty.CurrentHp / DynamicVars["OstyHpPerCard"].BaseValue)) * DynamicVars["Cards"].IntValue;
		if (count <= 0)
		{
			return;
		}

		Flash();
		for (int i = 0; i < count; i++)
		{
			CardModel card = combatState.CreateCard<SweepingGaze>(Owner);
			card.SetToFreeThisTurn();
			card.ExhaustOnNextPlay = true;
			await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top, this, skipVisuals: true);
			await HextechAutoPlayHelper.AutoPlayTransientCardAndCleanup(
				choiceContext,
				card,
				target: null,
				skipCardPileVisuals: true);
		}
	}
}
