namespace HextechRunes;

public sealed class FallingStarUpgradeRune : CardUpgradeRuneBase<FallingStar>
{
	internal override bool GrantsCardOnPickup => false;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("StunTurns", 1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<FallingStar>(),
		HoverTipFactory.FromCard<MeteorShower>()
	];

	internal override bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return deckCards.Any(static card => card is FallingStar or MeteorShower);
	}

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsRegentPlayer(player);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner || !IsSupportedCard(cardPlay.Card))
		{
			return;
		}

		List<Creature> targets = GetTargets(cardPlay).Where(static target => !target.IsDead).ToList();
		if (targets.Count == 0)
		{
			return;
		}

		Flash(targets);
		foreach (Creature target in targets)
		{
			await CreatureCmd.Stun(target);
		}
	}

	private IEnumerable<Creature> GetTargets(CardPlay cardPlay)
	{
		if (cardPlay.Card is MeteorShower)
		{
			return Owner?.Creature.CombatState?.HittableEnemies ?? [];
		}

		if (cardPlay.Target is Creature target && target.Side == CombatSide.Enemy)
		{
			return [target];
		}

		return Owner?.Creature.CombatState?.HittableEnemies ?? [];
	}

	private static bool IsSupportedCard(CardModel card)
	{
		return card is FallingStar or MeteorShower;
	}
}
