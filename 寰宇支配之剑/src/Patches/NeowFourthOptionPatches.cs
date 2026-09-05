using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace UniversalDominionSword.Patches;

/// <summary>涅奥固定的第四个先古遗物选项。两个后缀只在原结果上追加,从不阻止原方法。</summary>
internal static class NeowSwordOption
{
	private const string PositiveDonePage = "NEOW.pages.DONE.POSITIVE.description";

	internal static bool Contains(IEnumerable<EventOption> options)
	{
		return options.Any(option => option.Relic is UniversalDominionSwordRelic);
	}

	internal static EventOption? TryCreate(Neow neow)
	{
		if (VanillaMembers.AncientRelicOption == null)
		{
			return null;
		}

		RelicModel relic = ModelDb.Relic<UniversalDominionSwordRelic>().ToMutable();
		return VanillaMembers.AncientRelicOption.Invoke(neow, [relic, "INITIAL", PositiveDonePage]) as EventOption;
	}
}

[SwordPatch("neow.initial-options", "涅奥第四个先古遗物选项")]
[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class NeowInitialOptionsPatch
{
	[HarmonyPrepare]
	private static bool Prepare() => VanillaMembers.AncientRelicOption != null;

	[HarmonyPostfix]
	private static void Postfix(Neow __instance, ref IReadOnlyList<EventOption> __result)
	{
		// 带修正器(挑战/自定义)的局保持原版三选项;修正器局的选项集合由原版决定。
		if (__instance.Owner == null
			|| __instance.Owner.RunState.Modifiers.Count > 0
			|| NeowSwordOption.Contains(__result))
		{
			return;
		}

		EventOption? option = NeowSwordOption.TryCreate(__instance);
		if (option == null)
		{
			return;
		}

		List<EventOption> options = [.. __result, option];
		__result = options;
		Log.Info($"[{ModInfo.Id}] Added the Universal Dominion Sword as Neow's fixed fourth option.");
	}
}

[SwordPatch("neow.all-possible-options", "涅奥第四个先古遗物选项(图鉴/预览列表)")]
[HarmonyPatch(typeof(Neow), nameof(Neow.AllPossibleOptions), MethodType.Getter)]
internal static class NeowAllPossibleOptionsPatch
{
	[HarmonyPrepare]
	private static bool Prepare() => VanillaMembers.AncientRelicOption != null;

	[HarmonyPostfix]
	private static void Postfix(Neow __instance, ref IEnumerable<EventOption> __result)
	{
		if (NeowSwordOption.Contains(__result))
		{
			return;
		}

		EventOption? option = NeowSwordOption.TryCreate(__instance);
		if (option == null)
		{
			return;
		}

		List<EventOption> options = [.. __result, option];
		__result = options;
	}
}
