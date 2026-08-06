namespace HextechRunes;

public sealed class StatsRune : InitialForgeGrantRune
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ForgeCount", 2m)
	];

	protected override int InitialForgeCount => DynamicVars["ForgeCount"].IntValue;
}
