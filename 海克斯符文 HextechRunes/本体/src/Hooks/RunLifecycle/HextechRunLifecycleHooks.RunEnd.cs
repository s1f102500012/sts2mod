using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{


	private static void EnsureCombatHistoryBeforeLossSave(RunManager runManager, bool isVictory)
	{
		if (isVictory)
		{
			return;
		}

		try
		{
			if (runManager.DebugOnlyGetState() is not RunState runState
				|| runState.CurrentRoom is not CombatRoom combatRoom
				|| runState.CurrentMapPointHistoryEntry is not MapPointHistoryEntry mapPointHistory)
			{
				return;
			}

			MapPointRoomHistoryEntry? roomHistory = mapPointHistory.Rooms.Count > 0 ? mapPointHistory.Rooms[^1] : null;
			if (roomHistory == null || !roomHistory.RoomType.IsCombatRoom())
			{
				roomHistory = new MapPointRoomHistoryEntry
				{
					RoomType = combatRoom.RoomType,
					ModelId = combatRoom.ModelId
				};
				mapPointHistory.Rooms.Add(roomHistory);
				Log.Warn($"[{ModInfo.Id}][Mayhem] Added missing combat room history before loss save: encounter={combatRoom.ModelId.Entry}");
			}

			if (roomHistory.ModelId == null)
			{
				roomHistory.ModelId = combatRoom.ModelId;
				Log.Warn($"[{ModInfo.Id}][Mayhem] Filled missing combat encounter id before loss save: encounter={combatRoom.ModelId.Entry}");
			}

			if (roomHistory.MonsterIds == null)
			{
				roomHistory.MonsterIds = [];
			}

			if (roomHistory.MonsterIds.Count == 0)
			{
				foreach ((MonsterModel monster, _) in combatRoom.Encounter.MonstersWithSlots)
				{
					roomHistory.MonsterIds.Add(monster.Id);
				}
				Log.Warn($"[{ModInfo.Id}][Mayhem] Filled missing combat monster ids before loss save: encounter={combatRoom.ModelId.Entry} count={roomHistory.MonsterIds.Count}");
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] Failed to sanitize combat history before loss save: {ex}");
		}
	}

	[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded), typeof(bool))]
	[HextechPatch("run.end", "跑局结束统计")]
	private static class RunEndedPatch
	{
		[HarmonyPrefix]
		private static void Prefix(RunManager __instance, bool isVictory, out RunState? __state)
		{
			EnsureCombatHistoryBeforeLossSave(__instance, isVictory);
			__state = __instance.DebugOnlyGetState();
		}

		[HarmonyPostfix]
		private static void Postfix(RunState? __state, bool isVictory, SerializableRun __result)
		{
			HextechCombatHooks.ResetTransientCombatState();
			HextechEnemyHexEffects.ResetAllRunScopedState();
			HextechGoldrendSync.ClearRun(__state);
			HextechTelemetry.OnRunEnded(__state, __result, isVictory);
		}
	}
}
