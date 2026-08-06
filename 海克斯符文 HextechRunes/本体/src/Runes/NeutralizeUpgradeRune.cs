namespace HextechRunes;

public sealed class NeutralizeUpgradeRune : CardUpgradeRuneBase<Neutralize>
{
	private bool _isAutoPlayingDiscardedCard;

	internal override bool GrantsCardOnPickup => false;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Repeats", 2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Neutralize>(),
		HoverTipFactory.FromCard<Suppress>()
	];

	internal override bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return deckCards.Any(static card => card is Neutralize or Suppress);
	}

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsSilentPlayer(player);
	}

	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		if (_isAutoPlayingDiscardedCard
			|| Owner == null
			|| !IsOwnedCard(card)
			|| Owner.Creature.IsDead
			|| !IsSupportedCard(card)
			|| Owner.Creature.CombatState == null
			|| !CombatManager.Instance.IsInProgress
			|| CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		_isAutoPlayingDiscardedCard = true;
		try
		{
			Flash(enemies);
			for (int repeat = 0; repeat < DynamicVars["Repeats"].IntValue; repeat++)
			{
				foreach (Creature enemy in enemies.Where(static enemy => !enemy.IsDead).ToList())
				{
					CardModel copy = card.CreateClone();
					copy.SetToFreeThisTurn();
					copy.ExhaustOnNextPlay = true;
					await CardPileCmd.Add(copy, PileType.Hand, CardPilePosition.Top, this, skipVisuals: true);
					await HextechAutoPlayHelper.AutoPlayTransientCardAndCleanup(
						choiceContext,
						copy,
						enemy,
						skipCardPileVisuals: true);
				}
			}
		}
		finally
		{
			_isAutoPlayingDiscardedCard = false;
		}
	}

	private static bool IsSupportedCard(CardModel card)
	{
		return card is Neutralize or Suppress;
	}
}
