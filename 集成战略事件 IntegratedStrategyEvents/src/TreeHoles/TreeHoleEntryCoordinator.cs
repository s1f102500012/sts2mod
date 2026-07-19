using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleEntryCoordinator
{
	public static Task EnterFromEvent(Player owner)
	{
		return EnterFromEvent(owner, TreeHoleConstants.DeepBuriedActName, TreeHoleConstants.DeepBuriedStageLabel);
	}

	public static Task EnterFromEvent(Player owner, string destinationActName)
	{
		return EnterFromEvent(owner, destinationActName, TreeHoleConstants.UnknownStageLabel);
	}

	public static Task EnterFromEvent(Player owner, string destinationActName, string stageLabel)
	{
		IntegratedStrategyTemporaryMapAction.EnqueueTreeHoleEntry(owner, destinationActName, stageLabel);
		return Task.CompletedTask;
	}

	public static Task EnterFromDebugCommand(Player owner, string destinationActName, string stageLabel)
	{
		return EnterFromEventDeferred(owner, destinationActName, stageLabel);
	}

	internal static Task EnterFromSyncedAction(Player owner, string destinationActName, string stageLabel)
	{
		return EnterFromEventDeferred(owner, destinationActName, stageLabel);
	}

	private static async Task EnterFromEventDeferred(Player owner, string destinationActName, string stageLabel)
	{
		RunManager runManager = RunManager.Instance;
		if (owner.RunState is not RunState state)
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to enter a tree-hole without a run state.");
			return;
		}

		if (TreeHoleSessionManager.IsActive(state))
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to enter a tree-hole while one is already active.");
			return;
		}

		if (!TreeHoleSessionManager.AddPendingTreeHoleEntry(state))
		{
			Log.Warn($"{ModInfo.LogPrefix} Ignored a duplicate tree-hole entry request.");
			return;
		}

		try
		{
			await TreeHoleSessionManager.AwaitNextProcessFrame();

			if (!ReferenceEquals(runManager.DebugOnlyGetState(), state))
			{
				Log.Warn($"{ModInfo.LogPrefix} Tree-hole entry was cancelled because the active run changed.");
				return;
			}

			await WaitForEventOptionSettlement(state);

			if (TestMode.IsOff && NGame.Instance != null)
			{
				await NGame.Instance.Transition.RoomFadeOut();
			}

			Log.Info($"{ModInfo.LogPrefix} Preparing to enter {destinationActName} tree-hole.");
			SerializableActModel originalActSave = state.Act.ToSave();
			MapCoord? entryMapCoord = state.CurrentMapCoord;
			await TreeHoleRunAccessor.ExitCurrentRooms(runManager);
			TreeHoleRunAccessor.ClearScreens(runManager);
			uint treeHoleMapSeed = TreeHoleSeedFactory.CreateTreeHoleMapSeed(state, destinationActName, stageLabel);
			Rng treeHoleRng = new(treeHoleMapSeed, TreeHoleSeedFactory.TreeHoleMapRngName);
			IntegratedStrategyTreeHoleActMap treeHoleMap = IntegratedStrategyTreeHoleActMap.Create(treeHoleRng);
			TreeHoleSession session = new(
				state.Map,
				state.VisitedMapCoords.ToList(),
				state.MapPointHistory.Select(static history => history.ToList()).ToList(),
				state.ActFloor,
				originalActSave,
				treeHoleMapSeed,
				entryMapCoord,
				stageLabel,
				destinationActName,
				treeHoleMap,
				treeHoleMap.TerminalCoord);

			TreeHoleSessionManager.SetTreeHoleSession(state, session);
			state.Map = treeHoleMap;
			state.ClearVisitedMapCoordsDebug();
			state.AddVisitedMapCoord(treeHoleMap.StartingMapPoint.coord);
			TreeHoleSessionManager.RefreshLocationSynchronizers(state);
			TreeHoleSessionManager.SetMapScreen(treeHoleMap, state, initMarker: false);

			Log.Info($"{ModInfo.LogPrefix} Entering {destinationActName} tree-hole.");
			await TreeHoleRunAccessor.EnterRoomInternal(runManager, new MapRoom());
			Log.Info($"{ModInfo.LogPrefix} Entered {destinationActName} tree-hole map room.");
			await PersistTreeHoleEntry(runManager, destinationActName);
			await TreeHoleRunAccessor.FadeIn(runManager, showTransition: true);
		}
		finally
		{
			TreeHoleSessionManager.RemovePendingTreeHoleEntry(state);
		}
	}

	// 原版每次节点转换都会 SaveRun + CombatReplayWriter.RecordInitialState；自定义
	// 入层跳过了原生节点初始化流程，这里补齐同款落盘，避免入层后读档回到事件前
	// 的陈旧存档（奖励可重复领取/临时层会话丢失）。RecordInitialState 自带
	// IsEnabled 门禁，未开启回放录制时是 no-op。
	private static async Task PersistTreeHoleEntry(RunManager runManager, string destinationActName)
	{
		try
		{
			runManager.CombatReplayWriter.RecordInitialState(runManager.ToSave(null));
			await SaveManager.Instance.SaveRun(null);
			Log.Info($"{ModInfo.LogPrefix} Persisted {destinationActName} tree-hole entry.");
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to persist {destinationActName} tree-hole entry: {ex}");
		}
	}

	// 联机下同步的树洞进入动作可能先于共享事件的选项消息被处理；此时直接拆房会让
	// EventSynchronizer 留下未完成的事件克隆（下个事件开始时告警，慢端还会丢失
	// Finish 之前的效果）。拆房前等待本端全部事件克隆完成，超时才放行并告警。
	private static async Task WaitForEventOptionSettlement(RunState state)
	{
		if (state.CurrentRoom is not EventRoom)
		{
			return;
		}

		EventSynchronizer synchronizer = RunManager.Instance.EventSynchronizer;
		await synchronizer.AwaitPendingOptionTasks();
		const int maxFrames = 600;
		for (int frame = 0;
			frame < maxFrames && synchronizer.Events.Any(static e => !e.IsFinished);
			frame++)
		{
			await TreeHoleSessionManager.AwaitNextProcessFrame();
			await synchronizer.AwaitPendingOptionTasks();
		}

		if (synchronizer.Events.Any(static e => !e.IsFinished))
		{
			Log.Warn(
				$"{ModInfo.LogPrefix} Entering the tree-hole with unfinished event clones after " +
				$"waiting {maxFrames} frames; proceeding anyway.");
		}
	}
}
