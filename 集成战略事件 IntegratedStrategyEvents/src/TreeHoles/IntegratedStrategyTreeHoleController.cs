using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class IntegratedStrategyTreeHoleController
{
	private static bool _installed;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}

		RunManager.Instance.RunStarted += OnRunStarted;
		RunManager.Instance.RoomEntered += OnRoomEntered;
		_installed = true;
	}

	public static bool IsActive(IRunState? runState)
	{
		return TreeHoleSessionManager.IsActive(runState);
	}

	public static bool IsActiveCurrentRun()
	{
		return TreeHoleSessionManager.IsActiveCurrentRun();
	}

	/// <summary>
	/// 该地图是否属于模组自建的临时层（树洞 / 终局 / 断章），或当前跑局正处于临时层。
	/// 类型层面看 <see cref="IIntegratedStrategyTemporaryActMap"/> 标记，
	/// 存档往返后类型信息丢失（SavedActMap）则退回会话拓扑判定。
	/// </summary>
	public static bool IsTemporaryMap(IRunState? runState, ActMap map)
	{
		return IsActive(runState) ||
			map is IIntegratedStrategyTemporaryActMap ||
			TreeHoleSessionManager.IsCurrentAnyTemporaryMap(map);
	}

	public static bool TryRestoreCompletedCurrentRun()
	{
		return TreeHoleSessionManager.TryRestoreCompletedCurrentRun();
	}

	public static bool TryRestoreCompletedCurrentRunAfterTerminalProceed()
	{
		return TreeHoleSessionManager.TryRestoreCompletedCurrentRunAfterTerminalProceed();
	}

	public static void MarkTerminalRewardsProceededCurrentRun()
	{
		TreeHoleSessionManager.MarkTerminalRewardsProceededCurrentRun();
	}

	public static void HandleTerminalTreasureRoomProceed()
	{
		TreeHoleSessionManager.HandleTerminalTreasureRoomProceed();
	}

	public static bool TryGetCurrentDestination(out string actName)
	{
		return TreeHoleSessionManager.TryGetCurrentDestination(out actName);
	}

	public static bool TryGetCurrentDestination(out string stageLabel, out string actName)
	{
		return TreeHoleSessionManager.TryGetCurrentDestination(out stageLabel, out actName);
	}

	public static TreeHoleSaveSnapshot? GetSaveSnapshot(RunState? state)
	{
		return TreeHoleSessionManager.GetSaveSnapshot(state);
	}

	public static void QueueRestoreFromSave(SerializableRun save, RunState state)
	{
		TreeHoleSessionManager.QueueRestoreFromSave(save, state);
	}

	public static bool TryRestoreSavedSessionForCurrentRun(
		ActMap map,
		bool allowMapRebuild = true)
	{
		return TreeHoleSessionManager.TryRestoreSavedSessionForCurrentRun(map, allowMapRebuild);
	}

	public static bool IsCurrentTreeHoleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentTreeHoleMap(map);
	}

	public static bool IsCurrentEndlessFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentEndlessFinaleMap(map);
	}

	public static bool IsCurrentEternalDustFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentEternalDustFinaleMap(map);
	}

	public static bool IsCurrentRadiantApexFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentRadiantApexFinaleMap(map);
	}

	public static bool IsCurrentCarefreeViharaFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentCarefreeViharaFinaleMap(map);
	}

	public static bool IsCurrentDesireHallFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentDesireHallFinaleMap(map);
	}

	public static bool IsCurrentAbyssalJungleFinaleMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentAbyssalJungleFinaleMap(map);
	}

	public static bool IsCurrentProphetHornFragmentMap(ActMap map)
	{
		return TreeHoleSessionManager.IsCurrentProphetHornFragmentMap(map);
	}

	public static bool IsAtEternalDustFirstEventPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtEternalDustFirstEventPoint(state);
	}

	public static bool IsAtEternalDustSecondEventPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtEternalDustSecondEventPoint(state);
	}

	public static bool IsAtRadiantApexCombatPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtRadiantApexCombatPoint(state);
	}

	public static bool IsAtAbyssalJungleSublimationEventPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtAbyssalJungleSublimationEventPoint(state);
	}

	public static bool IsAtAbyssalJungleOdeEventPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtAbyssalJungleOdeEventPoint(state);
	}

	public static bool IsAtProphetHornFragmentEventPoint(RunState state)
	{
		return SpecialFinaleCoordinator.IsAtProphetHornFragmentEventPoint(state);
	}

	public static Task EnterFromEvent(Player owner)
	{
		return TreeHoleEntryCoordinator.EnterFromEvent(owner);
	}

	public static Task EnterFromEvent(Player owner, string destinationActName)
	{
		return TreeHoleEntryCoordinator.EnterFromEvent(owner, destinationActName);
	}

	public static Task EnterFromEvent(Player owner, string destinationActName, string stageLabel)
	{
		return TreeHoleEntryCoordinator.EnterFromEvent(owner, destinationActName, stageLabel);
	}

	public static Task EnterProphetHornFragmentFromEvent(Player owner, string destinationActName, string stageLabel)
	{
		return SpecialFinaleCoordinator.EnterProphetHornFragmentFromEvent(owner, destinationActName, stageLabel);
	}

	public static Task EnterFromDebugCommand(Player owner, string destinationActName, string stageLabel)
	{
		return TreeHoleEntryCoordinator.EnterFromDebugCommand(owner, destinationActName, stageLabel);
	}

	public static bool HandleEnterNextAct(RunManager runManager, ref Task result)
	{
		return SpecialFinaleCoordinator.HandleEnterNextAct(runManager, ref result);
	}

	// 终局插层期间（或建筑师交接待办时）允许从同一幕序号再次转换，
	// 供 0.108 新增的 ActChangeSynchronizer 重复转换守卫放行。
	public static bool ShouldAllowRepeatedActTransition()
	{
		return SpecialFinaleCoordinator.ShouldAllowRepeatedActTransition();
	}

	public static void SuppressArchitectActChangeOptions(EventModel eventModel)
	{
		SpecialFinaleCoordinator.SuppressArchitectActChangeOptions(eventModel);
	}

	public static IEnumerable<EventOption> FilterArchitectActChangeOptionsForDisplay(
		EventModel eventModel,
		IEnumerable<EventOption> options)
	{
		return SpecialFinaleCoordinator.FilterArchitectActChangeOptionsForDisplay(eventModel, options);
	}

	public static bool ShouldChooseArchitectOption(EventModel eventModel, EventOption option)
	{
		return SpecialFinaleCoordinator.ShouldChooseArchitectOption(eventModel, option);
	}

	public static bool HandleCreateRoom(RoomType roomType, AbstractModel? model, ref AbstractRoom result)
	{
		return SpecialFinaleCoordinator.HandleCreateRoom(roomType, model, ref result);
	}

	public static void EnsureCreatedRoomIsEndlessFinaleBoss(
		RoomType roomType,
		AbstractModel? model,
		ref AbstractRoom result)
	{
		SpecialFinaleCoordinator.EnsureCreatedRoomIsEndlessFinaleBoss(roomType, model, ref result);
	}

	public static BossNodeRenderSwap? BeginEndlessFinaleBossNodeRender(MapPoint point)
	{
		return SpecialFinaleCoordinator.BeginEndlessFinaleBossNodeRender(point);
	}

	public static void EndEndlessFinaleBossNodeRender(BossNodeRenderSwap? swap)
	{
		SpecialFinaleCoordinator.EndEndlessFinaleBossNodeRender(swap);
	}

	private static void OnRunStarted(RunState state)
	{
		TreeHoleFinaleMusicCoordinator.StopForRunReset();
		TreeHoleSessionManager.OnRunStarted(state);
	}

	private static void OnRoomEntered()
	{
		SpecialFinaleCoordinator.OnRoomEntered();
		TreeHoleSessionManager.OnRoomEntered();
	}
}
