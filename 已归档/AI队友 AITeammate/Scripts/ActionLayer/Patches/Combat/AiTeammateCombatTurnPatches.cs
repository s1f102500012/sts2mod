using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateCombatTurnPatches
{
    private static bool _syncingAiEnemyTurnReady;

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
    private static class CombatManagerReadyToEndTurnPatch
    {
        private static void Postfix(Player player, bool canBackOut)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null)
            {
                return;
            }

            Log.Info($"[AITeammate] ReadyToEndTurn player={player.NetId} host={session.HostPlayerId} canBackOut={canBackOut} allReady={CombatManager.Instance.AllPlayersReadyToEndTurn()}");
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToBeginEnemyTurn))]
    private static class CombatManagerReadyToBeginEnemyTurnPatch
    {
        private static void Postfix(Player player)
        {
            AiTeammateSessionState? session = AiTeammateSessionRegistry.ActiveRunSession;
            if (session == null)
            {
                return;
            }

            Log.Info($"[AITeammate] ReadyToBeginEnemyTurn player={player.NetId} host={session.HostPlayerId}");
            if (_syncingAiEnemyTurnReady ||
                player.NetId != session.HostPlayerId)
            {
                return;
            }

            try
            {
                _syncingAiEnemyTurnReady = true;
                foreach (AiTeammateSessionParticipant participant in session.Participants.Where(static participant => !participant.IsHost))
                {
                    Player? aiPlayer = RunManager.Instance.DebugOnlyGetState()?.GetPlayer(participant.PlayerId);
                    if (aiPlayer == null)
                    {
                        continue;
                    }

                    Log.Info($"[AITeammate] Auto-marking AI ready to begin enemy turn. aiPlayer={participant.PlayerId} wasReadyToEndTurn={CombatManager.Instance.IsPlayerReadyToEndTurn(aiPlayer)}");
                    CombatManager.Instance.SetReadyToBeginEnemyTurn(aiPlayer);
                }
            }
            finally
            {
                _syncingAiEnemyTurnReady = false;
            }
        }
    }
}
