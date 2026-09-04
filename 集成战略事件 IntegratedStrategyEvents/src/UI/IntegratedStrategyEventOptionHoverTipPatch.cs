using HarmonyLib;
using IntegratedStrategyEvents.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace IntegratedStrategyEvents.UI;

[HarmonyPatch(typeof(NEventOptionButton), "OnFocus")]
[IntegratedStrategyPatch("IntegratedStrategyEventOptionHoverTipPatch", "event-ui", "本模组事件界面")]
internal static class IntegratedStrategyEventOptionHoverTipPatch
{
	private static void Postfix(NEventOptionButton __instance)
	{
		if (!ShouldAlignHoverTipsRight(__instance.Event) || !__instance.Option.HoverTips.Any())
		{
			return;
		}

		NHoverTipSet.Remove(__instance);
		NHoverTipSet.CreateAndShow(__instance, __instance.Option.HoverTips, HoverTipAlignment.Right);
	}

	private static bool ShouldAlignHoverTipsRight(EventModel eventModel)
	{
		return eventModel is IntegratedStrategyEventModel { AlignHoverTipsRight: true };
	}
}
