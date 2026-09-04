using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal static class AiTeammateRelicSelectionPatches
{
    [HarmonyPatch(typeof(RelicSelectCmd), nameof(RelicSelectCmd.FromChooseARelicScreen))]
    private static class RelicSelectPatch
    {
        private static bool Prefix(Player player, IReadOnlyList<RelicModel> relics, ref Task<RelicModel?> __result)
        {
            if (!AiTeammateDummyController.CanUseDirectSelectionAutomation(player))
            {
                return true;
            }

            __result = Task.FromResult(AiTeammateDummyController.ChooseFirstRelic(relics));
            return false;
        }
    }
}
