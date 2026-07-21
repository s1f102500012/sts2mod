using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class IntegratedStrategyTreeHoleSavePatch
{
	[HarmonyBefore(ModInfo.RitsuLibCoreHarmonyId)]
	private static void Postfix(SerializableRun __result)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null)
		{
			return;
		}

		TreeHoleSaveSnapshot? snapshot = IntegratedStrategyTreeHoleController.GetSaveSnapshot(state);
		TreeHoleResumeRoom resumeRoom = TreeHoleRunAccessor.GetRequestedResumeRoomForSave();
		if (TreeHoleSessionManager.HasPendingArchitectCompletion(state))
		{
			resumeRoom = TreeHoleResumeRoom.Architect;
		}
		else if (resumeRoom == TreeHoleResumeRoom.None && state.CurrentRoom is MapRoom)
		{
			resumeRoom = TreeHoleResumeRoom.Map;
		}

		if (snapshot == null)
		{
			if (resumeRoom != TreeHoleResumeRoom.None)
			{
				IntegratedStrategyTreeHoleSaveStateStore.SaveResumeRoom(
					state,
					__result,
					resumeRoom,
					state.Map);
				return;
			}

			IntegratedStrategyTreeHoleSaveStateStore.RemoveFromSave(state);
			return;
		}

		int currentActIndex = __result.CurrentActIndex;
		if (currentActIndex >= 0 && currentActIndex < __result.Acts.Count)
		{
			__result.Acts[currentActIndex].SavedMap = SerializableActMap.FromActMap(snapshot.CurrentMap);
		}

		__result.VisitedMapCoords = snapshot.CurrentVisitedMapCoords.ToList();
		__result.MapPointHistory = snapshot.CurrentMapPointHistory
			.Select(static history => history.ToList())
			.ToList();
		__result.MapDrawings = null;
		IntegratedStrategyTreeHoleSaveStateStore.Save(state, __result, snapshot, resumeRoom);

		Log.Info($"{ModInfo.LogPrefix} Saved active tree-hole run at the temporary map location.");
	}
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class IntegratedStrategyTreeHoleLoadPatch
{
	[HarmonyAfter(ModInfo.RitsuLibCoreHarmonyId)]
	private static void Postfix(SerializableRun save, RunState __result)
	{
		IntegratedStrategyTreeHoleController.QueueRestoreFromSave(save, __result);
	}
}
