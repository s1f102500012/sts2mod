using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
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

			await WaitForEventOptionSettlement(state, destinationActName);

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
			await TreeHoleRunAccessor.PersistCurrentRunTransition(
				runManager,
				$"{destinationActName} tree-hole entry");
			await TreeHoleRunAccessor.FadeIn(runManager, showTransition: true);
		}
		finally
		{
			TreeHoleSessionManager.RemovePendingTreeHoleEntry(state);
		}
	}

	// 联机下同步的树洞进入动作可能先于共享事件的选项消息被处理；此时直接拆房会让
	// EventSynchronizer 留下未完成的事件克隆（下个事件开始时告警，慢端还会丢失
	// Finish 之前的效果）。拆房前等待本端全部事件克隆完成，超时才放行并告警。
	internal static async Task WaitForEventOptionSettlement(
		RunState state,
		string destinationActName)
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
				$"{ModInfo.LogPrefix} Entering {destinationActName} with unfinished event clones after " +
				$"waiting {maxFrames} frames; proceeding anyway.");
		}
	}
}
