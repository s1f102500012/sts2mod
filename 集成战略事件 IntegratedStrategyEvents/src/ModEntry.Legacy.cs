#if !STS2_109_OR_NEWER
using IntegratedStrategyEvents.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;
namespace IntegratedStrategyEvents;
public static partial class ModEntry
{
	private static void InjectSavedPropertyCaches() => SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ProphecyProjectionRelic));
}
#endif
