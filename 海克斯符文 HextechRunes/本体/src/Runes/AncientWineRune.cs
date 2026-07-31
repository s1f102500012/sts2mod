namespace HextechRunes;

public sealed class AncientWineRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 1m)
	];

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (!IsOwnedSkill(cardPlay.Card) || Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		int healAmount = CalculateHealAmount(Owner.Creature.MaxHp, DynamicVars["HealPercent"].BaseValue);
		Flash();
		return CreatureCmd.Heal(Owner.Creature, healAmount);
	}

	internal static int CalculateHealAmount(int maxHp, decimal healPercent)
	{
		return Math.Max(1, FloorToInt(Math.Max(0, maxHp) * Math.Max(0m, healPercent) / 100m));
	}
}
