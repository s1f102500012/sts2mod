using HarmonyLib;
using IntegratedStrategyEvents.Encounters;
using IntegratedStrategyEvents.Events;
using MegaCrit.Sts2.Core.Localization;

namespace IntegratedStrategyEvents;

// 遭遇表与事件表共用一个后缀：同一目标只登记一次补丁，合并顺序即原先两个补丁的执行序。
[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
[IntegratedStrategyPatch("IntegratedStrategyLocManagerInitializePatch", "content", "本模组内容")]
internal static class IntegratedStrategyLocManagerInitializePatch
{
	private static void Postfix()
	{
		IntegratedStrategyEncounterLocalization.Install();
		IntegratedStrategyEventRuntimeCompatibility.Install();
	}
}
