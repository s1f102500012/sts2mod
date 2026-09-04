using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace AITeammate.Scripts;

internal static class AiTeammateEventSafetyPatches
{
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
    private static class ActModelGenerateRoomsPatch
    {
        private static void Postfix(ActModel __instance)
        {
            if (AiTeammateSessionRegistry.ActiveRunSession == null)
            {
                return;
            }

            ExcludeUnsafeEvent<CrystalSphere>(__instance);
        }

        private static void ExcludeUnsafeEvent<TEvent>(ActModel actModel)
            where TEvent : EventModel
        {
            EventModel canonicalEvent = ModelDb.Event<TEvent>();
            bool wasPresent = actModel.AllEvents.Any(eventModel => eventModel.Id == canonicalEvent.Id) ||
                              ModelDb.AllSharedEvents.Any(eventModel => eventModel.Id == canonicalEvent.Id);
            if (!wasPresent)
            {
                return;
            }

            actModel.RemoveEventFromSet(canonicalEvent);
            Log.Info($"[AITeammate][EventSafety] Excluded {canonicalEvent.GetType().Name} from generated event pool for AI teammate session.");
        }
    }

    [HarmonyPatch(typeof(EventSynchronizer), "ChooseOptionForEvent")]
    private static class EventSynchronizerChooseOptionForEventPatch
    {
        private static bool Prefix(EventSynchronizer __instance, Player player, ref int optionIndex)
        {
            if (AiTeammateSessionRegistry.ActiveRunSession == null)
            {
                return true;
            }

            EventModel eventForPlayer;
            try
            {
                eventForPlayer = __instance.GetEventForPlayer(player);
            }
            catch
            {
                return true;
            }

            if (eventForPlayer.IsFinished)
            {
                Log.Info($"[AITeammate][EventSafety] Skipping shared event option for finished player={player.NetId} event={eventForPlayer.Id} requestedOption={optionIndex}.");
                return false;
            }

            if (optionIndex >= 0 &&
                optionIndex < eventForPlayer.CurrentOptions.Count &&
                !eventForPlayer.CurrentOptions[optionIndex].IsLocked)
            {
                return true;
            }

            int fallbackIndex = FindFirstUnlockedEventOption(eventForPlayer);
            if (fallbackIndex < 0)
            {
                Log.Warn($"[AITeammate][EventSafety] Skipping invalid shared event option for player={player.NetId} event={eventForPlayer.Id} requestedOption={optionIndex}; no unlocked options remain.");
                return false;
            }

            Log.Warn($"[AITeammate][EventSafety] Replacing invalid shared event option for player={player.NetId} event={eventForPlayer.Id} requestedOption={optionIndex} fallbackOption={fallbackIndex}.");
            optionIndex = fallbackIndex;
            return true;
        }

        private static int FindFirstUnlockedEventOption(EventModel eventModel)
        {
            for (int index = 0; index < eventModel.CurrentOptions.Count; index++)
            {
                EventOption option = eventModel.CurrentOptions[index];
                if (!option.IsLocked)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
