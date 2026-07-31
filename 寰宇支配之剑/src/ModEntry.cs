using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace UniversalDominionSword;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.UniversalDominionSword";

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

		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		NeowFourthOption.Install(harmony);
		DynamicRelicIcon.Install(harmony);
		ErasureTargeting.Install(harmony);
		ErasureKill.Install(harmony);

		_initialized = true;
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}. Neow fourth option, dynamic cosmic relic icon, and Erasure enabled.");
		Log.Info(
			$"[{ModInfo.Id}] Patch contract: " +
			ErasurePatchContract.RuntimeSummary);
	}
}
