using IntegratedStrategyEvents.Encounters;
using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.TreeHoles;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Map;

internal static class IntegratedStrategyForcedRoomController
{
	private static readonly AccessTools.FieldRef<RunState, HashSet<ModelId>> VisitedEventIdsRef =
		AccessTools.FieldRefAccess<RunState, HashSet<ModelId>>("_visitedEventIds");

	public static bool HandleCreateRoom(
		RoomType roomType,
		MapPointType mapPointType,
		AbstractModel? model,
		ref AbstractRoom result)
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState state)
		{
			return true;
		}

		if (TryCreateForcedPetalEliteRoom(state, roomType, model, out CombatRoom combatRoom))
		{
			result = combatRoom;
			return false;
		}

		if (TryCreateForcedEventRoom(state, roomType, mapPointType, model, out EventRoom eventRoom))
		{
			result = eventRoom;
			return false;
		}

		return true;
	}

	private static bool TryCreateForcedPetalEliteRoom(
		RunState state,
		RoomType roomType,
		AbstractModel? model,
		out CombatRoom room)
	{
		room = null!;
		if (!PetalSpecialEliteNodeController.TryPullForcedEncounter(roomType, out EncounterModel encounter))
		{
			return false;
		}

		LogForcedModelReplacement("Petal special elite node", model, encounter);
		room = new CombatRoom(encounter.ToMutable(), state);
		return true;
	}

	private static bool TryCreateForcedEventRoom(
		RunState state,
		RoomType roomType,
		MapPointType mapPointType,
		AbstractModel? model,
		out EventRoom room)
	{
		room = null!;
		if (roomType != RoomType.Event ||
			!TryGetForcedEventType(state, out Type forcedEventType))
		{
			return false;
		}

		// 原版每幕入口是先古(Ancient)节点，其 Event 房间必须走 PullAncient() 发放
		// 先古祝福；二幕开局分支绝不允许占用它。树洞/终局临时图的起点同为
		// Ancient 节点且依赖本控制器强制事件，故只对开局分支收紧。
		if (mapPointType == MapPointType.Ancient &&
			IntegratedStrategyEventReplay.IsSecondActOpeningBranch(forcedEventType))
		{
			return false;
		}

		// PullNextEvent 内部会走原生 Hook.ModifyNextEvent。普通强制节点继续尊重
		// 其他模组的替换；永恒之尘的两个萨米剧情前置节点必须保持固定，避免
		// SL 或第三方 Hook 把剧情链替换成普通事件。
		bool isLockedEternalDustEvent =
			TryGetLockedEternalDustEvent(state, forcedEventType, out EventModel lockedEternalDustEvent);
		HashSet<ModelId>? visitedEventsBeforePull = isLockedEternalDustEvent
			? [.. state.VisitedEventIds]
			: null;
		EventModel forcedEvent = state.Act.PullNextEvent(state);
		if (forcedEvent.GetType() != forcedEventType)
		{
			if (isLockedEternalDustEvent)
			{
				RepairLockedEventVisit(
					state,
					forcedEvent,
					lockedEternalDustEvent,
					visitedEventsBeforePull!);
				Log.Info(
					$"{ModInfo.LogPrefix} Rejected an override from {forcedEventType.Name} to " +
					$"{forcedEvent.Id.Entry} at a locked Eternal Dust story node.");
				forcedEvent = lockedEternalDustEvent;
			}
			else
			{
				Log.Info(
					$"{ModInfo.LogPrefix} Forced event {forcedEventType.Name} was overridden to " +
					$"{forcedEvent.Id.Entry} by a ModifyNextEvent hook; honoring the override.");
			}
		}

		LogForcedModelReplacement("Forced event node", model, forcedEvent);
		room = new EventRoom(forcedEvent);
		return true;
	}

	private static void RepairLockedEventVisit(
		RunState state,
		EventModel rejectedEvent,
		EventModel lockedEvent,
		IReadOnlySet<ModelId> visitedEventsBeforePull)
	{
		// PullNextEvent 会先把 ModifyNextEvent 返回的模型记为已访问。永恒之尘
		// 拒绝替换时必须同步修正这份运行状态，否则 SL 会污染后续事件池。
		if (!visitedEventsBeforePull.Contains(rejectedEvent.Id))
		{
			VisitedEventIdsRef(state).Remove(rejectedEvent.Id);
		}

		state.AddVisitedEvent(lockedEvent);
	}

	private static bool TryGetLockedEternalDustEvent(
		RunState state,
		Type forcedEventType,
		out EventModel eventModel)
	{
		if (forcedEventType == typeof(ReconstructionEvent) &&
			IntegratedStrategyTreeHoleController.IsAtEternalDustFirstEventPoint(state))
		{
			eventModel = ModelDb.Event<ReconstructionEvent>();
			return true;
		}

		if (forcedEventType == typeof(ExplorerSmallStepEvent) &&
			IntegratedStrategyTreeHoleController.IsAtEternalDustSecondEventPoint(state))
		{
			eventModel = ModelDb.Event<ExplorerSmallStepEvent>();
			return true;
		}

		eventModel = null!;
		return false;
	}

	private static bool TryGetForcedEventType(RunState state, out Type eventType)
	{
		if (IntegratedStrategyFirstEventPatch.TryGetForcedEventType(state, out eventType))
		{
			return true;
		}

		return IntegratedStrategySecretMapNodeController.TryGetForcedEventType(state, out eventType);
	}

	private static void LogForcedModelReplacement(
		string source,
		AbstractModel? incomingModel,
		AbstractModel forcedModel)
	{
		if (incomingModel != null && !incomingModel.Id.Equals(forcedModel.Id))
		{
			Log.Info(
				$"{ModInfo.LogPrefix} {source} forced room model to " +
				$"{forcedModel.Id.Entry} instead of {incomingModel.Id.Entry}.");
		}
	}
}
