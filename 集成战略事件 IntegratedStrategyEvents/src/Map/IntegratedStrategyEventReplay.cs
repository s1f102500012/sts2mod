using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.TreeHoles;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace IntegratedStrategyEvents.Map;

internal static class IntegratedStrategyEventReplay
{
	private static readonly ConditionalWeakTable<RunState, ReplayMarker> ReplayMarkers = new();

	public static bool TryRestoreSavedCurrentEvent(
		RoomSet roomSet,
		RunState runState,
		Predicate<EventModel> isManagedEvent,
		string reason)
	{
		if (roomSet.events.Count == 0 ||
			!TryGetSavedCurrentEvent(runState, out EventModel? savedEvent) ||
			savedEvent == null)
		{
			return false;
		}

		if (!isManagedEvent(savedEvent))
		{
			return false;
		}

		ForceEventAtCurrentIndex(roomSet, savedEvent);
		SetReplayMarker(runState, savedEvent.Id);
		Log.Info(
			$"{ModInfo.LogPrefix} Restored saved event {savedEvent.Id.Entry} " +
			$"for current map point ({reason}).");
		return true;
	}

	public static bool ShouldSkipReplayHistoryAppend(
		RunState runState,
		RoomType roomType,
		ModelId? modelId)
	{
		return roomType == RoomType.Event &&
			modelId != null &&
			ReplayMarkers.TryGetValue(runState, out ReplayMarker? marker) &&
			marker.EventId.Equals(modelId);
	}

	public static bool ShouldSkipReplayRoomVisit(RoomType roomType)
	{
		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null ||
			roomType != RoomType.Event ||
			!ReplayMarkers.TryGetValue(runState, out ReplayMarker? marker))
		{
			return false;
		}

		ReplayMarkers.Remove(runState);
		// 其他模组可通过原生 Hook.ModifyNextEvent 替换真正进入的事件；只有实际进入的
		// 事件与标记一致时才吞掉本次访问计数，否则撤销标记并按原版计数。
		if (runState.CurrentRoom is not EventRoom eventRoom ||
			!marker.EventId.Equals(eventRoom.CanonicalEvent.Id))
		{
			Log.Info(
				$"{ModInfo.LogPrefix} Entered event does not match the replay marker " +
				$"(another mod may have modified the next event); counting the visit normally.");
			return false;
		}

		Log.Info($"{ModInfo.LogPrefix} Re-entered saved event without advancing event visit counters.");
		return true;
	}

	public static bool IsAnyManagedForcedEvent(EventModel eventModel)
	{
		Type eventType = eventModel.GetType();
		return eventModel is IntegratedStrategyEventModel ||
			IsSecondActOpeningBranch(eventType) ||
			IsTreeHoleEvent(eventType) ||
			eventType == typeof(ReconstructionEvent) ||
			eventType == typeof(ExplorerSmallStepEvent) ||
			eventType == typeof(SublimationEvent) ||
			eventType == typeof(OdeEvent);
	}

	public static bool IsSecondActOpeningBranch(Type eventType)
	{
		return eventType == typeof(VoidPortentEvent) ||
			eventType == typeof(ChangeEvent) ||
			eventType == typeof(PrimordialDivergenceEvent) ||
			eventType == typeof(BeginningEvent) ||
			eventType == typeof(LiberationEvent);
	}

	public static bool IsSecondActOpeningBranch(EventModel eventModel)
	{
		return IsSecondActOpeningBranch(eventModel.GetType());
	}

	public static bool IsTreeHoleEvent(EventModel eventModel)
	{
		return IsTreeHoleEvent(eventModel.GetType());
	}

	public static bool IsTreeHoleEvent(Type eventType)
	{
		return eventType == typeof(ForwardForestEvent) ||
			eventType == typeof(StoryToBeToldEvent) ||
			eventType == typeof(TruthToBeToldEvent) ||
			eventType == typeof(ShiftingCityEvent) ||
			eventType == typeof(GlimpseEvent);
	}

	private static bool TryGetSavedCurrentEvent(RunState runState, out EventModel? eventModel)
	{
		eventModel = null;
		if (!runState.CurrentMapCoord.HasValue ||
			runState.MapPointHistory.Count <= runState.CurrentActIndex)
		{
			return false;
		}

		IReadOnlyList<MapPointHistoryEntry> actHistory = runState.MapPointHistory[runState.CurrentActIndex];
		if (!TryGetCurrentMapPointHistoryEntry(runState, actHistory, out MapPointHistoryEntry? currentEntry) ||
			currentEntry == null)
		{
			return false;
		}

		MapPointRoomHistoryEntry? eventRoom = currentEntry.FirstRoomOfType(RoomType.Event);
		if (eventRoom?.ModelId == null)
		{
			return false;
		}

		try
		{
			eventModel = ModelDb.GetById<EventModel>(eventRoom.ModelId);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not restore saved event {eventRoom.ModelId}: {ex}");
			return false;
		}
	}

	private static bool TryGetCurrentMapPointHistoryEntry(
		RunState runState,
		IReadOnlyList<MapPointHistoryEntry> actHistory,
		out MapPointHistoryEntry? entry)
	{
		entry = null;
		if (!runState.CurrentMapCoord.HasValue)
		{
			return false;
		}

		int row = runState.CurrentMapCoord.Value.row;
		if (row < 0)
		{
			return false;
		}

		if (TryGetTemporaryMapHistoryOffset(runState, out int offset))
		{
			// 临时图的隐藏起点会加入 VisitedMapCoords，但不会生成 MapPointHistory；
			// 当前房间对应“已访问临时坐标数 - 起点 - 1”的历史偏移。
			int visitedTemporaryRoomCount = runState.VisitedMapCoords.Count - 1;
			if (visitedTemporaryRoomCount <= 0)
			{
				return false;
			}

			int temporaryHistoryIndex = offset + visitedTemporaryRoomCount - 1;
			if (temporaryHistoryIndex < offset || temporaryHistoryIndex >= actHistory.Count)
			{
				return false;
			}

			entry = actHistory[temporaryHistoryIndex];
			return true;
		}

		if (row >= actHistory.Count)
		{
			return false;
		}

		entry = actHistory[row];
		return true;
	}

	private static bool TryGetTemporaryMapHistoryOffset(RunState runState, out int offset)
	{
		offset = 0;
		if (TreeHoleSessionManager.TryGetTreeHoleSession(runState, out TreeHoleSession treeHoleSession))
		{
			offset = GetActHistoryCount(treeHoleSession.OriginalMapPointHistory, runState.CurrentActIndex);
			return true;
		}

		if (TreeHoleSessionManager.TryGetFinaleSession(runState, out EndlessFinaleSession finaleSession))
		{
			offset = GetActHistoryCount(finaleSession.OriginalMapPointHistory, runState.CurrentActIndex);
			return true;
		}

		return false;
	}

	private static int GetActHistoryCount(
		IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history,
		int actIndex)
	{
		return actIndex >= 0 && actIndex < history.Count ? history[actIndex].Count : 0;
	}

	private static void ForceEventAtCurrentIndex(RoomSet roomSet, EventModel eventModel)
	{
		int desiredIndex = roomSet.eventsVisited % roomSet.events.Count;
		int existingIndex = roomSet.events.FindIndex(candidate => candidate.Id.Equals(eventModel.Id));
		if (existingIndex >= 0)
		{
			(roomSet.events[desiredIndex], roomSet.events[existingIndex]) =
				(roomSet.events[existingIndex], roomSet.events[desiredIndex]);
			return;
		}

		roomSet.events[desiredIndex] = eventModel;
	}

	private static void SetReplayMarker(RunState runState, ModelId eventId)
	{
		ReplayMarkers.Remove(runState);
		ReplayMarkers.Add(runState, new ReplayMarker(eventId));
	}

	private sealed record ReplayMarker(ModelId EventId);
}
