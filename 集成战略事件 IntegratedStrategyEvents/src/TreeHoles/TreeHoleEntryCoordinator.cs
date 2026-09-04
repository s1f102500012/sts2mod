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
		if (!IntegratedStrategyPatcher.IsAvailable("temporary-map"))
			throw new InvalidOperationException("Temporary maps are unavailable; see the startup compatibility report.");
		NetGameType gameType = RunManager.Instance.NetService.Type;
		Log.Info(
			$"{ModInfo.LogPrefix} Tree-hole entry requested for {destinationActName} " +
			$"(stage={stageLabel}, player={owner.NetId}, mode={gameType}).");
		if (ShouldEnterDirectly(gameType))
		{
			// 单人事件选项已由 EventSynchronizer 排序；再放入玩家动作队列可能被尚未
			// 退出的事件动作挡在队首。延后一帧即可等当前选项任务收尾后安全拆房。
			_ = TaskHelper.RunSafely(EnterFromEventDeferred(owner, destinationActName, stageLabel));
			return Task.CompletedTask;
		}

		IntegratedStrategyTemporaryMapAction.EnqueueTreeHoleEntry(owner, destinationActName, stageLabel);
		Log.Info(
			$"{ModInfo.LogPrefix} Tree-hole entry for {destinationActName} was submitted " +
			$"to the synchronized action queue.");
		return Task.CompletedTask;
	}

	internal static bool ShouldEnterDirectly(NetGameType gameType)
	{
		return gameType == NetGameType.Singleplayer;
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
		if (!IntegratedStrategyPatcher.IsAvailable("temporary-map"))
			throw new InvalidOperationException("Temporary maps are unavailable; see the startup compatibility report.");
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

			if (!await WaitForEventOptionSettlement(state, destinationActName)) return;

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
			await TreeHoleRunAccessor.FadeOut();
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

	// 同步切图动作与事件选项消息属于不同流程；必须等所有事件克隆完成，不能按本机帧数强制拆房。
	internal static async Task<bool> WaitForEventOptionSettlement(
		RunState state,
		string destinationActName)
	{
		if (state.CurrentRoom is not EventRoom)
		{
			return true;
		}

		EventSynchronizer synchronizer = RunManager.Instance.EventSynchronizer;
		Log.Info($"{ModInfo.LogPrefix} Waiting for {destinationActName} event option settlement.");
		return await TreeHoleTransitionSettlement.Await(
			() => synchronizer.Events.All(static e => e.IsFinished),
			synchronizer.AwaitPendingOptionTasks,
			TreeHoleSessionManager.AwaitNextProcessFrame,
			() => ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), state));
	}
}
