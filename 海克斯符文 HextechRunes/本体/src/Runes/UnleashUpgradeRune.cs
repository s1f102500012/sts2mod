namespace HextechRunes;

public sealed class UnleashUpgradeRune : CardUpgradeRuneBase<Unleash>
{
	internal override bool GrantsCardOnPickup => false;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Unleash>(),
		HoverTipFactory.FromCard<Protector>()
	];

	internal override bool MeetsCardAvailabilityRequirement(IEnumerable<CardModel> deckCards)
	{
		return deckCards.Any(static card => card is Unleash or Protector);
	}

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| result.UnblockedDamage <= 0m
			|| cardSource?.Owner != Owner
			|| cardSource is not (Unleash or Protector)
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		Flash([target]);
		await OstyCmd.Summon(choiceContext, Owner, result.UnblockedDamage, this);
	}
}
