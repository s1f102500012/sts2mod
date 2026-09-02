using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechMobileModelRegistrationHooks
{


	#if STS2_109_OR_NEWER
	[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init), typeof(Type[]))]
	#else
	[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init), new Type[0])]
	#endif
	[HextechPatch("compat.mobile-model-registration", "移动端模型注册兜底")]
	private static class ModelDbInitPatch
	{
		[HarmonyPostfix]
		private static void Postfix()
		{
			try
			{
				HextechModelBootstrap.CleanupMobileFirstModelRegistrationWorkaround();
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}] Android model registration workaround cleanup skipped: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}
