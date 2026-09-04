using System;
using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal static class ObscuraStrategy
{
    private const string EncounterToken = "THE_OBSCURA";
    private const string EnemyToken = "THE_OBSCURA";

    public static bool IsEncounterId(string? encounterId)
    {
        return ContainsToken(encounterId, EncounterToken);
    }

    public static bool IsObscuraCombat(
        string? encounterId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        return IsEncounterId(encounterId) ||
               enemiesById.Values.Any(static enemy => enemy.IsObscuraBody);
    }

    public static double EstimateFocusPriority(
        DeterministicEnemyState enemy,
        int teamDamage,
        int effectiveHp)
    {
        if (!enemy.IsObscuraBody)
        {
            return -25_000d + Math.Min(300d, teamDamage * 2d);
        }

        double priority = 25_000d;
        priority += Math.Min(1_600d, teamDamage * 18d);
        priority += Math.Min(650d, enemy.IncomingDamage * 10d + enemy.SustainedAttackPressure * 3d);
        priority -= Math.Min(260d, enemy.Block * 2d);
        priority -= Math.Max(0, enemy.CurrentHp - 30) * 3d;

        if (teamDamage >= effectiveHp)
        {
            priority += 16_000d;
        }

        return priority;
    }

    private static bool ContainsToken(string? value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
