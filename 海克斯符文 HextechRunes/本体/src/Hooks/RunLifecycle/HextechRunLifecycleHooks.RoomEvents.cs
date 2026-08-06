using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs.History;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{
	private static void SubscribeRoomEnteredIfNeeded(bool force = false)
	{
		RunManager manager = RunManager.Instance;
		if (_subscribedRoomEntered && ReferenceEquals(_subscribedRoomEnteredManager, manager))
		{
			if (!force)
			{
				return;
			}

			manager.RoomEntered -= OnRoomEntered;
		}
		else if (_subscribedRoomEnteredManager != null)
		{
			_subscribedRoomEnteredManager.RoomEntered -= OnRoomEntered;
		}

		manager.RoomEntered += OnRoomEntered;
		_subscribedRoomEntered = true;
		_subscribedRoomEnteredManager = manager;
	}

	private static void SubscribeRoomExitedIfNeeded(bool force = false)
	{
		RunManager manager = RunManager.Instance;
		if (_subscribedRoomExited && ReferenceEquals(_subscribedRoomExitedManager, manager))
		{
			if (!force)
			{
				return;
			}

			manager.RoomExited -= OnRoomExited;
		}
		else if (_subscribedRoomExitedManager != null)
		{
			_subscribedRoomExitedManager.RoomExited -= OnRoomExited;
		}

		manager.RoomExited += OnRoomExited;
		_subscribedRoomExited = true;
		_subscribedRoomExitedManager = manager;
	}

	private static void OnRoomEntered()
	{
		// 挂在 RunManager.RoomEntered 上，异常会中断委托链导致后续订阅者单端不执行（联机分叉），必须整体兜底。
		try
		{
			OnRoomEnteredCore();
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] OnRoomEntered failed: {ex}");
		}
	}

	private static void OnRoomEnteredCore()
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState runState)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: no run state");
			return;
		}

		HextechMayhemModifier? modifier = HextechMayhemModifier.FindIn(runState);
		if (modifier == null && !RunsInsideStartRunOrig.Contains(runState))
		{
			modifier = GetOrRecoverMayhemModifier(runState, $"OnRoomEntered recovered missing modifier room={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={runState.CurrentActIndex}");
		}

		int stageIndex = modifier == null
			? -1
			: ResolveCurrentStageIndex(runState, modifier, out _);
		if (modifier != null && !modifier.IsStageResolved(stageIndex) && modifier.TryRecoverResolvedActsFromPlayerRelics(nameof(OnRoomEntered), stageIndex))
		{
			RefreshEnemyUiSafely(modifier);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: room={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={runState.CurrentActIndex} stageIndex={stageIndex} stageResolved={modifier?.IsStageResolved(stageIndex)} startedWithNeow={runState.ExtraFields.StartedWithNeow} {DescribeCurrentEventState(runState)}");
		if (runState.CurrentRoom is EventRoom { CanonicalEvent: AncientEventModel ancientEvent }
			&& modifier != null
			&& runState.CurrentActIndex >= 0
			&& !modifier.IsStageResolved(stageIndex))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: pending act selection is deferred until ancient event proceed. act={runState.CurrentActIndex} event={ancientEvent.Id.Entry} {DescribeCurrentEventState(runState)}");
		}
		if (modifier != null && ShouldScheduleActSelectionOnRoomEntered(runState, modifier, stageIndex))
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomEntered: scheduling selection for room={runState.CurrentRoom?.GetType().Name ?? "null"}");
			TaskHelper.RunSafely(HextechRuneSelectionCoordinator.HandleStageSelection(runState, modifier, stageIndex));
		}

		try
		{
			HextechEnemyUi.HideMayhemModifierBadge();
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] OnRoomEntered badge refresh failed: {ex}");
		}

		if (modifier != null)
		{
			RefreshEnemyUiSafely(modifier);
		}
	}

	private static void RefreshEnemyUiSafely(HextechMayhemModifier modifier)
	{
		// UI 刷新是纯表现层，失败不能影响后续状态调度。
		try
		{
			HextechEnemyUi.Refresh(modifier);
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] OnRoomEntered enemy UI refresh failed: {ex}");
		}
	}

	private static void OnRoomExited()
	{
		try
		{
			if (RunManager.Instance.DebugOnlyGetState() is not RunState runState)
			{
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomExited: no run state");
				return;
			}

			MapPointHistoryEntry? currentHistory = runState.CurrentMapPointHistoryEntry;
			IReadOnlyList<MapPointRoomHistoryEntry>? rooms = currentHistory?.Rooms;
			MapPointRoomHistoryEntry? roomHistory = rooms != null && rooms.Count > 0 ? rooms[^1] : null;
			string modelEntry = roomHistory?.ModelId?.Entry ?? "null";
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] OnRoomExited: currentRoom={(runState.CurrentRoom?.GetType().Name ?? "null")} lastHistoryRoom={roomHistory?.RoomType} model={modelEntry}");
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] OnRoomExited failed: {ex}");
		}
	}

	private static bool ShouldScheduleActSelectionOnRoomEntered(RunState runState, HextechMayhemModifier modifier, int stageIndex)
	{
		if (stageIndex < 0 || modifier.IsStageResolved(stageIndex) || ShouldDeferActSelectionUntilAfterCurrentEvent(runState))
		{
			return false;
		}

		return runState.CurrentRoom is MapRoom || runState.CurrentRoom is not null and not EventRoom;
	}
}
