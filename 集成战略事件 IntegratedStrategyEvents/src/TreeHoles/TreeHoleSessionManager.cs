using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleSessionManager
{
	internal static TreeHoleSessionStore SessionStore { get; } = new();

	public static bool IsActive(IRunState? runState)
	{
		return runState is RunState state && SessionStore.IsActive(state);
	}

	public static bool IsActiveCurrentRun()
	{
		return IsActive(RunManager.Instance.DebugOnlyGetState());
	}

	public static bool TryRestoreCompletedCurrentRun()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null || !SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			return false;
		}

		if (SessionStore.IsCompletionSuppressedUntilTerminalProceed(state))
		{
			return false;
		}

		// 终局奖励 proceed 之后房间会一直停留在 Treasure/Combat（不会再进 MapRoom），
		// 若 proceed 时的单次返回请求丢失，只认 MapRoom 会让玩家永久卡在树洞层；
		// proceed 标记 + 房间已结算时同样允许触发返回。宝箱房没有 IsPreFinished
		// 信号（原版恒 false），但 proceed 标记在每次进房时都会被清除，标记仍在
		// 即代表玩家已在当前宝箱房里点过"前进"，可视作已结算。
		if (state.CurrentRoom is not MapRoom &&
			!(SessionStore.HasTerminalRewardsProceeded(state) &&
			  state.CurrentRoom is TreasureRoom or { IsPreFinished: true }))
		{
			return false;
		}

		if (!HasVisitedCoord(state.VisitedMapCoords, session.TerminalCoord))
		{
			return false;
		}

		return RequestRestoreOriginalMap(state, session);
	}

	// 终局奖励 proceed 已发生：记录标记供上面的门禁重试使用，并解除读档路径设下的
	// completion suppression——即使本次单发恢复因 session 尚未装回而打空，后续
	// SetMap/Open/RecalculateTravelability 的重试也不再被封死。
	public static void MarkTerminalRewardsProceededCurrentRun()
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState state)
		{
			return;
		}

		SessionStore.AddTerminalRewardsProceeded(state);
		SessionStore.RemoveCompletionSuppression(state);
	}

	// 宝箱房的"前进"按钮直接 NMapScreen.Open()，不经过 RunManager.ProceedFromTerminalRewardsScreen，
	// 精英终点依赖的 proceed 补丁对宝箱终点不会触发。这里由 NTreasureRoom 前进按钮的补丁调用，
	// 在终点宝箱房补齐与精英路径对称的标记+立即返回。
	public static void HandleTerminalTreasureRoomProceed()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null ||
			!SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session) ||
			state.CurrentRoom is not TreasureRoom ||
			!state.CurrentMapCoord.HasValue ||
			!state.CurrentMapCoord.Value.Equals(session.TerminalCoord))
		{
			return;
		}

		SessionStore.AddTerminalRewardsProceeded(state);
		SessionStore.RemoveCompletionSuppression(state);
		TryRestoreCompletedCurrentRunAfterTerminalProceed();
	}

	public static bool TryRestoreCompletedCurrentRunAfterTerminalProceed()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null || !SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			return false;
		}

		if (!state.CurrentMapCoord.HasValue || !state.CurrentMapCoord.Value.Equals(session.TerminalCoord))
		{
			return false;
		}

		if (!HasVisitedCoord(state.VisitedMapCoords, session.TerminalCoord))
		{
			return false;
		}

		if (state.CurrentRoom is not MapRoom &&
			state.CurrentRoom is not TreasureRoom &&
			state.CurrentRoom is not CombatRoom)
		{
			return false;
		}

		return RequestRestoreOriginalMap(state, session);
	}

	public static bool TryGetCurrentDestination(out string actName)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state != null && SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			actName = session.DestinationActName;
			return true;
		}

		if (state != null && SessionStore.TryGetFinaleSession(state, out EndlessFinaleSession finaleSession))
		{
			actName = finaleSession.DestinationActName;
			return true;
		}

		actName = string.Empty;
		return false;
	}

	public static bool TryGetCurrentDestination(out string stageLabel, out string actName)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state != null && SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			stageLabel = session.StageLabel;
			actName = session.DestinationActName;
			return true;
		}

		if (state != null && SessionStore.TryGetFinaleSession(state, out EndlessFinaleSession finaleSession))
		{
			stageLabel = finaleSession.StageLabel;
			actName = finaleSession.DestinationActName;
			return true;
		}

		stageLabel = string.Empty;
		actName = string.Empty;
		return false;
	}

	public static TreeHoleSaveSnapshot? GetSaveSnapshot(RunState? state)
	{
		return IntegratedStrategyTreeHoleSaveStateStore.CreateSnapshot(state, SessionStore);
	}

	public static void QueueRestoreFromSave(SerializableRun save, RunState state)
	{
		if (IntegratedStrategyTreeHoleSaveStateStore.TryGetResumeRoom(
				save,
				state,
				out TreeHoleResumeRoom resumeRoom,
				out _))
		{
			if (resumeRoom == TreeHoleResumeRoom.Architect)
			{
				SessionStore.AddPendingArchitectCompletion(state);
				Log.Info($"{ModInfo.LogPrefix} Queued the finale Architect room resume from save.");
				return;
			}

			if (resumeRoom == TreeHoleResumeRoom.Map)
			{
				SessionStore.AddPendingMapRoomResume(state);
				Log.Info($"{ModInfo.LogPrefix} Queued a map-room resume from save.");
			}
		}

		TreeHoleRestoreSnapshot? snapshot = IntegratedStrategyTreeHoleSaveStateStore.Load(save, state);
		if (snapshot == null)
		{
			return;
		}

		SessionStore.QueueRestore(state, snapshot);
		if (ShouldWaitForTerminalRewardsProceed(save, snapshot))
		{
			SessionStore.SuppressCompletionUntilTerminalProceed(state);
			Log.Info($"{ModInfo.LogPrefix} Delaying tree-hole completion restore until terminal rewards proceed.");
		}

		Log.Info($"{ModInfo.LogPrefix} Queued {snapshot.Kind} tree-hole restore from save.");
	}

	public static bool TryRestoreSavedSessionForCurrentRun(
		ActMap map,
		bool allowMapRebuild = true)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null || !SessionStore.TryGetPendingRestore(state, out TreeHoleRestoreSnapshot snapshot))
		{
			return false;
		}

		ActMap sessionMap = map;
		bool rebuiltSessionMap = false;
		bool sessionMapMatches = MatchesSavedTemporaryMap(sessionMap, snapshot);
		if (snapshot.Kind == TreeHoleSaveKind.TreeHole &&
			!sessionMapMatches &&
			snapshot.TreeHoleMapSeed != 0)
		{
			if (!allowMapRebuild)
			{
				return false;
			}

			sessionMap = IntegratedStrategyTreeHoleActMap.Create(new Rng(
				snapshot.TreeHoleMapSeed,
				TreeHoleSeedFactory.TreeHoleMapRngName));
			state.Map = sessionMap;
			rebuiltSessionMap = true;
			sessionMapMatches = MatchesSavedTemporaryMap(sessionMap, snapshot);
		}

		// 幕身份校验优先按保存的 ActModel ID（第三方模组增删临时幕会使序号漂移），
		// 旧格式快照无 ID 时回退序号比对。
		bool actMatches = !string.IsNullOrEmpty(snapshot.ParentActId)
			? string.Equals(state.Act.Id.Entry, snapshot.ParentActId, StringComparison.Ordinal)
			: state.CurrentActIndex == snapshot.CurrentActIndex;
		if (!actMatches ||
			!sessionMapMatches ||
			!MapContainsCoord(sessionMap, snapshot.TerminalCoord))
		{
			Log.Warn($"{ModInfo.LogPrefix} Ignored stale tree-hole restore snapshot.");
			SessionStore.RemovePendingRestore(state);
			SessionStore.RemovePendingMapRoomResume(state);
			return false;
		}

		ActMap originalMap = new SavedActMap(snapshot.OriginalMap);
		List<IReadOnlyList<MapPointHistoryEntry>> originalHistory =
			CopyHistoryForParentAct(
				state.MapPointHistory,
				snapshot.OriginalMapPointHistoryCounts,
				snapshot.CurrentActIndex,
				state.CurrentActIndex);

		if (snapshot.Kind == TreeHoleSaveKind.TreeHole)
		{
			SessionStore.SetTreeHoleSession(state, new TreeHoleSession(
				originalMap,
				snapshot.OriginalVisitedMapCoords,
				originalHistory,
				snapshot.OriginalActFloor,
				snapshot.OriginalActSave,
				snapshot.TreeHoleMapSeed,
				snapshot.CurrentMapCoord,
				snapshot.StageLabel,
				snapshot.DestinationActName,
				sessionMap,
				snapshot.TerminalCoord));
		}
		else
		{
			SpecialFinaleKind finaleKind = snapshot.Kind switch
			{
				TreeHoleSaveKind.EternalDustFinale => SpecialFinaleKind.EternalDust,
				TreeHoleSaveKind.RadiantApexFinale => SpecialFinaleKind.RadiantApex,
				TreeHoleSaveKind.CarefreeViharaFinale => SpecialFinaleKind.CarefreeVihara,
				TreeHoleSaveKind.AbyssalJungleFinale => SpecialFinaleKind.AbyssalJungle,
				TreeHoleSaveKind.AbyssalJungleIsharmlaFinale => SpecialFinaleKind.AbyssalJungleIsharmla,
				TreeHoleSaveKind.ProphetHornFragment => SpecialFinaleKind.ProphetHornFragment,
				TreeHoleSaveKind.DesireHallFinale => SpecialFinaleKind.DesireHall,
				_ => SpecialFinaleKind.EndlessFinale
			};
			SessionStore.SetFinaleSession(state, new EndlessFinaleSession(
				originalMap,
				snapshot.OriginalVisitedMapCoords,
				originalHistory,
				snapshot.OriginalActFloor,
				snapshot.OriginalActSave,
				snapshot.StageLabel,
				snapshot.DestinationActName,
				sessionMap,
				finaleKind));
		}

		state.ActFloor = snapshot.CurrentActFloor;
		SessionStore.RemovePendingRestore(state);
		RefreshLocationSynchronizers(state);
		if (rebuiltSessionMap)
		{
			SetMapScreen(sessionMap, state, initMarker: snapshot.CurrentMapCoord.HasValue);
		}

		Log.Info($"{ModInfo.LogPrefix} Restored {snapshot.Kind} tree-hole session from save.");
		return true;
	}

	public static bool IsCurrentTreeHoleMap(ActMap map)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		return state != null &&
			SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session) &&
			(ReferenceEquals(session.TreeHoleMap, map) || MapContainsCoord(map, session.TerminalCoord));
	}

	/// <summary>当前跑局的树洞图或任意 kind 的终局会话图（等价于逐 kind 的 IsCurrent*Map 全并集）。</summary>
	public static bool IsCurrentAnyTemporaryMap(ActMap map)
	{
		if (IsCurrentTreeHoleMap(map))
		{
			return true;
		}

		RunState? state = RunManager.Instance.DebugOnlyGetState();
		return state != null &&
			SessionStore.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			IsSessionFinaleMap(state, session, map);
	}

	public static bool IsCurrentEndlessFinaleMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.EndlessFinale);
	}

	public static bool IsCurrentEternalDustFinaleMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.EternalDust);
	}

	public static bool IsCurrentRadiantApexFinaleMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.RadiantApex);
	}

	public static bool IsCurrentCarefreeViharaFinaleMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.CarefreeVihara);
	}

	public static bool IsCurrentDesireHallFinaleMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.DesireHall);
	}

	public static bool IsCurrentAbyssalJungleFinaleMap(ActMap map)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		return state != null &&
			SessionStore.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			(session.Kind == SpecialFinaleKind.AbyssalJungle ||
			 session.Kind == SpecialFinaleKind.AbyssalJungleIsharmla) &&
			IsSessionFinaleMap(state, session, map);
	}

	public static bool IsCurrentProphetHornFragmentMap(ActMap map)
	{
		return IsCurrentFinaleMap(map, SpecialFinaleKind.ProphetHornFragment);
	}

	public static bool TryGetTreeHoleSession(RunState state, out TreeHoleSession session)
	{
		return SessionStore.TryGetTreeHoleSession(state, out session);
	}

	public static void SetTreeHoleSession(RunState state, TreeHoleSession session)
	{
		SessionStore.SetTreeHoleSession(state, session);
	}

	public static bool TryGetFinaleSession(RunState state, out EndlessFinaleSession session)
	{
		return SessionStore.TryGetFinaleSession(state, out session);
	}

	public static void SetFinaleSession(RunState state, EndlessFinaleSession session)
	{
		SessionStore.SetFinaleSession(state, session);
	}

	public static bool AddPendingTreeHoleEntry(RunState state)
	{
		return SessionStore.AddPendingTreeHoleEntry(state);
	}

	public static bool RemovePendingTreeHoleEntry(RunState state)
	{
		return SessionStore.RemovePendingTreeHoleEntry(state);
	}

	public static bool AddPendingFinaleEntry(RunState state)
	{
		return SessionStore.AddPendingFinaleEntry(state);
	}

	public static bool RemovePendingFinaleEntry(RunState state)
	{
		return SessionStore.RemovePendingFinaleEntry(state);
	}

	public static void AddPendingArchitectCompletion(RunState state)
	{
		SessionStore.AddPendingArchitectCompletion(state);
	}

	public static bool HasPendingArchitectCompletion(RunState state)
	{
		return SessionStore.HasPendingArchitectCompletion(state);
	}

	public static bool RemovePendingArchitectCompletion(RunState state)
	{
		return SessionStore.RemovePendingArchitectCompletion(state);
	}

	public static bool HasPendingMapRoomResume(RunState state)
	{
		return SessionStore.HasPendingMapRoomResume(state);
	}

	public static bool RemovePendingMapRoomResume(RunState state)
	{
		return SessionStore.RemovePendingMapRoomResume(state);
	}

	public static void OnRunStarted(RunState state)
	{
		SessionStore.ClearForRunStarted(state);
	}

	public static void OnRoomEntered()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null)
		{
			return;
		}

		// 进入新房间意味着上一个 proceed 标记已过期，避免陈旧标记让完结判定提前触发。
		SessionStore.RemoveTerminalRewardsProceeded(state);
		if (state.CurrentRoom is not MapRoom || !SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			return;
		}

		if (!HasVisitedCoord(state.VisitedMapCoords, session.TerminalCoord))
		{
			return;
		}

		if (SessionStore.IsCompletionSuppressedUntilTerminalProceed(state))
		{
			return;
		}

		RequestRestoreOriginalMap(state, session);
	}

	internal static async Task RestoreOriginalMapFromSyncedAction(Player owner)
	{
		if (owner.RunState is not RunState state)
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to return from a tree-hole without a run state.");
			return;
		}

		if (!SessionStore.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			// 联机 rejoin 端可能还挂着未消费的 pending restore：先尝试装回 session 再返回，
			// 否则该端会在收到同步返回动作后仍卡在树洞层。
			if (state.Map is { } currentMap &&
				TryRestoreSavedSessionForCurrentRun(currentMap) &&
				SessionStore.TryGetTreeHoleSession(state, out session))
			{
				await RestoreOriginalMapAndPersist(state, session);
				return;
			}

			SessionStore.RemovePendingTreeHoleReturn(state);
			Log.Warn($"{ModInfo.LogPrefix} Ignored a tree-hole return request because no session was active.");
			return;
		}

		await RestoreOriginalMapAndPersist(state, session);
	}

	private static async Task RestoreOriginalMapAndPersist(
		RunState state,
		TreeHoleSession session)
	{
		RunManager runManager = RunManager.Instance;
		RestoreOriginalMap(state, session);
		if (state.CurrentRoom is not MapRoom)
		{
			await runManager.EnterRoom(new MapRoom());
		}

		await TreeHoleRunAccessor.PersistCurrentRunTransition(
			runManager,
			$"return from {session.DestinationActName} tree-hole");
	}

	public static void RestoreOriginalMapForArchitect(RunState state, EndlessFinaleSession session)
	{
		TreeHoleFinaleMusicCoordinator.StopBeforeArchitectHandoff(session);

		SessionStore.RemoveFinaleSession(state);
		SessionStore.RemovePendingFinaleEntry(state);
		state.Map = session.OriginalMap;
		state.ClearVisitedMapCoordsDebug();
		foreach (MapCoord coord in session.OriginalVisitedMapCoords)
		{
			state.AddVisitedMapCoord(coord);
		}

		RestoreMapPointHistory(state, session.OriginalMapPointHistory);
		state.ActFloor = session.OriginalActFloor;
		TreeHoleRunAccessor.RestoreActRooms(state, session.OriginalActSave);
		RefreshLocationSynchronizers(state);
		SessionStore.AddPendingArchitectCompletion(state);
		ResetActChangeTransitionMemory();
		Log.Info($"{ModInfo.LogPrefix} Returned from {session.DestinationActName} finale before entering The Architect.");
	}

	public static void RestoreOriginalMapFromFinale(RunState state, EndlessFinaleSession session)
	{
		SessionStore.RemoveFinaleSession(state);
		SessionStore.RemovePendingFinaleEntry(state);
		state.Map = session.OriginalMap;
		state.ClearVisitedMapCoordsDebug();
		foreach (MapCoord coord in session.OriginalVisitedMapCoords)
		{
			state.AddVisitedMapCoord(coord);
		}

		RestoreMapPointHistory(state, session.OriginalMapPointHistory);
		state.ActFloor = session.OriginalActFloor;
		TreeHoleRunAccessor.RestoreActRooms(state, session.OriginalActSave);
		RefreshLocationSynchronizers(state);
		SetMapScreen(session.OriginalMap, state, initMarker: state.CurrentMapCoord.HasValue);
		ResetActChangeTransitionMemory();
		Log.Info($"{ModInfo.LogPrefix} Returned from {session.DestinationActName} finale.");
	}

	// 终局/断章插层从当前幕额外消耗了一次 act 转换，0.108 的 ActChangeSynchronizer 会记住
	// "已从该幕转换过"并拦截后续同幕转换（例如返回后打完本幕 BOSS 的正常进下一幕）。
	// 返回原图时清掉这份记忆，让插层对原版转换语义完全透明。
	private static void ResetActChangeTransitionMemory()
	{
		if (RunManager.Instance?.ActChangeSynchronizer is { } synchronizer)
		{
			IntegratedStrategyFinaleActChangeGuardPatch.ResetTransitionMemory(synchronizer);
		}
	}

	public static void RestoreMapPointHistory(
		RunState state,
		IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> originalHistory)
	{
		if (!TreeHoleRunAccessor.TryGetMapPointHistory(state, out List<List<MapPointHistoryEntry>> mapPointHistory))
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not restore tree-hole map history.");
			return;
		}

		mapPointHistory.Clear();
		foreach (IReadOnlyList<MapPointHistoryEntry> actHistory in originalHistory)
		{
			mapPointHistory.Add(actHistory.ToList());
		}
	}

	public static bool HasVisitedCoord(IEnumerable<MapCoord> visitedCoords, MapCoord coord)
	{
		return visitedCoords.Any(visited => visited.Equals(coord));
	}

	private static bool ShouldWaitForTerminalRewardsProceed(
		SerializableRun save,
		TreeHoleRestoreSnapshot snapshot)
	{
		return snapshot.Kind == TreeHoleSaveKind.TreeHole &&
			save.PreFinishedRoom is { IsPreFinished: true } &&
			HasVisitedCoord(save.VisitedMapCoords, snapshot.TerminalCoord);
	}

	public static bool MapContainsCoord(ActMap map, MapCoord coord)
	{
		return map.HasPoint(coord) ||
			map.StartingMapPoint.coord.Equals(coord) ||
			map.BossMapPoint.coord.Equals(coord) ||
			map.SecondBossMapPoint?.coord.Equals(coord) == true;
	}

	private static bool MatchesSavedTemporaryMap(
		ActMap map,
		TreeHoleRestoreSnapshot snapshot)
	{
		SerializableActMap? expected = snapshot.TemporaryMap ?? CreateLegacyTemporaryMap(snapshot);
		if (expected == null)
		{
			return false;
		}

		SerializableActMap actual = SerializableActMap.FromActMap(map);
		if (actual.GridWidth < expected.GridWidth ||
			actual.GridHeight < expected.GridHeight ||
			!SamePointCoord(actual.StartingPoint, expected.StartingPoint) ||
			!SamePointCoord(actual.BossPoint, expected.BossPoint) ||
			!ContainsExpectedPointCoord(actual.SecondBossPoint, expected.SecondBossPoint) ||
			!ContainsAllCoords(actual.StartMapPointCoords, expected.StartMapPointCoords))
		{
			return false;
		}

		Dictionary<MapCoord, SerializableMapPoint> actualPoints = (actual.Points ?? [])
			.ToDictionary(static point => point.Coord);
		foreach (SerializableMapPoint expectedPoint in expected.Points ?? [])
		{
			if (!actualPoints.TryGetValue(expectedPoint.Coord, out SerializableMapPoint? actualPoint) ||
				!ContainsAllCoords(actualPoint.ChildCoords, expectedPoint.ChildCoords))
			{
				return false;
			}
		}

		return true;
	}

	private static SerializableActMap? CreateLegacyTemporaryMap(TreeHoleRestoreSnapshot snapshot)
	{
		return IntegratedStrategyTreeHoleSaveStateStore.CreateLegacyTemporaryMap(
			snapshot.Kind,
			snapshot.TreeHoleMapSeed);
	}

	private static bool SamePointCoord(
		SerializableMapPoint? left,
		SerializableMapPoint? right)
	{
		return left != null && right != null && left.Coord.Equals(right.Coord);
	}

	private static bool ContainsExpectedPointCoord(
		SerializableMapPoint? actual,
		SerializableMapPoint? expected)
	{
		return expected == null || actual != null && actual.Coord.Equals(expected.Coord);
	}

	private static bool ContainsAllCoords(
		IReadOnlyCollection<MapCoord>? actual,
		IReadOnlyCollection<MapCoord>? expected)
	{
		return expected == null || expected.Count == 0 ||
			actual != null && expected.All(actual.Contains);
	}

	public static void RefreshLocationSynchronizers(RunState state)
	{
		RunManager.Instance.MapSelectionSynchronizer.OnLocationChanged(state.MapLocation);
		RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(state.RunLocation);
	}

	public static void SetMapScreen(ActMap map, RunState state, bool initMarker)
	{
		NMapScreen? mapScreen = NMapScreen.Instance;
		if (mapScreen == null)
		{
			return;
		}

		mapScreen.SetMap(map, state.Rng.Seed, clearDrawings: true);
		if (initMarker && state.CurrentMapCoord is { } currentCoord && map.HasPoint(currentCoord))
		{
			mapScreen.InitMarker(currentCoord);
		}

		mapScreen.SetTravelEnabled(true);
		mapScreen.RefreshAllMapPointVotes();
	}

	public static async Task AwaitNextProcessFrame()
	{
		if (NGame.Instance != null)
		{
			await NGame.Instance.AwaitProcessFrame();
			return;
		}

		await Task.Yield();
	}

	private static bool IsCurrentFinaleMap(ActMap map, SpecialFinaleKind kind)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		return state != null &&
			SessionStore.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == kind &&
			IsSessionFinaleMap(state, session, map);
	}

	// 存档往返后 state.Map 会被反序列化为新的 SavedActMap 实例，纯引用比较会失配
	// （表现为终局层的 BOSS 节点/起点样式修正不生效）；引用不同时退回
	// "当前地图 + 拓扑一致"判断。
	private static bool IsSessionFinaleMap(RunState state, EndlessFinaleSession session, ActMap map)
	{
		if (ReferenceEquals(session.FinaleMap, map))
		{
			return true;
		}

		return ReferenceEquals(state.Map, map) &&
			map.GetColumnCount() == session.FinaleMap.GetColumnCount() &&
			map.GetRowCount() == session.FinaleMap.GetRowCount() &&
			map.BossMapPoint.coord.Equals(session.FinaleMap.BossMapPoint.coord) &&
			map.StartingMapPoint.coord.Equals(session.FinaleMap.StartingMapPoint.coord);
	}

	private static void RestoreOriginalMap(RunState state, TreeHoleSession session)
	{
		SessionStore.RemoveCompletionSuppression(state);
		SessionStore.RemoveTerminalRewardsProceeded(state);
		SessionStore.RemoveTreeHoleSession(state);
		SessionStore.RemovePendingTreeHoleReturn(state);
		state.Map = session.OriginalMap;
		state.ClearVisitedMapCoordsDebug();
		foreach (MapCoord coord in session.OriginalVisitedMapCoords)
		{
			state.AddVisitedMapCoord(coord);
		}

		RestoreMapPointHistory(state, session.OriginalMapPointHistory);
		state.ActFloor = session.OriginalActFloor;
		TreeHoleRunAccessor.RestoreActRooms(state, session.OriginalActSave);
		RefreshLocationSynchronizers(state);
		SetMapScreen(session.OriginalMap, state, initMarker: state.CurrentMapCoord.HasValue);
		Log.Info($"{ModInfo.LogPrefix} Returned from {session.DestinationActName} tree-hole.");
	}

	private static bool RequestRestoreOriginalMap(RunState state, TreeHoleSession session)
	{
		if (!SessionStore.AddPendingTreeHoleReturn(state))
		{
			// 入队的同步返回动作可能丢失/被吞（掉线滞留、战斗收尾期被取消、执行时
			// 异常被静默记录）；超时后清除标志重发，而不是永远等待。
			if (!SessionStore.IsPendingTreeHoleReturnExpired(state, TimeSpan.FromSeconds(5)))
			{
				return true;
			}

			Log.Warn($"{ModInfo.LogPrefix} Tree-hole return request seems lost; retrying.");
			SessionStore.RemovePendingTreeHoleReturn(state);
			SessionStore.AddPendingTreeHoleReturn(state);
		}

		Player? requester = state.Players.FirstOrDefault(static player => player.IsActiveForHooks) ??
			state.Players.FirstOrDefault();
		if (requester == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Falling back to local tree-hole return because no player was available.");
			RestoreOriginalMap(state, session);
			return true;
		}

		IntegratedStrategyTemporaryMapAction.EnqueueTreeHoleReturn(requester);
		Log.Info($"{ModInfo.LogPrefix} Queued synced return from {session.DestinationActName} tree-hole.");
		return true;
	}

	private static List<IReadOnlyList<MapPointHistoryEntry>> CopyHistoryForParentAct(
		IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> source,
		IReadOnlyList<int> counts,
		int savedParentActIndex,
		int currentParentActIndex)
	{
		List<IReadOnlyList<MapPointHistoryEntry>> result = source
			.Select(static history => (IReadOnlyList<MapPointHistoryEntry>)history.ToList())
			.ToList();
		if (savedParentActIndex < 0 ||
			savedParentActIndex >= counts.Count ||
			currentParentActIndex < 0 ||
			currentParentActIndex >= source.Count)
		{
			return result;
		}

		IReadOnlyList<MapPointHistoryEntry> parentHistory = source[currentParentActIndex];
		int parentHistoryCount = Math.Min(
			Math.Max(counts[savedParentActIndex], 0),
			parentHistory.Count);
		result[currentParentActIndex] = parentHistory.Take(parentHistoryCount).ToList();
		return result;
	}
}
