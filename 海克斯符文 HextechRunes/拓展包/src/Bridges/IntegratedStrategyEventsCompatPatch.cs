using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HextechRunesSponsorPack;

// 合唱团(集成战略事件的 FinalChorale)投影遗物允许当前 HP 超过上限:原版 SetCurrentHpInternal 会把值截到 MaxHp,
// 这里在写入前把上限抬到同一数值。判定只对合唱团生效,其他生物一律直接返回。
// 「HP 可超上限」的规则本应属于 ISE 自己的 FinalChorale(见重构方案 §6.3),在 ISE 开出 Interop 之前维持现状。
[SponsorPatch("ise.chorale-overheal", "集成战略事件兼容", Optional = true)]
[HarmonyPatch(typeof(Creature), nameof(Creature.SetCurrentHpInternal), [typeof(decimal)])]
internal static class IntegratedStrategyEventsCompatPatch
{
	[HarmonyPrefix]
	private static void SetCurrentHpInternalPrefix(Creature __instance, decimal amount)
	{
		if (amount <= __instance.MaxHp || !IntegratedStrategyEventsBridge.IsFinalChorale(__instance))
		{
			return;
		}

		__instance.SetMaxHpInternal(amount);
	}
}
