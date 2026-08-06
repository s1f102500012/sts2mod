namespace HextechRunes;

public sealed class StatsOnStatsOnStatsRune : InitialForgeGrantRune
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("ForgeCount", 6m)
	];

	protected override int InitialForgeCount => DynamicVars["ForgeCount"].IntValue;
}
