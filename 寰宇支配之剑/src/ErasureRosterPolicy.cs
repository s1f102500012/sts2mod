namespace UniversalDominionSword;

internal static class ErasureRosterPolicy
{
	private const int MaximumSnapshotAttempts = 4;

	public static T[] SnapshotNonNull<T>(IReadOnlyList<T?> entries)
		where T : class
	{
		for (int attempt = 0; attempt < MaximumSnapshotAttempts; attempt++)
		{
			try
			{
				int count = entries.Count;
				T[] snapshot = new T[count];
				int written = 0;
				for (int index = 0; index < count; index++)
				{
					T? entry = entries[index];
					if (entry != null)
					{
						snapshot[written++] = entry;
					}
				}

				if (written != snapshot.Length)
				{
					Array.Resize(ref snapshot, written);
				}
				return snapshot;
			}
			catch (ArgumentOutOfRangeException)
			{
				// The live roster shrank between Count and the index read.
			}
		}

		return SnapshotAvailableEntries(entries);
	}

	private static T[] SnapshotAvailableEntries<T>(
		IReadOnlyList<T?> entries)
		where T : class
	{
		List<T> snapshot = [];
		int count;
		try
		{
			count = entries.Count;
		}
		catch
		{
			return [];
		}

		for (int index = 0; index < count; index++)
		{
			try
			{
				if (entries[index] is T entry)
				{
					snapshot.Add(entry);
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				break;
			}
		}
		return snapshot.ToArray();
	}
}
