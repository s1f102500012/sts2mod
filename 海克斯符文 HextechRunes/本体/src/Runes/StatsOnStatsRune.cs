namespace HextechRunes;

public sealed class StatsOnStatsRune : InitialForgeGrantRune
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ForgeCount", 4m)
	];

	protected override int InitialForgeCount => DynamicVars["ForgeCount"].IntValue;
}
