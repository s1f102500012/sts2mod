using System;
using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal interface IEncounterTargetingStrategy
{
    bool RequiresTargetLock { get; }

    double? EstimateBasePriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp);

    double EstimatePriorityAdjustment(DeterministicEnemyState enemy);
}

internal static class EncounterTargetingStrategyRegistry
{
    public static IReadOnlyList<IEncounterTargetingStrategy> GetActive(
        string encounterId,
        IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById,
        bool isPhantasmalGardenersCombat,
        bool isObscuraCombat)
    {
        List<IEncounterTargetingStrategy> strategies = [];
        if (isObscuraCombat)
        {
            strategies.Add(ObscuraTargetingStrategy.Instance);
        }
        else if (isPhantasmalGardenersCombat)
        {
            strategies.Add(PhantasmalGardenersTargetingStrategy.Instance);
        }

        if (CorpseSlugDebuffTargetingStrategy.IsActive(enemiesById))
        {
            strategies.Add(CorpseSlugDebuffTargetingStrategy.Instance);
        }

        if (GremlinMercAftermathTargetingStrategy.IsActive(encounterId, enemiesById))
        {
            strategies.Add(GremlinMercAftermathTargetingStrategy.Instance);
        }

        return strategies;
    }
}

internal sealed class ObscuraTargetingStrategy : IEncounterTargetingStrategy
{
    public static readonly ObscuraTargetingStrategy Instance = new();

    public bool RequiresTargetLock => true;

    public double? EstimateBasePriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp)
    {
        return ObscuraStrategy.EstimateFocusPriority(enemy, teamDamage, effectiveHp);
    }

    public double EstimatePriorityAdjustment(DeterministicEnemyState enemy)
    {
        return 0d;
    }
}

internal sealed class PhantasmalGardenersTargetingStrategy : IEncounterTargetingStrategy
{
    public static readonly PhantasmalGardenersTargetingStrategy Instance = new();

    public bool RequiresTargetLock => true;

    public double? EstimateBasePriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp)
    {
        return PhantasmalGardenersStrategy.EstimateFocusPriority(enemy, teamDamage, effectiveHp);
    }

    public double EstimatePriorityAdjustment(DeterministicEnemyState enemy)
    {
        return 0d;
    }
}

internal sealed class CorpseSlugDebuffTargetingStrategy : IEncounterTargetingStrategy
{
    public static readonly CorpseSlugDebuffTargetingStrategy Instance = new();

    public bool RequiresTargetLock => true;

    public static bool IsActive(IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        int corpseSlugCount = enemiesById.Values.Count(static enemy => enemy.IsCorpseSlug);
        return corpseSlugCount >= 2 &&
               enemiesById.Values.Any(static enemy => enemy.IsCorpseSlugDebuffIntent) &&
               enemiesById.Values.Any(static enemy => enemy.IsCorpseSlug && enemy.IsAttacking);
    }

    public double? EstimateBasePriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp)
    {
        return null;
    }

    public double EstimatePriorityAdjustment(DeterministicEnemyState enemy)
    {
        if (!enemy.IsCorpseSlug)
        {
            return 0d;
        }

        if (enemy.IsCorpseSlugDebuffIntent)
        {
            return 32_000d + Math.Max(0, 160 - enemy.CurrentHp - enemy.Block);
        }

        if (enemy.IsAttacking)
        {
            return -24_000d;
        }

        return -8_000d;
    }
}

internal sealed class GremlinMercAftermathTargetingStrategy : IEncounterTargetingStrategy
{
    public static readonly GremlinMercAftermathTargetingStrategy Instance = new();

    public bool RequiresTargetLock => true;

    public static bool IsActive(string encounterId, IReadOnlyDictionary<string, DeterministicEnemyState> enemiesById)
    {
        return (encounterId.Contains("GREMLIN_MERC", StringComparison.OrdinalIgnoreCase) ||
                enemiesById.Values.Any(static enemy => enemy.IsFatGremlin || enemy.IsSneakyGremlin)) &&
               enemiesById.Values.Any(static enemy => enemy.IsFatGremlin) &&
               enemiesById.Values.Any(static enemy => enemy.IsSneakyGremlin) &&
               enemiesById.Values.All(static enemy => !enemy.IsGremlinMerc);
    }

    public double? EstimateBasePriority(DeterministicEnemyState enemy, int teamDamage, int effectiveHp)
    {
        return null;
    }

    public double EstimatePriorityAdjustment(DeterministicEnemyState enemy)
    {
        if (enemy.IsFatGremlin)
        {
            return 36_000d + Math.Max(0, 120 - enemy.CurrentHp - enemy.Block);
        }

        if (enemy.IsSneakyGremlin)
        {
            return -24_000d;
        }

        return -4_000d;
    }
}
