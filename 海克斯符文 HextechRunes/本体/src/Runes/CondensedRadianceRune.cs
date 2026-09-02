namespace HextechRunes;

public sealed class CondensedRadianceRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1),
		new StarsVar(1)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsRegentPlayer(player);
	}

	public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		bool addedByPlayer = creator == Owner;
		if (!addedByPlayer || card.Owner != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		Flash();
		await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue, Owner);
	}
}
