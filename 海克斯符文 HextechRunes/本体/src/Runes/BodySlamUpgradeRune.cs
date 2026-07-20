namespace HextechRunes;

public sealed class BodySlamUpgradeRune : CardUpgradeRuneBase<BodySlam>
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", 2m)
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsBodySlam(cardSource) || !IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource))
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || Owner.Creature.IsDead || cardPlay.Card.Owner != Owner || !IsBodySlam(cardPlay.Card))
		{
			return;
		}

		decimal block = Owner.Creature.Block;
		if (block <= 0m)
		{
			return;
		}

		Flash();
		// 0.109.0 起 LoseBlock 首参新增 PlayerChoiceContext。
#if STS2_109_OR_NEWER
		await CreatureCmd.LoseBlock(context, Owner.Creature, block, Owner.Creature);
#else
		await CreatureCmd.LoseBlock(Owner.Creature, block);
#endif
	}

	private static bool IsBodySlam(CardModel? card)
	{
		return card is BodySlam;
	}
}
