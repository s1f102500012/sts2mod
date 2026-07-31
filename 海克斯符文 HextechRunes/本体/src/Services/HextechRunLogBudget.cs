namespace HextechRunes;

internal static class HextechRunLogBudget
{
	private static readonly object Sync = new();
	private static readonly Dictionary<string, int> ConsumedByKey = new(StringComparer.Ordinal);

	internal static bool TryConsume(string key, int budget)
	{
		if (budget <= 0)
		{
			return false;
		}

		lock (Sync)
		{
			ConsumedByKey.TryGetValue(key, out int consumed);
			if (consumed >= budget)
			{
				return false;
			}

			ConsumedByKey[key] = consumed + 1;
			return true;
		}
	}

	internal static void Reset()
	{
		lock (Sync)
		{
			ConsumedByKey.Clear();
		}
	}
}
