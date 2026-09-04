using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace IntegratedStrategyEvents.UI;

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.SetPortrait))]
[IntegratedStrategyPatch("IntegratedStrategyEventPortraitPatch", "event-ui", "本模组事件界面")]
internal static class IntegratedStrategyEventPortraitPatch
{
	private static void Postfix(NEventLayout __instance)
	{
		if (!IntegratedStrategyEventLayout.IsIntegratedStrategyEvent(__instance))
		{
			return;
		}

		Callable.From(() => IntegratedStrategyEventPortraitFitter.ApplyWithDriver(__instance)).CallDeferred();
	}
}
