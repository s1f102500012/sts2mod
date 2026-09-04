using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateRewardPatches
{
    [HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.Offer))]
    private static class RewardsSetOfferPatch
    {
        private static bool Prefix(RewardsSet __instance, ref Task __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(__instance.Player))
            {
                return true;
            }

            __result = LocalContext.IsMe(__instance.Player)
                ? AiTeammateDummyController.ExecuteDeterministicLocalRewardSetAsync(__instance)
                : AiTeammateDummyController.ExecuteDeterministicRewardSetAsync(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.OfferForRoomEnd))]
    private static class RewardsCmdOfferForRoomEndPatch
    {
        private static void Postfix(Player player, AbstractRoom room, ref Task __result)
        {
            __result = OfferForAiTeammatesAfterHostAsync(__result, player, room);
        }

        private static async Task OfferForAiTeammatesAfterHostAsync(Task originalTask, Player player, AbstractRoom room)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null)
            {
                await originalTask;
                if (AiTeammateSessionRegistry.AutopilotEnabled &&
                    AiTeammateDummyController.CanUseDirectSelectionAutomation(player) &&
                    player.RunState.Players.Count <= 1 &&
                    room is CombatRoom standaloneCombatRoom)
                {
                    await AiTeammateDummyController.ProceedAfterDeterministicTerminalRewardsAsync(player, standaloneCombatRoom);
                }

                return;
            }

            if (player.NetId != session.HostPlayerId)
            {
                await originalTask;
                return;
            }

            if (room is TreasureRoom)
            {
                await originalTask;
                return;
            }

            AiTeammateSessionParticipant[] aiParticipants = session.Participants
                .Where(static participant => !participant.IsHost)
                .ToArray();

            await AwaitHostRoomEndRewardsAsync(originalTask, player, room);
            Log.Info($"[AITeammate] Starting room-end AI reward fanout room={room.GetType().Name} roomCount={player.RunState.CurrentRoomCount} aiCount={aiParticipants.Length}");
            try
            {
                foreach (AiTeammateSessionParticipant participant in aiParticipants)
                {
                    await OfferRoomEndRewardsForAiParticipantAsync(player, room, participant);
                }
            }
            catch (Exception exception)
            {
                Log.Warn($"[AITeammate] Room-end AI reward fanout failed room={room.GetType().Name} roomCount={player.RunState.CurrentRoomCount}: {exception}");
            }
            Log.Info($"[AITeammate] Finished room-end AI reward fanout room={room.GetType().Name} roomCount={player.RunState.CurrentRoomCount} currentRoom={player.RunState.CurrentRoom?.GetType().Name ?? "null"}");

            if (AiTeammateSessionRegistry.AutopilotEnabled &&
                LocalContext.IsMe(player) &&
                room is CombatRoom combatRoom)
            {
                await AiTeammateDummyController.ProceedAfterDeterministicTerminalRewardsAsync(player, combatRoom);
            }
        }

        private static async Task OfferRoomEndRewardsForAiParticipantAsync(
            Player hostPlayer,
            AbstractRoom room,
            AiTeammateSessionParticipant participant)
        {
            Player? aiPlayer = hostPlayer.RunState.GetPlayer(participant.PlayerId);
            if (aiPlayer == null)
            {
                return;
            }

            Log.Info($"[AITeammate] Offering room-end rewards to AI player={aiPlayer.NetId} room={room.GetType().Name} roomCount={hostPlayer.RunState.CurrentRoomCount} currentRoom={hostPlayer.RunState.CurrentRoom?.GetType().Name ?? "null"}");
            RewardsSet rewardsSet = CreateRoomEndRewardsSet(aiPlayer, room);
            await AiTeammateDummyController.ExecuteDeterministicRewardSetAsync(rewardsSet);
            Log.Info($"[AITeammate] Finished room-end rewards for AI player={aiPlayer.NetId} room={room.GetType().Name} roomCount={hostPlayer.RunState.CurrentRoomCount} currentRoom={hostPlayer.RunState.CurrentRoom?.GetType().Name ?? "null"}");
        }

        private static async Task AwaitHostRoomEndRewardsAsync(Task originalTask, Player player, AbstractRoom room)
        {
            Log.Info($"[AITeammate] Waiting for host room-end rewards room={room.GetType().Name} roomCount={player.RunState.CurrentRoomCount} currentRoom={player.RunState.CurrentRoom?.GetType().Name ?? "null"}");
            await originalTask;
            Log.Info($"[AITeammate] Host room-end rewards complete room={room.GetType().Name} roomCount={player.RunState.CurrentRoomCount} currentRoom={player.RunState.CurrentRoom?.GetType().Name ?? "null"}");
        }

        private static RewardsSet CreateRoomEndRewardsSet(Player player, AbstractRoom room)
        {
            if (room is CombatRoom combatRoom && combatRoom.Encounter != null && !combatRoom.Encounter.ShouldGiveRewards)
            {
                return new RewardsSet(player).EmptyForRoom(room);
            }

            return new RewardsSet(player).WithRewardsFromRoom(room);
        }
    }

    [HarmonyPatch(typeof(CardReward), "OnSelect")]
    private static class CardRewardOnSelectPatch
    {
        private static bool Prefix(CardReward __instance, ref Task<bool> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(__instance.Player))
            {
                return true;
            }

            __result = AiTeammateDummyController.ExecuteDeterministicCardRewardAsync(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.SetLocalPlayerReady))]
    private static class ActChangeSynchronizerSetLocalPlayerReadyPatch
    {
        private static void Postfix(ActChangeSynchronizer __instance)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null ||
                session.HostPlayerId != MegaCrit.Sts2.Core.Context.LocalContext.NetId)
            {
                return;
            }

            RunManager? runManager = RunManager.Instance;
            if (runManager == null)
            {
                return;
            }

            Player? hostPlayer = runManager.DebugOnlyGetState()?.GetPlayer(session.HostPlayerId);
            var runState = hostPlayer?.RunState;
            if (runState == null)
            {
                return;
            }

            var actionQueueSynchronizer = runManager.ActionQueueSynchronizer;
            if (actionQueueSynchronizer == null)
            {
                return;
            }

            foreach (AiTeammateSessionParticipant participant in session.Participants.Where(static participant => !participant.IsHost))
            {
                Player? aiPlayer = runState.GetPlayer(participant.PlayerId);
                if (aiPlayer == null)
                {
                    continue;
                }

                Log.Info($"[AITeammate] Auto-readying AI teammate for act transition player={aiPlayer.NetId} act={runState.CurrentActIndex + 1}");
                actionQueueSynchronizer.RequestEnqueue(new VoteToMoveToNextActAction(aiPlayer));
            }
        }
    }
}
