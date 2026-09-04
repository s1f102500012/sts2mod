using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Abandon))]
internal static class AiTeammateRunManagerAbandonPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        AiTeammateSaveSupport.PrepareForInProgressAbandon();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class AiTeammateRunManagerCleanUpPatch
{
    [HarmonyPostfix]
    private static void Postfix(RunManager __instance)
    {
        if (__instance.NetService is not AiTeammateLoopbackHostGameService &&
            AiTeammateSessionRegistry.Current == null)
        {
            return;
        }

        AiTeammateSaveSupport.ClearInMemorySessionIfNeeded();
    }
}

[HarmonyPatch(typeof(RunLobby), nameof(RunLobby.AbandonRun))]
internal static class AiTeammateRunLobbyAbandonRunPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RunLobby __instance)
    {
        INetGameService? netService = AccessTools.Field(typeof(RunLobby), "_netService")?.GetValue(__instance) as INetGameService;
        if (netService is not AiTeammateLoopbackHostGameService loopbackService)
        {
            return true;
        }

        if (loopbackService.Type != NetGameType.Host)
        {
            throw new InvalidOperationException("AI teammate abandon run can only be called as host!");
        }

        Log.Info("[AITeammate] Replacing RunLobby.AbandonRun for local loopback host.");
        AiTeammateSessionRegistry.MarkRunAbandoning("run_lobby_abandon");
        loopbackService.SendMessage(default(RunAbandonedMessage));

        IRunLobbyListener? lobbyListener = AccessTools.Field(typeof(RunLobby), "_lobbyListener")?.GetValue(__instance) as IRunLobbyListener;
        lobbyListener?.RunAbandoned();

        netService.Disconnect(NetError.HostAbandoned);
        return false;
    }
}
