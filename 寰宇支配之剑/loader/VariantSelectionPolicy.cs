namespace UniversalDominionSword.Loader;

internal static class VariantSelectionPolicy
{
	public static Version? PickCompatibleVersion(
		IEnumerable<Version> targets,
		Version? host)
	{
		Version[] ordered = targets
			.Distinct()
			.OrderBy(version => version)
			.ToArray();
		if (ordered.Length == 0)
		{
			return null;
		}

		return host == null
			? ordered[^1]
			: ordered.LastOrDefault(version => version <= host);
	}
}
