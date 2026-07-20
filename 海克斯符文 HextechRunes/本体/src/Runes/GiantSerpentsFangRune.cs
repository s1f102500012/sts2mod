namespace HextechRunes;

public sealed class GiantSerpentsFangRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("BlockReductionPercent", 50m)
	];

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| target.Side != CombatSide.Enemy
			|| target.Block <= 0
			|| result.TotalDamage <= 0
			|| !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		int blockLoss = Math.Max(1, (int)Math.Ceiling(target.Block * DynamicVars["BlockReductionPercent"].BaseValue / 100m));
		Flash([target]);
		// 0.109.0 起 LoseBlock 首参新增 PlayerChoiceContext。
#if STS2_109_OR_NEWER
		await CreatureCmd.LoseBlock(choiceContext, target, blockLoss, Owner.Creature);
#else
		await CreatureCmd.LoseBlock(target, blockLoss);
#endif
	}
}
