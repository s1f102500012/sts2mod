using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace AITeammate.Scripts;

internal static class PhantasmalGardenersStrategy
{
    private const string EncounterToken = "PHANTASMAL_GARDENERS";
    private const string EnemyToken = "PHANTASMAL_GARDENER";
    private const string EnemyNameToken = "PHANTASMAL";

    private static readonly BindingFlags InstanceAnyVisibility =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool IsEncounterId(string? encounterId)
    {
        return ContainsToken(encounterId, EncounterToken);
    }

    public static bool IsGardenersCombat(
        string? encounterId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        return IsEncounterId(encounterId) || CountLikelyGardeners(enemiesById.Values) >= 3;
    }

    public static double EstimateFocusPriority(
        DeterministicEnemyState enemy,
        int teamDamage,
        int effectiveHp)
    {
        int rawHp = Math.Max(1, enemy.CurrentHp);
        double priority = 4_000d;
        priority -= rawHp * 26d;
        priority -= Math.Min(120, enemy.Block * 2);
        priority += Math.Min(620, teamDamage * 10);
        priority += Math.Min(380, enemy.IncomingDamage * 9 + enemy.SustainedAttackPressure * 2);

        if (teamDamage >= effectiveHp)
        {
            priority += 12_000d;
        }
        else if (rawHp <= 28)
        {
            priority += 650d - rawHp * 8d;
        }

        if (enemy.PunishesAttacks)
        {
            priority -= 220d;
        }

        return priority;
    }

    public static bool IsPredictedNextEliteGardeners(RunState runState)
    {
        return TryGetNextEliteEncounterId(runState, out string encounterId) &&
               IsEncounterId(encounterId);
    }

    public static bool TryGetNextEliteEncounterId(RunState runState, out string encounterId)
    {
        encounterId = string.Empty;
        try
        {
            PropertyInfo? property = typeof(RunState).GetProperty("NextEliteEncounter", InstanceAnyVisibility);
            object? encounter = property?.GetValue(runState);
            return TryReadModelIdEntry(encounter, out encounterId);
        }
        catch
        {
            encounterId = string.Empty;
            return false;
        }
    }

    public static double EstimateEliteRoutePenalty(Player player, int depthFromRoot)
    {
        double hpRatio = player.Creature.MaxHp > 0
            ? Math.Clamp(player.Creature.CurrentHp / (double)player.Creature.MaxHp, 0d, 1d)
            : 1d;
        double penalty = 250d;

        if (player.RunState is RunState runState && runState.CurrentActIndex == 0)
        {
            penalty += 55d;
        }

        if (hpRatio < 0.78d)
        {
            penalty += (0.78d - hpRatio) * 260d;
        }

        penalty += Math.Max(0, 2 - depthFromRoot) * 25d;
        return Math.Clamp(penalty, 210d, 420d);
    }

    private static int CountLikelyGardeners(IEnumerable<DeterministicEnemyState> enemies)
    {
        return enemies.Count(static enemy => IsLikelyGardener(enemy.Creature));
    }

    private static bool IsLikelyGardener(Creature creature)
    {
        return ContainsToken(creature.Name, EnemyNameToken) ||
               ContainsToken(creature.Name, "幻影") ||
               ContainsToken(creature.ToString(), EnemyToken) ||
               ContainsToken(creature.GetType().Name, EnemyToken);
    }

    private static bool TryReadModelIdEntry(object? model, out string entry)
    {
        entry = string.Empty;
        if (model == null)
        {
            return false;
        }

        object? id = model.GetType().GetProperty("Id", InstanceAnyVisibility)?.GetValue(model);
        object? value = id?.GetType().GetProperty("Entry", InstanceAnyVisibility)?.GetValue(id);
        entry = value?.ToString() ?? model.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(entry);
    }

    private static bool ContainsToken(string? value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
