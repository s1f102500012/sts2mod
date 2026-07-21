using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.Relics;
using IntegratedStrategyEvents.TreeHoles;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Map;

internal static class IntegratedStrategySecretMapNodeController
{
	private const string SecretMapNodesRngName = "integrated_strategy_secret_map_nodes";

	private const string SecretTreeHoleEventRngName = "integrated_strategy_secret_tree_hole_event";

	private const int MinimumSecretNodes = 1;
	private const int MaximumSecretNodes = 3;
	private const string SecretIconPath = $"res://{ModInfo.ModId}/images/map/map_secret.png";
	private const string SecretOutlinePath = $"res://{ModInfo.ModId}/images/map/map_secret_outline.png";
	private static readonly Vector2 SecretMapIconSize = new(48f, 48f);
	private static readonly Vector2 SecretMapOutlineSize = new(54f, 54f);
	private static readonly Vector2 SecretMapHoverScale = Vector2.One * 1.45f;
	public const string SecretLegendItemName = "SecretLegendItem";
	public const MapPointType SecretLegendHighlightPointType = MapPointType.Boss;
	// 秘境节点可强制刷新的树洞事件登记表：类型 + 全员准入门槛 + 换入 RoomSet 的委托，
	// 新增秘境事件只需在此加一行。
	private static readonly SecretNodeEvent[] TreeHoleEvents =
	[
		new(typeof(ForwardForestEvent), ForwardForestEvent.CanEnterTreeHoleForAllPlayers),
		new(typeof(StoryToBeToldEvent)),
		new(typeof(ShiftingCityEvent)),
		new(typeof(GlimpseEvent), GlimpseEvent.CanEnterTreeHoleForAllPlayers)
	];

	private static readonly Action<List<EventModel>, int> SwapToTruthToBeTold =
		CreateSwapDelegate(typeof(TruthToBeToldEvent));

	private sealed record SecretNodeEvent(Type EventType, Func<RunState, bool>? CanEnterForAllPlayers = null)
	{
		public Action<List<EventModel>, int> SwapIntoEvents { get; } = CreateSwapDelegate(EventType);
	}

	private static Action<List<EventModel>, int> CreateSwapDelegate(Type eventType)
	{
		return AccessTools.Method(typeof(RoomSet), nameof(RoomSet.SwapToOrCreateAtIndex))
			.MakeGenericMethod(typeof(EventModel), eventType)
			.CreateDelegate<Action<List<EventModel>, int>>();
	}

	private static readonly Lazy<HashSet<ModelId>> SecretNodeForcedEventIds = new(static () =>
	{
		HashSet<ModelId> ids = [.. TreeHoleEvents.Select(static entry => ModelDb.GetId(entry.EventType))];
		ids.Add(ModelDb.GetId(typeof(TruthToBeToldEvent)));
		return ids;
	});

	/// <summary>该事件 id 是否为秘境节点强制刷新的树洞类事件（用于判定它不占用普通事件节点额度）。</summary>
	public static bool IsSecretNodeForcedEventId(ModelId modelId)
	{
		return SecretNodeForcedEventIds.Value.Contains(modelId);
	}

	private static readonly ConditionalWeakTable<RunState, SecretNodeStore> SecretNodes = new();
	private static Texture2D? _secretIcon;
	private static Texture2D? _secretOutline;

	public static void MarkSecretNodes(IRunState runState, ActMap map, int actIndex)
	{
		if (runState is not RunState state || ShouldSkipMap(state, map))
		{
			return;
		}

		SecretNodeStore store = SecretNodes.GetOrCreateValue(state);
		HashSet<MapCoord> selectedCoords = SelectSecretNodeCoords(state, map, actIndex);
		store.Set(actIndex, selectedCoords);
		if (selectedCoords.Count > 0)
		{
			Log.Info(
				$"{ModInfo.LogPrefix} Marked {selectedCoords.Count} secret map node(s) " +
				$"in act {actIndex}: {string.Join(", ", selectedCoords.Select(FormatCoord))}.");
		}
	}

	public static bool IsAtSecretNode(RunState state)
	{
		return TryGetCurrentUnknownMapPoint(state, out MapPoint point) &&
			IsSecretNode(state, state.Map, state.CurrentActIndex, point.coord);
	}

	public static bool IsAtProphetHornSecretNode(RunState state)
	{
		return TryGetCurrentUnknownMapPoint(state, out MapPoint point) &&
			TryGetProphetHornSecretNodeCoord(state, state.Map, state.CurrentActIndex, out MapCoord coord) &&
			point.coord.Equals(coord);
	}

	public static bool IsAtSecretNodeCurrentRun()
	{
		return RunManager.Instance.DebugOnlyGetState() is RunState state &&
			IsAtSecretNode(state);
	}

	public static bool TryApplySecretNodeIcon(NNormalMapPoint mapPointNode)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		MapPoint? point = mapPointNode.Point;
		if (state == null || point == null || !IsSecretNode(state, point))
		{
			return false;
		}

		Texture2D? icon = GetSecretIcon();
		Texture2D? outline = GetSecretOutline();
		if (icon == null || outline == null)
		{
			return false;
		}

		TextureRect iconRect = IntegratedStrategyMapReflectionCache.NormalMapPointIcon(mapPointNode);
		TextureRect outlineRect = IntegratedStrategyMapReflectionCache.NormalMapPointOutline(mapPointNode);
		iconRect.Texture = icon;
		outlineRect.Texture = outline;
		Vector2 center = GetIconContainerCenter(mapPointNode, iconRect);
		ResizeAroundCenter(iconRect, SecretMapIconSize, center);
		ResizeSecretOutlineAroundIconCenter(outlineRect, iconRect);
		ApplySecretNodeOpacity(mapPointNode, iconRect, outlineRect);
		return true;
	}

	public static bool TryApplySecretNodeOpacity(NNormalMapPoint mapPointNode)
	{
		if (!IsSecretMapPointNode(mapPointNode))
		{
			return false;
		}

		ApplySecretNodeOpacity(
			mapPointNode,
			IntegratedStrategyMapReflectionCache.NormalMapPointIcon(mapPointNode),
			IntegratedStrategyMapReflectionCache.NormalMapPointOutline(mapPointNode));
		return true;
	}

	public static bool ApplySecretNodeHighlightOpacity(NNormalMapPoint mapPointNode, bool highlighted)
	{
		if (!IsSecretMapPointNode(mapPointNode))
		{
			return false;
		}

		ApplySecretNodeOpacity(
			mapPointNode,
			IntegratedStrategyMapReflectionCache.NormalMapPointIcon(mapPointNode),
			IntegratedStrategyMapReflectionCache.NormalMapPointOutline(mapPointNode),
			highlighted);
		return true;
	}

	public static bool TryAnimateSecretNodeHover(NNormalMapPoint mapPointNode)
	{
		return TryAnimateSecretNode(mapPointNode, hovered: true);
	}

	public static bool TryAnimateSecretNodeUnhover(NNormalMapPoint mapPointNode)
	{
		return TryAnimateSecretNode(mapPointNode, hovered: false);
	}

	// 对应原版 NNormalMapPoint.AnimHover/AnimUnhover：悬停快速放大(0.05s线性)，
	// 离开慢速回弹(0.5s Cubic-Out)；秘境节点的差异只在图标/描边贴图与不透明度处理。
	private static bool TryAnimateSecretNode(NNormalMapPoint mapPointNode, bool hovered)
	{
		if (!IsSecretMapPointNode(mapPointNode))
		{
			return false;
		}

		TextureRect iconRect = IntegratedStrategyMapReflectionCache.NormalMapPointIcon(mapPointNode);
		TextureRect questIconRect = IntegratedStrategyMapReflectionCache.NormalMapPointQuestIcon(mapPointNode);
		TextureRect outlineRect = IntegratedStrategyMapReflectionCache.NormalMapPointOutline(mapPointNode);
		ref Tween? tweenRef = ref IntegratedStrategyMapReflectionCache.NormalMapPointTween(mapPointNode);
		tweenRef?.Kill();
		Tween tween = mapPointNode.CreateTween().SetParallel();
		tweenRef = tween;

		double duration = hovered ? 0.05 : 0.5;
		Vector2 targetScale = hovered ? SecretMapHoverScale : Vector2.One;
		Color iconSelfModulate = hovered ? Colors.White : GetIconSelfModulate(mapPointNode, highlighted: false);
		iconRect.Modulate = Colors.White;

		PropertyTweener[] tweeners =
		[
			tween.TweenProperty(iconRect, "scale", targetScale, duration),
			tween.TweenProperty(questIconRect, "scale", targetScale, duration),
			tween.TweenProperty(iconRect, "self_modulate", iconSelfModulate, duration),
			tween.TweenProperty(outlineRect, "modulate", GetOpaqueMapBackgroundColor(), duration)
		];
		if (!hovered)
		{
			foreach (PropertyTweener tweener in tweeners)
			{
				tweener.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			}
		}

		ApplySecretOutlineBaseOpacity(mapPointNode, outlineRect);
		return true;
	}

	public static bool IsSecretMapPointNode(NNormalMapPoint mapPointNode)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		MapPoint? point = mapPointNode.Point;
		return state != null && point != null && IsSecretNode(state, point);
	}

	public static bool IsSecretLegendHighlightType(MapPointType pointType)
	{
		return pointType == SecretLegendHighlightPointType;
	}

	public static bool TryForceNextTreeHoleEvent(RoomSet roomSet, RunState state)
	{
		if (!IsAtSecretNode(state) || roomSet.events.Count == 0)
		{
			return false;
		}

		if (IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
				roomSet,
				state,
				IntegratedStrategyEventReplay.IsSecondActOpeningBranch,
				"saved second-act opening event on secret node"))
		{
			return true;
		}

		int desiredIndex = roomSet.eventsVisited % roomSet.events.Count;
		if (IsAtProphetHornSecretNode(state))
		{
			if (IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
					roomSet,
					state,
					static eventModel => eventModel is TruthToBeToldEvent,
					"saved Prophet Horn secret node event"))
			{
				return true;
			}

			SwapToTruthToBeTold(roomSet.events, desiredIndex);
			Log.Info($"{ModInfo.LogPrefix} Prophet Horn secret map node forced next event to TRUTH_TO_BE_TOLD.");
			return true;
		}

		if (IntegratedStrategyEventReplay.TryRestoreSavedCurrentEvent(
				roomSet,
				state,
				IntegratedStrategyEventReplay.IsTreeHoleEvent,
				"saved secret node event"))
		{
			return true;
		}

		SecretNodeEvent selected = SelectTreeHoleEvent(state);
		selected.SwapIntoEvents(roomSet.events, desiredIndex);
		Log.Info(
			$"{ModInfo.LogPrefix} Secret map node forced next event to " +
			$"{ModelDb.GetId(selected.EventType).Entry}.");
		return true;
	}

	public static bool TryGetForcedEventType(RunState state, out Type eventType)
	{
		if (!IsAtSecretNode(state))
		{
			eventType = null!;
			return false;
		}

		if (IsAtProphetHornSecretNode(state))
		{
			eventType = typeof(TruthToBeToldEvent);
			return true;
		}

		eventType = SelectTreeHoleEvent(state).EventType;
		return true;
	}

	private static bool IsSecretNode(RunState state, ActMap map, int actIndex, MapCoord coord)
	{
		if (ShouldSkipMap(state, map) ||
			map.GetPoint(coord) is not { PointType: MapPointType.Unknown })
		{
			return false;
		}

		SecretNodeStore store = SecretNodes.GetOrCreateValue(state);
		if (!store.ContainsAct(actIndex))
		{
			store.Set(actIndex, SelectSecretNodeCoords(state, map, actIndex));
		}

		return store.Contains(actIndex, coord);
	}

	private static bool IsSecretNode(RunState state, MapPoint point)
	{
		return point.PointType == MapPointType.Unknown &&
			IsSecretNode(state, state.Map, state.CurrentActIndex, point.coord);
	}

	private static bool TryGetCurrentUnknownMapPoint(RunState state, out MapPoint point)
	{
		point = null!;
		if (!state.CurrentMapCoord.HasValue)
		{
			return false;
		}

		MapCoord coord = state.CurrentMapCoord.Value;
		if (state.Map.GetPoint(coord) is not { PointType: MapPointType.Unknown } currentPoint)
		{
			return false;
		}

		point = currentPoint;
		return true;
	}

	private static readonly Assembly VanillaMapAssembly = typeof(ActMap).Assembly;
	private static readonly Assembly SelfAssembly = typeof(IntegratedStrategySecretMapNodeController).Assembly;

	internal static bool ShouldSkipMapMutation(IRunState runState, ActMap map)
	{
		// 秘境节点只出现在正常大地图上：
		// 1) 本模组的树洞/终局/断章临时层由标记接口+会话判定排除；
		// 2) 其他模组的脚本化/临时地图类型（非原版、非本模组程序集）默认排除（玩家反馈）；
		// 3) 其他模组也可通过 IntegratedStrategyEventsInterop.RegisterSecretNodeSkipPredicate 显式声明排除。
		return IntegratedStrategyTreeHoleController.IsTemporaryMap(runState, map) ||
			IsExternalScriptedMap(map) ||
			IntegratedStrategyEventsInterop.ShouldSkipSecretNodes(map);
	}

	private static bool ShouldSkipMap(RunState state, ActMap map)
	{
		return ShouldSkipMapMutation(state, map);
	}

	private static bool IsExternalScriptedMap(ActMap map)
	{
		Assembly mapAssembly = map.GetType().Assembly;
		return mapAssembly != VanillaMapAssembly && mapAssembly != SelfAssembly;
	}

	private static HashSet<MapCoord> SelectSecretNodeCoords(RunState state, ActMap map, int actIndex)
	{
		if (TryGetProphetHornSecretNodeCoord(state, map, actIndex, out MapCoord prophetHornCoord))
		{
			return [prophetHornCoord];
		}

		List<MapPoint> candidates = map.GetAllMapPoints()
			.Where(static point => point.PointType == MapPointType.Unknown && point.CanBeModified)
			.OrderBy(static point => point.coord.row)
			.ThenBy(static point => point.coord.col)
			.ToList();
		if (candidates.Count == 0)
		{
			return [];
		}

		Rng rng = new(CreateSecretNodeSeed(state, actIndex), SecretMapNodesRngName);
		rng.Shuffle(candidates);
		int maxCount = Math.Min(MaximumSecretNodes, candidates.Count);
		int count = rng.NextInt(MinimumSecretNodes, maxCount + 1);
		return candidates.Take(count).Select(static point => point.coord).ToHashSet();
	}

	private static SecretNodeEvent SelectTreeHoleEvent(RunState state)
	{
		MapCoord coord = state.CurrentMapCoord ?? default;
		uint seed = IntegratedStrategyStableRng.CreateSeed(
			state.Rng.Seed,
			SecretTreeHoleEventRngName,
			unchecked((uint)state.CurrentActIndex),
			IntegratedStrategyStableRng.HashCoord(coord));
		Rng rng = new(seed, SecretTreeHoleEventRngName);
		SecretNodeEvent[] available = TreeHoleEvents
			.Where(entry => entry.CanEnterForAllPlayers?.Invoke(state) ?? true)
			.ToArray();
		SecretNodeEvent[] candidates = available
			.Where(entry => !state.VisitedEventIds.Contains(ModelDb.GetId(entry.EventType)))
			.ToArray();

		if (candidates.Length == 0)
		{
			candidates = available;
		}

		return candidates[rng.NextInt(candidates.Length)];
	}

	private static uint CreateSecretNodeSeed(RunState state, int actIndex)
	{
		return IntegratedStrategyStableRng.CreateSeed(
			state.Rng.Seed,
			SecretMapNodesRngName,
			unchecked((uint)actIndex));
	}

	private static bool TryGetProphetHornSecretNodeCoord(
		RunState state,
		ActMap map,
		int actIndex,
		out MapCoord coord)
	{
		coord = default;
		return actIndex == ProphetHornRelic.TargetActIndex &&
			ProphetHornRelic.IsActiveInRun(state) &&
			ProphetHornActMap.TryGetSecretCoord(map, out coord);
	}

	public static Texture2D? GetSecretIcon()
	{
		return _secretIcon ??= LoadTexture(SecretIconPath);
	}

	private static Texture2D? GetSecretOutline()
	{
		return _secretOutline ??= LoadTexture(SecretOutlinePath);
	}

	private static Texture2D? LoadTexture(string path)
	{
		Texture2D? texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
		if (texture == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to load secret map node texture: {path}");
		}

		return texture;
	}

	private static string FormatCoord(MapCoord coord)
	{
		return $"({coord.col},{coord.row})";
	}

	private static void ResizeAroundCenter(TextureRect rect, Vector2 targetSize)
	{
		ResizeAroundCenter(rect, targetSize, rect.Position + rect.Size * 0.5f);
	}

	private static void ResizeAroundCenter(TextureRect rect, Vector2 targetSize, Vector2 center)
	{
		rect.CustomMinimumSize = targetSize;
		rect.Size = targetSize;
		rect.Position = center - targetSize * 0.5f;
		rect.PivotOffset = targetSize * 0.5f;
		rect.Scale = Vector2.One;
	}

	private static void ResizeSecretOutlineAroundIconCenter(TextureRect outlineRect, TextureRect iconRect)
	{
		// Outline is a child of Icon in the vanilla map point scene.
		ResizeAroundCenter(outlineRect, SecretMapOutlineSize, iconRect.Size * 0.5f);
	}

	private static Vector2 GetIconContainerCenter(NNormalMapPoint mapPointNode, TextureRect iconRect)
	{
		if (iconRect.GetParent() is Control iconContainer && iconContainer.Size != Vector2.Zero)
		{
			return iconContainer.Size * 0.5f;
		}

		if (mapPointNode.Size != Vector2.Zero)
		{
			return mapPointNode.Size * 0.5f;
		}

		return mapPointNode.PivotOffset != Vector2.Zero ? mapPointNode.PivotOffset : new Vector2(28f, 28f);
	}

	private static void ApplySecretNodeOpacity(
		NNormalMapPoint mapPointNode,
		TextureRect iconRect,
		TextureRect outlineRect,
		bool highlighted = false)
	{
		iconRect.Modulate = Colors.White;
		iconRect.SelfModulate = GetIconSelfModulate(mapPointNode, highlighted);
		outlineRect.Modulate = GetOpaqueMapBackgroundColor();
		ApplySecretOutlineBaseOpacity(mapPointNode, outlineRect);
	}

	private static void ApplySecretOutlineBaseOpacity(NNormalMapPoint mapPointNode, TextureRect outlineRect)
	{
		ref Color outlineColorRef = ref IntegratedStrategyMapReflectionCache.MapPointOutlineColor(mapPointNode);
		Color outlineColor = outlineColorRef;
		outlineColor.A = 1f;
		outlineColorRef = outlineColor;

		Color modulate = outlineRect.Modulate;
		modulate.A = 1f;
		outlineRect.Modulate = modulate;

		Color selfModulate = outlineRect.SelfModulate;
		selfModulate.A = 1f;
		outlineRect.SelfModulate = selfModulate;
	}

	private static Color GetIconSelfModulate(NNormalMapPoint mapPointNode, bool highlighted)
	{
		return highlighted || mapPointNode.State is MapPointState.Travelable or MapPointState.Traveled
			? Colors.White
			: StsColors.halfTransparentWhite;
	}

	private static Color GetOpaqueMapBackgroundColor()
	{
		Color color = RunManager.Instance.DebugOnlyGetState()?.Act.MapBgColor ?? Colors.White;
		color.A = 1f;
		return color;
	}

	private sealed class SecretNodeStore
	{
		private readonly Dictionary<int, HashSet<MapCoord>> _coordsByAct = [];

		public bool ContainsAct(int actIndex)
		{
			return _coordsByAct.ContainsKey(actIndex);
		}

		public void Set(int actIndex, HashSet<MapCoord> coords)
		{
			_coordsByAct[actIndex] = coords;
		}

		public bool Contains(int actIndex, MapCoord coord)
		{
			return _coordsByAct.TryGetValue(actIndex, out HashSet<MapCoord>? coords) &&
				coords.Contains(coord);
		}
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterMapGenerated))]
internal static class IntegratedStrategyTreeHoleEarlyRestorePatch
{
	[HarmonyPriority(Priority.First)]
	private static void Prefix(IRunState runState, ActMap map, int actIndex)
	{
		// 此时 GenerateMap 的返回值尚未赋给 RunState.Map；需要重建的旧树洞图留到
		// NMapScreen.SetMap 后处理，避免这里写入的替代地图随后被原版覆盖。
		IntegratedStrategyTreeHoleController.TryRestoreSavedSessionForCurrentRun(
			map,
			allowMapRebuild: false);
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterMapGenerated))]
internal static class IntegratedStrategySecretMapNodeGenerationPatch
{
	private static void Prefix(IRunState runState, ActMap map, int actIndex)
	{
		IntegratedStrategySecretMapNodeController.MarkSecretNodes(runState, map, actIndex);
	}
}

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
internal static class IntegratedStrategySecretMapNodeEventPatch
{
	[HarmonyPriority(Priority.First)]
	private static bool Prefix(RoomSet __instance, RunState runState)
	{
		if (!IntegratedStrategySecretMapNodeController.IsAtSecretNode(runState))
		{
			return true;
		}

		// 秘境节点上树洞事件优先；二层结局分支由 ShouldForceSecondActOpeningEvent
		// 自行避开秘境节点并顺延到首个非秘境事件节点。
		return !IntegratedStrategySecretMapNodeController.TryForceNextTreeHoleEvent(__instance, runState);
	}
}

[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
internal static class IntegratedStrategySecretMapNodeIconPatch
{
	private static void Postfix(NNormalMapPoint __instance)
	{
		IntegratedStrategySecretMapNodeController.TryApplySecretNodeIcon(__instance);
	}
}

[HarmonyPatch(typeof(NNormalMapPoint), "RefreshColorInstantly")]
internal static class IntegratedStrategySecretMapNodeColorPatch
{
	private static void Postfix(NNormalMapPoint __instance)
	{
		IntegratedStrategySecretMapNodeController.TryApplySecretNodeOpacity(__instance);
	}
}

[HarmonyPatch(typeof(NNormalMapPoint), "AnimHover")]
internal static class IntegratedStrategySecretMapNodeHoverPatch
{
	private static bool Prefix(NNormalMapPoint __instance)
	{
		return !IntegratedStrategySecretMapNodeController.TryAnimateSecretNodeHover(__instance);
	}
}

[HarmonyPatch(typeof(NNormalMapPoint), "AnimUnhover")]
internal static class IntegratedStrategySecretMapNodeUnhoverPatch
{
	private static bool Prefix(NNormalMapPoint __instance)
	{
		return !IntegratedStrategySecretMapNodeController.TryAnimateSecretNodeUnhover(__instance);
	}
}
