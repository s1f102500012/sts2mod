using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace UniversalDominionSword;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private static Harmony? _harmony;

	private static bool _initialized;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		ModHelper.AddModelToPool<EventRelicPool, UniversalDominionSwordRelic>();
		ModHelper.AddModelToPool<TokenCardPool, UniversalDominionSwordCard>();

		Harmony harmony = _harmony ??= new Harmony(ModInfo.HarmonyId);
		SwordPatcher.ApplyAll(harmony, typeof(ModEntry).Assembly);
		SwordPatcher.LogSummary();
		SwordPatcher.LogSharedPatchTargets(harmony);

		_initialized = true;
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}
}
