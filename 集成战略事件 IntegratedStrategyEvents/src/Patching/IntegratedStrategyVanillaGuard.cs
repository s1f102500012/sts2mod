using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace IntegratedStrategyEvents;

internal static class IntegratedStrategyVanillaGuard
{
	private static readonly Dictionary<string, string> Expected = Load();
	private static Dictionary<string, string> Load()
	{
		Dictionary<string, string> result = new(StringComparer.Ordinal);
		using Stream? stream = typeof(IntegratedStrategyVanillaGuard).Assembly.GetManifestResourceStream("vanilla_guard.txt");
		if (stream == null) return result;
		using StreamReader reader = new(stream);
		while (reader.ReadLine() is string line)
		{
			int separator = line.LastIndexOf('=');
			if (separator > 0 && !line.StartsWith('#')) result.Add(line[..separator], line[(separator + 1)..]);
		}
		return result;
	}

	internal static void Verify(MethodBase method)
	{
		string key = IntegratedStrategyPatchCatalog.TargetKey(method);
		if (!Expected.TryGetValue(key, out string? expected))
			throw new InvalidOperationException($"Missing vanilla IL baseline: {key}");
		string actual = IntegratedStrategyPatchCatalog.IlHash(method);
		if (actual != expected)
			Log.Warn($"{ModInfo.LogPrefix}[VanillaGuard] DRIFT {key}: expected={expected}, actual={actual}.");
		else Log.Info($"{ModInfo.LogPrefix}[VanillaGuard] OK {key}");
	}
}
