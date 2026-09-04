using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class AiTeammateTestCombatHelper
{
    private static readonly HashSet<string> PatchedCombatKeys = new(StringComparer.Ordinal);

    public static void ApplyOneHpEnemiesIfNeeded(Player player, RunState runState)
    {
        if (!AiTeammateSessionRegistry.ShouldUseTestMap(runState) ||
            player.Creature?.CombatState == null ||
            runState.CurrentRoom is not MegaCrit.Sts2.Core.Rooms.CombatRoom)
        {
            return;
        }

        string combatKey = BuildCombatKey(runState);
        bool firstPatchForCombat = PatchedCombatKeys.Add(combatKey);

        foreach (Creature enemy in player.Creature.CombatState.HittableEnemies)
        {
            try
            {
                if (enemy.Block > 0)
                {
                    enemy.LoseBlockInternal(enemy.Block);
                }

                if (enemy.CurrentHp > 1)
                {
                    enemy.SetCurrentHpInternal(1);
                }
            }
            catch (Exception exception)
            {
                Log.Warn($"[AITeammate] Failed to apply one-HP test combat shortcut enemy={enemy} key={combatKey}: {exception.Message}");
            }
        }

        if (firstPatchForCombat)
        {
            Log.Info($"[AITeammate] Applied one-HP enemy shortcut for test-map combat key={combatKey}.");
        }
    }

    private static string BuildCombatKey(RunState runState)
    {
        string coord = runState.CurrentMapCoord.HasValue
            ? $"{runState.CurrentMapCoord.Value.col},{runState.CurrentMapCoord.Value.row}"
            : "none";
        return $"act={runState.CurrentActIndex};roomCount={runState.CurrentRoomCount};coord={coord}";
    }
}
