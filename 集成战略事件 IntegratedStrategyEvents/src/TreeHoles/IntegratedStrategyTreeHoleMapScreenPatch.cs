using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleMapScreenPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleMapScreenPatch
{
	private static void Postfix(NMapScreen __instance, ActMap map)
	{

		if (IntegratedStrategyTreeHoleController.TryRestoreCompletedCurrentRun())
		{
			return;
		}

		if (map is IntegratedStrategyTreeHoleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentTreeHoleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPoint(__instance, "_bossPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord, map.BossMapPoint.coord);
			return;
		}

		if (IsEndlessFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord, map.BossMapPoint.coord);
			StyleEndlessFinaleBossNode(__instance);
			EnsureEndlessFinaleBossTravelable(__instance);
			return;
		}

		if (map is IntegratedStrategyEternalDustFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentEternalDustFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
			return;
		}

		if (map is IntegratedStrategyRadiantApexFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentRadiantApexFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
			return;
		}

		if (map is IntegratedStrategyCarefreeViharaFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentCarefreeViharaFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
			return;
		}

		if (map is IntegratedStrategyDesireHallFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentDesireHallFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
			return;
		}

		if (map is IntegratedStrategyAbyssalJungleFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentAbyssalJungleFinaleMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
			return;
		}

		if (map is IntegratedStrategyProphetHornFragmentActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentProphetHornFragmentMap(map))
		{
			HideSpecialPoint(__instance, "_startingPointNode");
			HideSpecialPaths(__instance, map.StartingMapPoint.coord);
		}
	}

	private static void StyleEndlessFinaleBossNode(NMapScreen screen)
	{
		if (IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), "_bossPointNode")?.GetValue(screen) is not NBossMapPoint bossPoint)
		{
			return;
		}

		bossPoint.Position = new Vector2(-80f, -520f);
		bossPoint.Scale = Vector2.One * 2.5f;
		bossPoint.ZIndex = 10;
	}

	internal static void EnsureEndlessFinaleBossTravelable(NMapScreen screen)
	{
		if (IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), "_map")?.GetValue(screen) is not ActMap map ||
			!IsEndlessFinaleMap(map) ||
			IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), "_runState")?.GetValue(screen) is not RunState state ||
			IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), "_bossPointNode")?.GetValue(screen) is not NBossMapPoint bossPoint ||
			state.VisitedMapCoords.Count == 0)
		{
			return;
		}

		MapCoord lastVisitedCoord = state.VisitedMapCoords[state.VisitedMapCoords.Count - 1];
		if (lastVisitedCoord.Equals(map.StartingMapPoint.coord))
		{
			bossPoint.State = MapPointState.Travelable;
		}
	}

	private static bool IsEndlessFinaleMap(ActMap map)
	{
		return map is IntegratedStrategyEndlessFinaleActMap ||
			IntegratedStrategyTreeHoleController.IsCurrentEndlessFinaleMap(map) ||
			HasEndlessFinaleTopology(map);
	}

	// 存档/联机往返后地图会被反序列化为 SavedActMap，类型与会话实例检查都会
	// 失效，导致 BOSS 节点停留在原版硬编码位置（地图区域外左上）。该 7×2、
	// 网格为空、起点(3,0)先古 + BOSS(3,1) 的形状只有阿米娅终局层会出现，
	// 按拓扑识别兜底。
	private static bool HasEndlessFinaleTopology(ActMap map)
	{
		return map is SavedActMap &&
			map.GetColumnCount() == 7 &&
			map.GetRowCount() == 2 &&
			map.BossMapPoint.coord.Equals(new MapCoord(3, 1)) &&
			map.StartingMapPoint.coord.Equals(new MapCoord(3, 0)) &&
			map.StartingMapPoint.PointType == MapPointType.Ancient &&
			!map.GetAllMapPoints().Any();
	}

	private static void HideSpecialPoint(NMapScreen screen, string fieldName)
	{
		if (IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), fieldName)?.GetValue(screen) is CanvasItem item)
		{
			item.Hide();
			item.ProcessMode = Node.ProcessModeEnum.Disabled;
		}
	}

	private static void HideSpecialPaths(NMapScreen screen, params MapCoord[] hiddenCoords)
	{
		object? pathsValue = IntegratedStrategyPrivateMembers.Field(typeof(NMapScreen), "_paths")?.GetValue(screen);
		if (pathsValue is not IDictionary paths)
		{
			return;
		}

		foreach (DictionaryEntry entry in paths)
		{
			if (entry.Key is not ValueTuple<MapCoord, MapCoord> key ||
				(!IsSpecialCoord(key.Item1, hiddenCoords) &&
				 !IsSpecialCoord(key.Item2, hiddenCoords)))
			{
				continue;
			}

			if (entry.Value is not IEnumerable<TextureRect> pathTextures)
			{
				continue;
			}

			foreach (TextureRect pathTexture in pathTextures)
			{
				pathTexture.Hide();
			}
		}
	}

	private static bool IsSpecialCoord(MapCoord coord, IReadOnlyList<MapCoord> hiddenCoords)
	{
		return hiddenCoords.Any(hiddenCoord => coord.Equals(hiddenCoord));
	}
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleMapOpenPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleMapOpenPatch
{
	private static void Postfix()
	{
		if (RunManager.Instance.DebugOnlyGetState()?.Map is { } map)
		{
			}

		IntegratedStrategyTreeHoleController.TryRestoreCompletedCurrentRun();
	}
}

[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleMapTravelabilityPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleMapTravelabilityPatch
{
	private static void Postfix(NMapScreen __instance)
	{
		IntegratedStrategyTreeHoleMapScreenPatch.EnsureEndlessFinaleBossTravelable(__instance);
		// 常驻重试点：终局返回请求若丢失，玩家停留在打开的地图上时该方法会被反复
		// 调用，配合超时重发保证最终能返回大地图。
		IntegratedStrategyTreeHoleController.TryRestoreCompletedCurrentRun();
	}
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleTerminalRewardsProceedPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyTreeHoleTerminalRewardsProceedPatch
{
	private static void Postfix(ref Task __result)
	{
		__result = RestoreTreeHoleAfterTerminalProceed(__result);
	}

	private static async Task RestoreTreeHoleAfterTerminalProceed(Task proceedTask)
	{
		await proceedTask;
		// 无论本次恢复是否成功，terminal proceed 已发生：记录标记并解除读档
		// suppression，让后续 SetMap/Open/RecalculateTravelability 的重试可用。
		IntegratedStrategyTreeHoleController.MarkTerminalRewardsProceededCurrentRun();
		IntegratedStrategyTreeHoleController.TryRestoreCompletedCurrentRunAfterTerminalProceed();
	}
}

// 宝箱房的"前进"按钮直接 NMapScreen.Open()，不走 ProceedFromTerminalRewardsScreen，
// 上面的补丁对宝箱终点永远不触发（玩家反馈：终点为宝箱时无法离开树洞）。
// 在按钮回调上补一个对称入口。
[HarmonyPatch(typeof(NTreasureRoom), "OnProceedButtonReleased")]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleTreasureProceedPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyTreeHoleTreasureProceedPatch
{
	private static void Postfix()
	{
		IntegratedStrategyTreeHoleController.HandleTerminalTreasureRoomProceed();
	}
}

[HarmonyPatch(typeof(NMapScreen), "InitMapPrompt")]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleMapPromptPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleMapPromptPatch
{
	private static void Postfix(NMapScreen __instance)
	{
		if (!IntegratedStrategyTreeHoleController.TryGetCurrentDestination(out string stageLabel, out string actName))
		{
			return;
		}

		ApplyPromptText(__instance, $"{stageLabel}{actName}");
	}

	private static void ApplyPromptText(Node node, string text)
	{
		if (IsPromptNode(node))
		{
			switch (node)
			{
				case MegaRichTextLabel richTextLabel:
					richTextLabel.SetTextAutoSize(text);
					break;
				case MegaLabel megaLabel:
					megaLabel.SetTextAutoSize(text);
					break;
				case Label label:
					label.Text = text;
					break;
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyPromptText(child, text);
		}
	}

	private static bool IsPromptNode(Node node)
	{
		string name = node.Name.ToString();
		return name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) &&
			(node is Label || node is MegaLabel || node is MegaRichTextLabel);
	}
}

[HarmonyPatch(typeof(NActBanner), nameof(NActBanner.Create))]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleActBannerPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleActBannerPatch
{
	private static void Postfix(NActBanner __result)
	{
		IntegratedStrategyTreeHoleActBannerText.Apply(__result);
	}
}

[HarmonyPatch(typeof(NActBanner), nameof(NActBanner._Ready))]
[IntegratedStrategyPatch("IntegratedStrategyTreeHoleActBannerReadyPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyTreeHoleActBannerReadyPatch
{
	private static void Postfix(NActBanner __instance)
	{
		IntegratedStrategyTreeHoleActBannerText.Apply(__instance);
	}
}

internal static class IntegratedStrategyTreeHoleActBannerText
{
	public static void Apply(NActBanner banner)
	{
		if (!IntegratedStrategyTreeHoleController.TryGetCurrentDestination(out string stageLabel, out string actName))
		{
			return;
		}

		SetMegaLabelField(banner, "_actNumber", stageLabel);
		SetMegaLabelField(banner, "_actName", actName);
	}

	private static void SetMegaLabelField(NActBanner banner, string fieldName, string text)
	{
		if (IntegratedStrategyPrivateMembers.Field(typeof(NActBanner), fieldName)?.GetValue(banner) is MegaLabel label)
		{
			label.SetTextAutoSize(text);
		}
	}
}
