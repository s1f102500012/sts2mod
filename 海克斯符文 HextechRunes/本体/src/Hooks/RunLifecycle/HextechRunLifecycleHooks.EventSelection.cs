using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{
	private static void EventRoomProceedPrefix(out EventRoomProceedState __state)
	{
		bool shouldSelectAfterProceed = TryGetPendingEventProceedSelection(out RunState runState, out int actIndex, out string eventId);
		__state = new EventRoomProceedState(shouldSelectAfterProceed, runState, actIndex, eventId);
		if (shouldSelectAfterProceed)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed begin: act={actIndex} event={eventId} {DescribeCurrentEventState(runState)}");
		}
	}

	private static void EventRoomProceedPostfix(EventRoomProceedState __state, ref Task __result)
	{
		__result = EventRoomProceedAfterOriginal(__result, __state);
	}

	private static async Task EventRoomProceedAfterOriginal(Task original, EventRoomProceedState state)
	{
		await original;

		// mod 延续体异常不能把原版 NEventRoom.Proceed 任务链打成 faulted(单端中断即联机分叉)。
		try
		{
			await EventRoomProceedContinuation(state);
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] EventRoomProceed continuation failed: {ex}");
		}
	}

	private static async Task EventRoomProceedContinuation(EventRoomProceedState state)
	{
		if (!state.ShouldSelectAfterProceed)
		{
			return;
		}

		RunState runState = state.RunState;
		int actIndex = state.ActIndex;
		string eventId = state.EventId;
		if (!IsCurrentRun(runState))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: run changed after proceed act={actIndex} event={eventId}");
			return;
		}

		HextechMayhemModifier modifier = GetOrRecoverMayhemModifier(runState, $"EventRoomProceed recovered missing modifier after proceed act={actIndex} event={eventId}");
		if (!modifier.IsActResolved(actIndex) && modifier.TryRecoverResolvedActsFromPlayerRelics(nameof(EventRoomProceedAfterOriginal)))
		{
			HextechEnemyUi.Refresh(modifier);
		}

		if (modifier.IsActResolved(actIndex))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: act{actIndex} already resolved event={eventId}");
			return;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed: waiting for all ancient events before act{actIndex} selection event={eventId} mapOpen={NMapScreen.Instance?.IsOpen == true}");
		NMapScreen.Instance?.SetTravelEnabled(enabled: false);
		try
		{
			await WaitForAllCurrentEventsFinished(runState, eventId);
			if (!IsCurrentRun(runState) || modifier.IsActResolved(actIndex))
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed skip: run changed or act{actIndex} resolved after wait event={eventId}");
				return;
			}

			HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed: selecting act{actIndex} hex after all ancient events finished event={eventId} mapOpen={NMapScreen.Instance?.IsOpen == true}");
			await HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier);
		}
		finally
		{
			if (IsCurrentRun(runState))
			{
				NMapScreen.Instance?.SetTravelEnabled(enabled: true);
			}
		}
	}

	private static bool TryGetPendingEventProceedSelection(out RunState runState, out int actIndex, out string eventId)
	{
		runState = null!;
		actIndex = -1;
		eventId = "null";

		if (RunManager.Instance.DebugOnlyGetState() is not RunState currentRunState
			|| currentRunState.CurrentActIndex is < 0 or > 2
			|| currentRunState.CurrentRoom is not EventRoom { CanonicalEvent: AncientEventModel ancientEvent })
		{
			return false;
		}

		if (HextechMayhemModifier.FindIn(currentRunState)?.IsActResolved(currentRunState.CurrentActIndex) == true)
		{
			return false;
		}

		runState = currentRunState;
		actIndex = currentRunState.CurrentActIndex;
		eventId = ancientEvent.Id.Entry;
		return true;
	}

	private static async Task WaitForAllCurrentEventsFinished(RunState runState, string eventId)
	{
		for (int frame = 0; IsCurrentRun(runState); frame++)
		{
			IReadOnlyList<EventModel> events = RunManager.Instance.EventSynchronizer.Events;
			int finishedCount = events.Count(static eventModel => eventModel.IsFinished);
			if (AreRequiredCurrentEventsFinished(runState, events, finishedCount, out string completionReason))
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed: required events finished event={eventId} count={events.Count} finished={finishedCount} reason={completionReason} waitedFrames={frame}");
				return;
			}

			if (frame % 300 == 0)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] EventRoomProceed: waiting for remote events event={eventId} finished={finishedCount}/{events.Count} players={runState.Players.Count}");
			}

			await WaitOneFrame();
		}
	}

	private static bool AreRequiredCurrentEventsFinished(
		RunState runState,
		IReadOnlyList<EventModel> events,
		int finishedCount,
		out string completionReason)
	{
		completionReason = "all-player-events";
		return events.Count >= runState.Players.Count && finishedCount == events.Count;
	}
}
