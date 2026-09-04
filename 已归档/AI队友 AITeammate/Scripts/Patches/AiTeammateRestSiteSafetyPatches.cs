using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateRestSiteSafetyPatches
{
    [HarmonyPatch(typeof(NRestSiteRoom), "RestSiteButtonHovered")]
    private static class NRestSiteRoomRestSiteButtonHoveredPatch
    {
        private static bool Prefix(NRestSiteRoom __instance, NRestSiteButton button)
        {
            if (!IsAutomationActive())
            {
                return true;
            }

            RestSiteOption? option = button.Option;
            if (option == null || __instance.Options.Contains(option))
            {
                return true;
            }

            Log.Debug($"[AITeammate][Rest] Suppressed stale rest option hover option={option.OptionId} currentOptions={__instance.Options.Count}.");
            return false;
        }
    }

    [HarmonyPatch(typeof(NRestSiteRoom), "OnPlayerChangedHoveredRestSiteOption")]
    private static class NRestSiteRoomOnPlayerChangedHoveredRestSiteOptionPatch
    {
        private static bool Prefix(NRestSiteRoom __instance, ulong playerId)
        {
            if (!IsAutomationActive())
            {
                return true;
            }

            RestSiteSynchronizer? synchronizer = RunManager.Instance?.RestSiteSynchronizer;
            int? hoveredOptionIndex;
            try
            {
                hoveredOptionIndex = synchronizer?.GetHoveredOptionIndex(playerId);
            }
            catch (Exception exception)
            {
                Log.Debug($"[AITeammate][Rest] Suppressed unreadable rest hover state player={playerId} reason={exception.Message}");
                return false;
            }

            if (!hoveredOptionIndex.HasValue)
            {
                return true;
            }

            int optionIndex = hoveredOptionIndex.Value;
            if (optionIndex >= 0 && optionIndex < __instance.Options.Count)
            {
                return true;
            }

            Log.Debug($"[AITeammate][Rest] Suppressed stale rest hover index player={playerId} index={optionIndex} currentOptions={__instance.Options.Count}.");
            return false;
        }
    }

    private static bool IsAutomationActive()
    {
        return AiTeammateSessionRegistry.ActiveRunSession != null ||
               AiTeammateSessionRegistry.AutopilotEnabled ||
               AiTeammateSessionRegistry.TryGetAutopilotHostController(out _);
    }
}
