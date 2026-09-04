using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateTestMapPatches
{
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap))]
    private static class ActModelCreateMapPatch
    {
        private static void Postfix(RunState runState, ref ActMap __result)
        {
            if (!AiTeammateSessionRegistry.ShouldUseTestMap(runState))
            {
                return;
            }

            __result = new AiTeammateTestActMap(runState.CurrentActIndex);
            Log.Info($"[AITeammate] Replaced generated Act {runState.CurrentActIndex + 1} map with AI teammate test map.");
        }
    }

    [HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
    private static class RunManagerRollRoomTypeForPatch
    {
        private static bool Prefix(MapPointType pointType, ref RoomType __result)
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (pointType != MapPointType.Unknown || !AiTeammateSessionRegistry.ShouldUseTestMap(runState))
            {
                return true;
            }

            __result = RoomType.Event;
            Log.Info("[AITeammate] Forced test-map unknown room to resolve as an Event room.");
            return false;
        }
    }

    [HarmonyPatch(typeof(RunManager), "CreateRoom")]
    private static class RunManagerCreateRoomPatch
    {
        private static bool Prefix(RoomType roomType, MapPointType mapPointType, AbstractModel? model, ref AbstractRoom __result)
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (!AiTeammateSessionRegistry.ShouldUseTestMap(runState) ||
                roomType != RoomType.Event ||
                mapPointType != MapPointType.Unknown)
            {
                return true;
            }

            EventModel? forcedEvent = GetForcedTestMapEvent(runState?.CurrentMapCoord);
            if (forcedEvent == null)
            {
                return true;
            }

            __result = new EventRoom((model as EventModel) ?? forcedEvent);
            Log.Info($"[AITeammate] Forced test-map event branch to create {forcedEvent.GetType().Name} at coord={runState?.CurrentMapCoord?.col},{runState?.CurrentMapCoord?.row}.");
            return false;
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
    private static class RunManagerGenerateRoomsPatch
    {
        private const int TestMapStartingGold = 999;

        private static void Postfix()
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (!AiTeammateSessionRegistry.ShouldUseTestMap(runState) || runState == null)
            {
                return;
            }

            foreach (var player in runState.Players)
            {
                player.Gold = TestMapStartingGold;
                Log.Info($"[AITeammate] Seeded test-map starting gold player={player.NetId} gold={TestMapStartingGold}");
            }
        }
    }

    private static EventModel? GetForcedTestMapEvent(MapCoord? coord)
    {
        if (AiTeammateTestActMap.IsAromaOfChaosCoord(coord))
        {
            return ModelDb.Event<AromaOfChaos>();
        }

        if (AiTeammateTestActMap.IsDrowningBeaconCoord(coord))
        {
            return ModelDb.Event<DrowningBeacon>();
        }

        if (AiTeammateTestActMap.IsWellspringCoord(coord))
        {
            return ModelDb.Event<Wellspring>();
        }

        if (AiTeammateTestActMap.IsFakeMerchantCoord(coord))
        {
            return ModelDb.Event<FakeMerchant>();
        }

        return null;
    }
}
