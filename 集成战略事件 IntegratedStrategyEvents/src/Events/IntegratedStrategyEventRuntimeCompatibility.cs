using Godot;
using HarmonyLib;
using IntegratedStrategyEvents.Compatibility;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace IntegratedStrategyEvents.Events;

internal static class IntegratedStrategyEventRuntimeCompatibility
{
	private static readonly IntegratedStrategyLocMerge LocMerge =
		new("events", "event", BuildEventLocalization);

	public static void Install()
	{
		LocMerge.Install();
	}

	internal static void MergeCurrentEventLocalization()
	{
		LocMerge.Merge();
	}

	private static Dictionary<string, string> BuildEventLocalization()
	{
		Dictionary<string, string> entries = new(StringComparer.Ordinal);
		foreach ((Type eventType, Func<List<(string, string)>?> createLocalization) in IntegratedStrategyContentCatalog.EventDefinitions)
		{
			// 用 ModelDb 的真实 entry 作前缀：RitsuLib 注册的事件会拿到带 mod 前缀的固定 entry，
			// 仅 Inject 的事件保持原版 slug，两种情况都与游戏查表用的 Id.Entry 一致。
			string eventKey = ModelDb.GetEntry(eventType);
			foreach ((string relativeKey, string value) in IntegratedStrategyRichText.ApplyFontSizes(createLocalization()) ?? [])
			{
				entries[$"{eventKey}.{relativeKey}"] = value;
			}
		}

		return entries;
	}
}

[HarmonyPatch(typeof(EventOption), "AddLocVars")]
[IntegratedStrategyPatch("IntegratedStrategyEventOptionAddLocVarsPatch", "content", "本模组内容")]
internal static class IntegratedStrategyEventOptionAddLocVarsPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(EventOption __instance, EventModel eventModel)
	{
		if (eventModel is not IntegratedStrategyEventModel)
		{
			return true;
		}

		if (__instance.Description != null)
		{
			eventModel.Owner?.Character?.AddDetailsTo(__instance.Description);
			__instance.Description.Add("IsMultiplayer", eventModel.Owner != null && eventModel.Owner.RunState.Players.Count > 1);
		}

		return false;
	}
}
