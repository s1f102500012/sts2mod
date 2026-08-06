namespace HextechRunes;

public static class HextechRunesInterop
{
	private static readonly object ExtraActProviderLock = new();
	private static readonly List<Func<IRunState, string?>> ExtraActProviders = [];

	public static void RegisterExtraActProvider(Func<IRunState, string?> provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		lock (ExtraActProviderLock)
		{
			if (!ExtraActProviders.Contains(provider))
			{
				ExtraActProviders.Add(provider);
			}
		}
	}

	internal static string? GetCurrentExtraActId(IRunState runState)
	{
		Func<IRunState, string?>[] providers;
		lock (ExtraActProviderLock)
		{
			providers = ExtraActProviders.ToArray();
		}

		foreach (Func<IRunState, string?> provider in providers)
		{
			try
			{
				string? stageId = provider(runState);
				if (!string.IsNullOrWhiteSpace(stageId))
				{
					return stageId.Trim();
				}
			}
			catch (Exception ex)
			{
				if (HextechRunLogBudget.TryConsume("compat.extra-act-provider", 3))
				{
					Log.Warn($"[{ModInfo.Id}][Mayhem] Extra act provider failed and was ignored: {ex.Message}");
				}
			}
		}

		return null;
	}
}
