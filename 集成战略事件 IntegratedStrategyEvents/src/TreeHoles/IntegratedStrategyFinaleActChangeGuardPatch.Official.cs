#if STS2_109_OR_NEWER
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
namespace IntegratedStrategyEvents.TreeHoles;

[HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.OnPlayerReady))]
[IntegratedStrategyPatch("IntegratedStrategyFinaleActChangeGuardPatch", "temporary-map", "本模组终局二次换幕")]
internal static class IntegratedStrategyFinaleActChangeGuardPatch
{
	// STS2 0.110.1/0.111.0 的 _lastTransitioningActIndex 会拦同一幕序号的第二次转换。
	// 仅终局插层/建筑师交接允许重置，过期 actIndex 投票仍由原版拒绝。
	[HarmonyPriority(Priority.Normal)]
	private static void Prefix(ActChangeSynchronizer __instance)
	{
		if (IntegratedStrategyTreeHoleController.ShouldAllowRepeatedActTransition()) ResetTransitionMemory(__instance);
	}
	internal static void ResetTransitionMemory(ActChangeSynchronizer synchronizer)
	{
		IntegratedStrategyPrivateMembers.Field(typeof(ActChangeSynchronizer), "_lastTransitioningActIndex")?.SetValue(synchronizer, -1);
	}
}
#endif
