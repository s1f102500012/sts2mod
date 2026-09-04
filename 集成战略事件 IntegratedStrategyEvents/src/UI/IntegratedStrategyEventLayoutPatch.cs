using Godot;
using HarmonyLib;
using IntegratedStrategyEvents.Events;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace IntegratedStrategyEvents.UI;

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
[IntegratedStrategyPatch("IntegratedStrategyEventLayoutPatch", "event-ui", "本模组事件界面")]
internal static class IntegratedStrategyEventLayoutPatch
{
	private static void Prefix(NEventLayout __instance)
	{
		IntegratedStrategyEventModel? strategyEvent = IntegratedStrategyEventLayout.GetIntegratedStrategyEvent(__instance);
		if (strategyEvent == null)
		{
			return;
		}

		IntegratedStrategyEventLayoutApplier.ResetBeforeOptionsAdded(__instance, strategyEvent);
	}

	private static void Postfix(NEventLayout __instance)
	{
		IntegratedStrategyEventModel? strategyEvent = IntegratedStrategyEventLayout.GetIntegratedStrategyEvent(__instance);
		if (strategyEvent == null)
		{
			return;
		}

		Callable
			.From(() => IntegratedStrategyEventLayoutApplier.ApplyAfterOptionsAdded(__instance, strategyEvent))
			.CallDeferred();
	}
}
