namespace HextechRunes;

public sealed class BashUpgradeRune : CardUpgradeRuneBase<Bash>
{
	internal override bool GrantsCardOnPickup => false;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Bash>(),
		HoverTipFactory.FromCard<Break>(),
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<StrengthPower>()
	];

	internal override bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return deckCards.Any(static card => card is Bash or Break);
	}

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner || Owner.Creature.IsDead)
		{
			return;
		}

		decimal amount = CalculateStrengthGain(cardPlay.Card);
		if (amount <= 0m)
		{
			return;
		}

		Flash();
		await PowerCmd.Apply<StrengthPower>(Owner.Creature, amount, Owner.Creature, cardPlay.Card);
	}

	internal static decimal CalculateStrengthGain(CardModel card)
	{
		return card switch
		{
			Bash bash => bash.DynamicVars.Vulnerable.BaseValue,
			Break breakCard => breakCard.DynamicVars.Vulnerable.BaseValue,
			_ => 0m
		};
	}
}
