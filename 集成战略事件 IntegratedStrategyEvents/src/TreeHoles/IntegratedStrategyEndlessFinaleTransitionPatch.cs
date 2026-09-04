using HarmonyLib;
using IntegratedStrategyEvents.Events;
using IntegratedStrategyEvents.Map;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleEnterNextActPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyEndlessFinaleEnterNextActPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(RunManager __instance, ref Task __result)
	{
		return IntegratedStrategyTreeHoleController.HandleEnterNextAct(__instance, ref __result);
	}

	private static void Postfix(RunManager __instance, ref Task __result)
	{
		if (__instance.DebugOnlyGetState() is RunState state &&
			TreeHoleSessionManager.HasPendingArchitectCompletion(state))
		{
			__result = SpecialFinaleCoordinator.PersistArchitectHandoffAfterEnterNextAct(
				__instance,
				state,
				__result);
		}
	}
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
[IntegratedStrategyPatch("IntegratedStrategyTemporaryMapLoadRoomPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyTemporaryMapLoadRoomPatch
{
	[HarmonyPriority(Priority.First)]
	private static void Prefix(
		RunManager __instance,
		ref AbstractRoom? preFinishedRoom,
		out ResumeLoadState? __state)
	{
		__state = null;
		if (__instance.DebugOnlyGetState() is not RunState state)
		{
			return;
		}

		if (TreeHoleSessionManager.HasPendingArchitectCompletion(state))
		{
			if (preFinishedRoom is not EventRoom { CanonicalEvent: TheArchitect })
			{
				preFinishedRoom = new EventRoom(ModelDb.Event<TheArchitect>());
			}
			__state = new ResumeLoadState(state, TreeHoleResumeRoom.Architect);
			return;
		}

		if (!TreeHoleSessionManager.HasPendingMapRoomResume(state) &&
			preFinishedRoom != null &&
			TryGetRequiredEternalDustEvent(state, out EventModel requiredEvent) &&
			(preFinishedRoom is not EventRoom eventRoom ||
			 eventRoom.CanonicalEvent.GetType() != requiredEvent.GetType()))
		{
			Log.Info(
				$"{ModInfo.LogPrefix} Replaced a mismatched saved room with the required " +
				$"Eternal Dust event {requiredEvent.Id.Entry}.");
			preFinishedRoom = new EventRoom(requiredEvent);
			state.AddVisitedEvent(requiredEvent);
		}

		if (TreeHoleSessionManager.HasPendingMapRoomResume(state))
		{
			preFinishedRoom = new MapRoom();
			__state = new ResumeLoadState(state, TreeHoleResumeRoom.Map);
		}
	}

	private static bool TryGetRequiredEternalDustEvent(
		RunState state,
		out EventModel eventModel)
	{
		if (IntegratedStrategyTreeHoleController.IsAtEternalDustFirstEventPoint(state))
		{
			eventModel = ModelDb.Event<ReconstructionEvent>();
			return true;
		}

		if (IntegratedStrategyTreeHoleController.IsAtEternalDustSecondEventPoint(state))
		{
			eventModel = ModelDb.Event<ExplorerSmallStepEvent>();
			return true;
		}

		eventModel = null!;
		return false;
	}

	private static void Postfix(
		RunManager __instance,
		ResumeLoadState? __state,
		ref Task __result)
	{
		if (__state != null)
		{
			__result = FinishResume(__instance, __state, __result);
		}
	}

	private static async Task FinishResume(
		RunManager runManager,
		ResumeLoadState resumeState,
		Task loadTask)
	{
		await loadTask;
		if (!ReferenceEquals(runManager.DebugOnlyGetState(), resumeState.State))
		{
			return;
		}

		if (resumeState.Room == TreeHoleResumeRoom.Map)
		{
			TreeHoleSessionManager.RemovePendingMapRoomResume(resumeState.State);
			Log.Info($"{ModInfo.LogPrefix} Resumed the saved run in its map room.");
		}
		else
		{
			Log.Info($"{ModInfo.LogPrefix} Resumed the saved finale Architect handoff.");
		}
	}

	private sealed record ResumeLoadState(RunState State, TreeHoleResumeRoom Room);
}

[HarmonyPatch(typeof(EventModel), "SetEventState")]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleArchitectOptionsPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyEndlessFinaleArchitectOptionsPatch
{
	[HarmonyPriority(Priority.Last)]
	[HarmonyAfter("Act4Placeholder")]
	private static void Postfix(EventModel __instance)
	{
		IntegratedStrategyTreeHoleController.SuppressArchitectActChangeOptions(__instance);
	}
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleArchitectOptionDisplayPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyEndlessFinaleArchitectOptionDisplayPatch
{
	[HarmonyPriority(Priority.Last)]
	[HarmonyAfter("Act4Placeholder")]
	private static void Prefix(EventModel ____event, ref IEnumerable<EventOption> options)
	{
		options = IntegratedStrategyTreeHoleController.FilterArchitectActChangeOptionsForDisplay(
			____event,
			options);
	}
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleArchitectOptionClickPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyEndlessFinaleArchitectOptionClickPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(EventModel ____event, EventOption option)
	{
		return IntegratedStrategyTreeHoleController.ShouldChooseArchitectOption(____event, option);
	}
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleCreateRoomPatch", "temporary-map", "本模组树洞或终局会话")]
internal static class IntegratedStrategyEndlessFinaleCreateRoomPatch
{
	[HarmonyPriority(Priority.Low)]
	private static bool Prefix(
		ref RoomType roomType,
		MapPointType mapPointType,
		AbstractModel? model,
		ref AbstractRoom __result)
	{
		if (!IntegratedStrategyForcedRoomController.HandleCreateRoom(roomType, mapPointType, model, ref __result))
		{
			return false;
		}

		return IntegratedStrategyTreeHoleController.HandleCreateRoom(roomType, model, ref __result);
	}

	[HarmonyPriority(Priority.Last)]
	private static void Postfix(
		RoomType roomType,
		AbstractModel? model,
		ref AbstractRoom __result)
	{
		IntegratedStrategyTreeHoleController.EnsureCreatedRoomIsEndlessFinaleBoss(roomType, model, ref __result);
	}
}

[HarmonyPatch(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready))]
[IntegratedStrategyPatch("IntegratedStrategyEndlessFinaleBossMapPointPatch", "map-ui", "本模组地图显示")]
internal static class IntegratedStrategyEndlessFinaleBossMapPointPatch
{
	private static void Prefix(NBossMapPoint __instance, out BossNodeRenderSwap? __state)
	{
		__state = IntegratedStrategyTreeHoleController.BeginEndlessFinaleBossNodeRender(__instance.Point);
	}

	private static void Postfix(BossNodeRenderSwap? __state)
	{
		IntegratedStrategyTreeHoleController.EndEndlessFinaleBossNodeRender(__state);
	}
}
