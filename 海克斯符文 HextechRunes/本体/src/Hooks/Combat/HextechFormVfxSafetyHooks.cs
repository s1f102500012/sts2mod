using HarmonyLib;
#if STS2_110_OR_NEWER
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Forms;
using static HextechRunes.HextechHookReflection;
#endif

namespace HextechRunes;

// 自定义角色场景不一定提供 0.110 起新增的 %FormVfx 容器。形态能力本身不依赖该节点，
// 因此容器缺失时只跳过纯视觉挂载与清理，避免战斗结算和放弃流程被 VFX 异常中断。
internal static class HextechFormVfxSafetyHooks
{
	public static void Install(Harmony harmony)
	{
#if STS2_110_OR_NEWER
		harmony.Patch(
			ResolveAddFormVfxTarget(),
			prefix: new HarmonyMethod(typeof(HextechFormVfxSafetyHooks), nameof(AddFormVfxPrefix))
			{
				priority = Priority.First
			});
		harmony.Patch(
			ResolveRemoveFormVfxTarget(),
			prefix: new HarmonyMethod(typeof(HextechFormVfxSafetyHooks), nameof(RemoveFormVfxPrefix))
			{
				priority = Priority.First
			});
#endif
	}

	internal static bool ShouldRunOriginal(bool hasFormVfxHolder)
	{
		return hasFormVfxHolder;
	}

#if STS2_110_OR_NEWER
	internal static MethodInfo ResolveAddFormVfxTarget()
	{
		return RequireMethod(
			typeof(NCreatureVisuals),
			nameof(NCreatureVisuals.AddFormVfx),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(NFormVfx));
	}

	internal static MethodInfo ResolveRemoveFormVfxTarget()
	{
		return RequireMethod(
			typeof(NCreatureVisuals),
			nameof(NCreatureVisuals.RemoveFormVfx),
			BindingFlags.Instance | BindingFlags.Public);
	}

	private static bool AddFormVfxPrefix(NCreatureVisuals __instance)
	{
		return ShouldRunOriginal(__instance._formVfxHolder != null);
	}

	private static bool RemoveFormVfxPrefix(NCreatureVisuals __instance)
	{
		return ShouldRunOriginal(__instance._formVfxHolder != null);
	}
#endif
}
