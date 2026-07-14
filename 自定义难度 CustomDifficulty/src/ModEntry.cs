using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace CustomDifficulty;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.CustomDifficulty";

	private static Harmony? _harmony;
	private static bool _initialized;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		_harmony = new Harmony(HarmonyId);
		_harmony.PatchAll();
		CustomDifficultyStorage.Initialize();
		_initialized = true;
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}
}
