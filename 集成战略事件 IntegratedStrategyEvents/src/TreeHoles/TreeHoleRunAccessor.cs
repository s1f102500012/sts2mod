using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleRunAccessor
{
	private static readonly AsyncLocal<TreeHoleResumeRoom> ResumeRoomForSave = new();
	private static readonly MethodInfo? RunManagerClearScreensMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(RunManager), "ClearScreens");
	private static readonly MethodInfo? RunManagerFadeInMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(RunManager), "FadeIn");
	private static readonly MethodInfo? RunManagerExitCurrentRoomsMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(RunManager), "ExitCurrentRooms");
	private static readonly MethodInfo? RunManagerEnterRoomInternalMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(RunManager), "EnterRoomInternal");
	private static readonly MethodInfo? RunManagerWinRunMethod =
		IntegratedStrategyPrivateMembers.Method(typeof(RunManager), "WinRun");
	private static readonly FieldInfo? ActRoomsField =
		IntegratedStrategyPrivateMembers.Field(typeof(ActModel), "_rooms");
	private static readonly FieldInfo? MapPointHistoryField =
		IntegratedStrategyPrivateMembers.Field(typeof(RunState), "_mapPointHistory");

	public static void ClearScreens(RunManager runManager)
	{
		IntegratedStrategyPresentation.Run(() => RunManagerClearScreensMethod?.Invoke(runManager, null), "clear transition screens");
	}

	public static Task FadeOut() => IntegratedStrategyPresentation.RunAsync(async () =>
	{
		if (MegaCrit.Sts2.Core.TestSupport.TestMode.IsOff && MegaCrit.Sts2.Core.Nodes.NGame.Instance != null)
			await MegaCrit.Sts2.Core.Nodes.NGame.Instance.Transition.RoomFadeOut();
	}, "fade out temporary map");

	public static async Task FadeIn(RunManager runManager, bool showTransition)
	{
		await IntegratedStrategyPresentation.RunAsync(async () =>
		{
			if (RunManagerFadeInMethod?.Invoke(runManager, [showTransition]) is Task task) await task;
		}, "fade in temporary map");
	}

	public static async Task ExitCurrentRooms(RunManager runManager)
	{
		if (RunManagerExitCurrentRoomsMethod?.Invoke(runManager, null) is Task task)
		{
			await task;
		}
	}

	public static async Task EnterRoomInternal(RunManager runManager, AbstractRoom room)
	{
		if (RunManagerEnterRoomInternalMethod?.Invoke(runManager, [room, false]) is Task task)
		{
			await task;
			return;
		}

		await runManager.EnterRoom(room);
	}

	public static async Task PersistCurrentRunTransition(
		RunManager runManager,
		string transitionDescription,
		TreeHoleResumeRoom resumeRoom = TreeHoleResumeRoom.Map)
	{
		TreeHoleResumeRoom previousResumeRoom = ResumeRoomForSave.Value;
		ResumeRoomForSave.Value = resumeRoom;
		try
		{
			SerializableRun stagedSave = runManager.ToSave(null);
			runManager.CombatReplayWriter.RecordInitialState(stagedSave);
			await SaveManager.Instance.SaveRun(null);
			Log.Info($"{ModInfo.LogPrefix} Completed persistence request for {transitionDescription}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to persist {transitionDescription}: {ex}");
		}
		finally
		{
			ResumeRoomForSave.Value = previousResumeRoom;
		}
	}

	internal static TreeHoleResumeRoom GetRequestedResumeRoomForSave()
	{
		return ResumeRoomForSave.Value;
	}

	public static bool TryWinRun(RunManager runManager, out Task task)
	{
		if (RunManagerWinRunMethod?.Invoke(runManager, null) is Task winRunTask)
		{
			task = winRunTask;
			return true;
		}

		task = Task.CompletedTask;
		return false;
	}

	public static void RestoreActRooms(RunState state, SerializableActModel originalActSave)
	{
		if (ActRoomsField == null)
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not restore act room set.");
			return;
		}

		// 树洞/终局临时层不切换 Act，层内战斗消耗的是同一个 RoomSet 的遭遇战队列。
		// 整体回滚快照会把普通/精英遭遇计数一并回卷，导致返回大地图后重复遇到
		// 层内刚打过的敌人（玩家反馈）。恢复后保留两个战斗计数的较大值；
		// 事件/BOSS 计数与事件列表仍按快照回滚（层内事件是强制换入的，不占主图轮换）。
		RoomSet restored = RoomSet.FromSave(originalActSave.SerializableRooms);
		if (ActRoomsField.GetValue(state.Act) is RoomSet liveRooms)
		{
			restored.normalEncountersVisited =
				Math.Max(restored.normalEncountersVisited, liveRooms.normalEncountersVisited);
			restored.eliteEncountersVisited =
				Math.Max(restored.eliteEncountersVisited, liveRooms.eliteEncountersVisited);
		}

		ActRoomsField.SetValue(state.Act, restored);
	}

	public static bool TryGetMapPointHistory(
		RunState state,
		out List<List<MapPointHistoryEntry>> mapPointHistory)
	{
		if (MapPointHistoryField?.GetValue(state) is List<List<MapPointHistoryEntry>> history)
		{
			mapPointHistory = history;
			return true;
		}

		mapPointHistory = [];
		return false;
	}
}
