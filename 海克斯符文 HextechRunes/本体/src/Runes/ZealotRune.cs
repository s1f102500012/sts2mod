namespace HextechRunes;

public sealed class ZealotRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("RelicsNeeded", 5m),
		new CardsVar(1)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		if (player != Owner || !IsOwnersFirstTurn)
		{
			return count;
		}

		return count + Math.Floor(player.Relics.Count / DynamicVars["RelicsNeeded"].BaseValue);
	}
}
