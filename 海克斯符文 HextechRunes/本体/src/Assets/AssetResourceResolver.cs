namespace HextechRunes;

internal static class AssetResourceResolver
{
	public static bool IsRawImagePath(string path)
	{
		return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
	}

	public static T? Resolve<T>(
		string path,
		IDictionary<string, T> cache,
		Func<T, bool> isUsable,
		Func<string, T?> loadPrimary,
		Func<string, T?> loadSecondary)
		where T : class
	{
		if (cache.TryGetValue(path, out T? cached))
		{
			if (isUsable(cached))
			{
				return cached;
			}

			cache.Remove(path);
		}

		T? primary = loadPrimary(path);
		if (primary != null && isUsable(primary))
		{
			cache[path] = primary;
			return primary;
		}

		T? secondary = loadSecondary(path);
		if (secondary != null && isUsable(secondary))
		{
			cache[path] = secondary;
			return secondary;
		}

		return null;
	}
}
