using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

// 神迹事件的「锻造器售价」本局临时修正:主 mod 的 HextechForgeShopPriceHelper.GetRandomForgeShopPriceFor
// 是 internal,拓展包用 Harmony(反射定位)postfix,把本局 BelieverRune 累计的售价修正叠加到算出的价格上。
// 纯拓展包,主 mod 源码一行不动。
// 目标按全名字符串 TypeByName("HextechRunes.HextechForgeShopPriceHelper") 定位:主 mod 若把这个 internal 类
// 挪进子命名空间,本补丁会静默失效(Install 只 Log.Warn)。
// 目标是本体的 internal 类,只能在运行时按全名枚举,所以这是"动态目标"补丁:只带 [SponsorPatch] + Apply(Harmony)。
// 目标缺失(本体挪走了这个 internal 类)时抛出,由 SponsorPatcher 按 Optional 记 Info 并列进启动摘要的失败项——
// 比原先各自 Log.Warn 更显形:补丁不会静默消失,但也不会污染玩家日志的 Warn 级别。
[SponsorPatch("believer.forge-price", "信徒·锻造器售价修正", Optional = true)]
internal static class MiracleEventForgePricePatch
{
	internal static void Apply(Harmony harmony)
	{
		Type? helper = AccessTools.TypeByName("HextechRunes.HextechForgeShopPriceHelper");
		MethodInfo? target = helper == null
			? null
			: AccessTools.Method(helper, "GetRandomForgeShopPriceFor", [ typeof(RunState) ]);
		if (target == null)
		{
			throw new MissingMethodException("HextechRunes.HextechForgeShopPriceHelper", "GetRandomForgeShopPriceFor");
		}

		harmony.Patch(target, postfix: new HarmonyMethod(typeof(MiracleEventForgePricePatch), nameof(Postfix)));
		Log.Info($"[{ModInfo.Id}] Miracle forge-price patch installed on {target.DeclaringType?.Name}.{target.Name}.");
	}

	private static void Postfix(RunState runState, ref int __result)
	{
		// 商店算价(ModifyMerchantPrice)可能传 null 的 runState(shopRelic.Owner 为 null) —— 此时从 RunManager 兜底取本局,
		// 否则会漏掉售价修正、显示成基础价。
		RunState? state = runState ?? GetActiveRunState();
		if (state == null)
		{
			return;
		}

		int delta = state.Players
			.SelectMany(player => player.Relics)
			.OfType<BelieverRune>()
			.Sum(believer => believer.ForgePriceDelta);
		if (delta != 0)
		{
			__result = Math.Max(0, __result + delta);
		}
	}

	private static RunState? GetActiveRunState()
	{
		try
		{
			return RunManager.Instance?.DebugOnlyGetState() as RunState;
		}
		catch
		{
			return null;
		}
	}
}
