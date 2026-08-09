using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Content;

namespace Heartsteel;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	public static void Initialize()
	{
#if !STS2_110_OR_NEWER
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HeartsteelRelic));
#endif

		ModContentRegistry registry = ModContentRegistry.For(ModInfo.Id);
		registry.RegisterRelic<SharedRelicPool, HeartsteelRelic>(
			ModelPublicEntryOptions.FromFullPublicEntry("HEARTSTEEL_RELIC"));
		OrnnsForgeRegistration.Install();

		Log.Info(
			$"[{ModInfo.Id}] Loaded implementation target={ModInfo.TargetGameVersion} " +
			$"with RitsuLib {ModInfo.RitsuLibVersion}; " +
			$"ids={MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(HeartsteelRelic))}," +
			$"{MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(HeartsteelDevourPower))}," +
			$"{MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(OrnnsForge))}.");
	}
}
