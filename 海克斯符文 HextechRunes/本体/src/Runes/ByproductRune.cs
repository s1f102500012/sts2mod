namespace HextechRunes;

public sealed class ByproductRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		bool addedByPlayer = creator == Owner;
		if (!addedByPlayer || card.Owner != Owner || Owner == null || Owner.Creature.IsDead || card.Type != CardType.Status)
		{
			return;
		}

		Flash();
		await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), DynamicVars.Cards.BaseValue, Owner, fromHandDraw: false);
	}
}
