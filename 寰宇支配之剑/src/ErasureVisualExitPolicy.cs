namespace UniversalDominionSword;

internal readonly record struct ErasureVisualExitSnapshot(
	bool IsExactNode,
	bool IsReserved,
	bool IsInRemovingList,
	bool HasIncompleteDeathAnimation,
	bool IsCanonicalTerminationActive);

internal static class ErasureVisualExitPolicy
{
	public static bool ShouldPreserve(
		in ErasureVisualExitSnapshot snapshot)
	{
		return snapshot.IsExactNode
			&& snapshot.IsReserved
			&& (snapshot.IsCanonicalTerminationActive
				|| (snapshot.IsInRemovingList
					&& snapshot.HasIncompleteDeathAnimation));
	}
}
