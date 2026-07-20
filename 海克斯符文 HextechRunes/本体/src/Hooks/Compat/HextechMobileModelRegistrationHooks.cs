using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechMobileModelRegistrationHooks
{
	public static void Install(Harmony harmony)
	{
		harmony.Patch(
#if STS2_109_OR_NEWER
			// 0.109.0 起 Init 新增可选参数 Type[]? injectedModelTypes,按新签名精确匹配。
			RequireMethod(typeof(ModelDb), nameof(ModelDb.Init), BindingFlags.Static | BindingFlags.Public, typeof(Type[])),
#else
			RequireMethod(typeof(ModelDb), nameof(ModelDb.Init), BindingFlags.Static | BindingFlags.Public),
#endif
			postfix: new HarmonyMethod(typeof(HextechMobileModelRegistrationHooks), nameof(ModelDbInitPostfix)));
	}

	private static void ModelDbInitPostfix()
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
