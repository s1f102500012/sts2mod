using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BetterCharacterRelics;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	internal const string HarmonyId = "Natsuki.BetterCharacterRelics";
	internal static string CompatibilityTarget => typeof(ModEntry).Assembly
		.GetCustomAttributes<AssemblyMetadataAttribute>().Single(item => item.Key == "CompatibilityTarget").Value!;

	public static void Initialize()
	{
		Patching.ApplyAll();
        Log.Info($"[BetterCharacterRelics] Loaded 1.1.3; compatibility target {CompatibilityTarget}.");
	}
}
