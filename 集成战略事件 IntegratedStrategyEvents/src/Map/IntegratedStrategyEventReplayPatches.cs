using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Map;

[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
[IntegratedStrategyPatch("IntegratedStrategyEventReplayHistoryPatch", "map-rules", "现有全局地图规则")]
internal static class IntegratedStrategyEventReplayHistoryPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(
		RunState __instance,
		MapPointType mapPointType,
		RoomType initialRoomType,
		ModelId? roomModelId)
	{
		return !IntegratedStrategyEventReplay.ShouldSkipReplayHistoryAppend(
			__instance,
			initialRoomType,
			roomModelId);
	}
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.MarkRoomVisited))]
[IntegratedStrategyPatch("IntegratedStrategyEventReplayRoomVisitPatch", "map-rules", "现有全局地图规则")]
internal static class IntegratedStrategyEventReplayRoomVisitPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(RoomType roomType)
	{
		return !IntegratedStrategyEventReplay.ShouldSkipReplayRoomVisit(roomType);
	}
}
