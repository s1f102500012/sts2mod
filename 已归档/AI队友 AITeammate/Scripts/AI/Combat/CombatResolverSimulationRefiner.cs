using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace AITeammate.Scripts;

internal static class BattleAiRollingSearchTuning
{
    public const int InitialTargetTurn = 8;
    public const int TargetTurnJump = 6;
    public const int BackstepTurns = TargetTurnJump / 2;
    public const int SameTurnCommandDepthLimit = 50;
    public const int MaxTurnLoads = 15_000;
}

internal sealed class CombatResolverSimulationRefiner
{
    private const int MaxSimulatedPlans = 4;
    private const int MaxSimulatedActionsPerPlan = 6;
    private const int MaxDeepSearchResults = 12;
    private const int AllEnemiesKilledBonus = 14_000;
    private const int AllNonMinionEnemiesKilledBonus = 11_500;
    private const int EnemyKilledBonus = 1_850;
    private const int NonMinionEnemyKilledBonus = 1_150;
    private const int DamageValuePerPoint = 92;
    private const int ActorHpLossPenaltyPerPoint = 260;
    private const int TeamHpLossPenaltyPerPoint = 145;
    private const int ExcessPredictedActorHpLossPenaltyPerPoint = 180;
    private const int ExcessPredictedTeamHpLossPenaltyPerPoint = 80;
    private const int ActorPredictedDeathPenalty = 18_000;
    private const int TeamPredictedDeathPenalty = 4_200;
    private const int ActorDamagePreventedValuePerPoint = 66;
    private const int TeamDamagePreventedValuePerPoint = 22;
    private const int BrawlActorHpLossPenaltyPerPoint = 115;
    private const int BrawlTeamHpLossPenaltyPerPoint = 64;
    private const int BrawlExcessActorHpLossPenaltyPerPoint = 80;
    private const int BrawlExcessTeamHpLossPenaltyPerPoint = 35;
    private const int BrawlActorDamagePreventedValuePerPoint = 30;
    private const int BrawlTeamDamagePreventedValuePerPoint = 10;
    private const int RolloutScoreDivisor = 6;
    private const int DeepSearchOverrideScoreMargin = 1_600;
    private const int DeepSearchOverrideDamageMargin = 16;
    private const int DeepSearchOverrideActorHpSavedMargin = 6;
    private const int DeepSearchOverrideTeamHpSavedMargin = 10;
    private const int ScalingFightNoProgressPenalty = 2_200;
    private const int UsefulProtectionValuePerPoint = 66;
    private const int RemainingEnergyPenaltyPerPoint = 18;
    private const int RemainingStarsPenaltyPerPoint = 8;
    private const int NormalCombatPotionMargin = 2_800;
    private const int EliteOrBossPotionMargin = 900;
    private const int CrisisPotionMargin = 550;

    public async Task<CombatLinePlan?> TryChooseBestPlanAsync(
        DeterministicCombatContext context,
        IReadOnlyList<CombatLinePlan> candidatePlans,
        CancellationToken ct)
    {
        if (candidatePlans.Count == 0)
        {
            return null;
        }

        Dictionary<string, AiLegalActionOption> actionsById = context.LegalActions
            .ToDictionary(static action => action.ActionId, StringComparer.Ordinal);
        List<CombatResolverSimulationResult> heuristicResults = [];
        foreach (CombatLinePlan plan in candidatePlans.Take(MaxSimulatedPlans))
        {
            ct.ThrowIfCancellationRequested();
            if (plan.ActionIds.Count == 0 ||
                plan.ActionIds.Count > MaxSimulatedActionsPerPlan ||
                plan.ActionIds.Any(static actionId => actionId.StartsWith("virtual_draw_", StringComparison.Ordinal)) ||
                plan.ActionIds.Any(actionId => !actionsById.ContainsKey(actionId)))
            {
                continue;
            }

            CombatResolverSimulationResult? result = await CombatActionResolverSimulator.TryEvaluateAsync(
                context,
                plan,
                actionsById,
                ct);
            if (result != null)
            {
                heuristicResults.Add(result);
            }
        }

        List<CombatResolverSimulationResult> deepSearchResults = (await CombatActionResolverSimulator.TryDeepSearchAsync(context, MaxDeepSearchResults, ct)).ToList();
        if (heuristicResults.Count > 0 && deepSearchResults.Count > 0)
        {
            CombatResolverSimulationResult heuristicAnchor = SelectGateAnchor(context, heuristicResults);
            int rawDeepSearchCount = deepSearchResults.Count;
            deepSearchResults = deepSearchResults
                .Where(result => ShouldAdmitDeepSearchOverride(context, heuristicAnchor, result))
                .ToList();
            if (deepSearchResults.Count != rawDeepSearchCount)
            {
                Log.Debug($"[AITeammate][Sim] resolver-deep-search gated actor={context.Actor.NetId} kept={deepSearchResults.Count}/{rawDeepSearchCount} heuristic=[{string.Join(", ", heuristicAnchor.Plan.ActionIds)}] heuristicScore={heuristicAnchor.Score}");
            }
        }

        List<CombatResolverSimulationResult> simulatedResults = heuristicResults
            .Concat(deepSearchResults)
            .GroupBy(static result => string.Join("|", result.Plan.ActionIds), StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static result => result.Score)
                .First())
            .ToList();
        if (simulatedResults.Count == 0)
        {
            return null;
        }

        HashSet<string> legalActionIds = actionsById.Keys.ToHashSet(StringComparer.Ordinal);
        simulatedResults = simulatedResults
            .Where(result => result.Plan.ActionIds.Count > 0 &&
                             legalActionIds.Contains(result.Plan.ActionIds[0]))
            .ToList();
        if (simulatedResults.Count == 0)
        {
            Log.Debug($"[AITeammate][Sim] resolver-sim declined all routes actor={context.Actor.NetId} reason=no-live-first-action");
            return null;
        }

        simulatedResults = ApplyOutcomeSafetyGate(context, simulatedResults);

        CombatResolverSimulationResult bestByScore = simulatedResults
            .OrderByDescending(static result => result.Score)
            .ThenBy(static result => string.Join("|", result.Plan.ActionIds), StringComparer.Ordinal)
            .First();
        CombatResolverSimulationResult? best = ApplyPotionConservationGate(context, bestByScore, simulatedResults);
        if (best == null)
        {
            Log.Debug($"[AITeammate][Sim] resolver-sim declined potion-only route actor={context.Actor.NetId} actions=[{string.Join(", ", bestByScore.Plan.ActionIds)}] score={bestByScore.Score} potions={bestByScore.PotionsUsed}");
            return null;
        }

        if (ShouldDeclineOverDefensiveOverride(context, best))
        {
            Log.Info($"[AITeammate][Sim] resolver-sim declined over-defensive route actor={context.Actor.NetId} actions=[{string.Join(", ", best.Plan.ActionIds)}] score={best.Score} damage={best.DamageDealt} kills={best.EnemiesKilled} actorProjected={best.PredictedActorHpLossAfterEnemyTurn} teamProjected={best.PredictedTeamHpLossAfterEnemyTurn} teamDeaths={best.PredictedTeamDeathsAfterEnemyTurn}");
            return null;
        }

        Log.Info($"[AITeammate][Sim] resolver-sim actor={context.Actor.NetId} actions=[{string.Join(", ", best.Plan.ActionIds)}] score={best.Score} damage={best.DamageDealt} kills={best.EnemiesKilled} actorHpLost={best.ActorHpLost} actorProjected={best.PredictedActorHpLossAfterEnemyTurn} teamHpLost={best.TeamHpLost} teamProjected={best.PredictedTeamHpLossAfterEnemyTurn} teamDeaths={best.PredictedTeamDeathsAfterEnemyTurn} prevented={best.ActorDamagePreventedAfterEnemyTurn}/{best.TeamDamagePreventedAfterEnemyTurn} block={best.ActorBlockAfter} energy={best.ActorEnergyAfter} stars={best.ActorStarsAfter} setup={best.ActorPowerScoreDelta}/{best.TeamPowerScoreDelta}/{best.EnemyControlScoreDelta} rollout={best.RolloutScore} rollingWindow={BattleAiRollingSearchTuning.InitialTargetTurn}/{BattleAiRollingSearchTuning.TargetTurnJump}/{BattleAiRollingSearchTuning.BackstepTurns} potions={best.PotionsUsed}");

        return new CombatLinePlan
        {
            ActionIds = best.Plan.ActionIds,
            Score = best.Score,
            EstimatedDamageDealt = best.DamageDealt,
            EstimatedDamageTaken = Math.Max(best.PredictedActorHpLossAfterEnemyTurn, best.PredictedTeamHpLossAfterEnemyTurn),
            EstimatedBlockAfterEnemyTurn = best.ActorBlockAfter
        };
    }

    private static CombatResolverSimulationResult SelectGateAnchor(
        DeterministicCombatContext context,
        List<CombatResolverSimulationResult> heuristicResults)
    {
        List<CombatResolverSimulationResult> gated = ApplyOutcomeSafetyGate(context, heuristicResults);
        CombatResolverSimulationResult bestByScore = gated
            .OrderByDescending(static result => result.Score)
            .ThenBy(static result => string.Join("|", result.Plan.ActionIds), StringComparer.Ordinal)
            .First();
        return ApplyPotionConservationGate(context, bestByScore, gated) ?? bestByScore;
    }

    private static bool ShouldAdmitDeepSearchOverride(
        DeterministicCombatContext context,
        CombatResolverSimulationResult heuristic,
        CombatResolverSimulationResult search)
    {
        if (search.PredictedTeamDeathsAfterEnemyTurn > heuristic.PredictedTeamDeathsAfterEnemyTurn)
        {
            return false;
        }

        if (PredictsActorDeath(context, search) && !PredictsActorDeath(context, heuristic))
        {
            return false;
        }

        if (search.PotionsUsed > heuristic.PotionsUsed &&
            !IsPotionPlanIndependentlyWarranted(context, search))
        {
            return false;
        }

        if (search.KilledAllEnemies && !heuristic.KilledAllEnemies)
        {
            return true;
        }

        if (search.KilledAllNonMinionEnemies && !heuristic.KilledAllNonMinionEnemies)
        {
            return true;
        }

        int actorHpSaved = heuristic.PredictedActorHpLossAfterEnemyTurn - search.PredictedActorHpLossAfterEnemyTurn;
        int teamHpSaved = heuristic.PredictedTeamHpLossAfterEnemyTurn - search.PredictedTeamHpLossAfterEnemyTurn;
        if (actorHpSaved >= DeepSearchOverrideActorHpSavedMargin ||
            teamHpSaved >= DeepSearchOverrideTeamHpSavedMargin)
        {
            return true;
        }

        if (search.NonMinionEnemiesKilled > heuristic.NonMinionEnemiesKilled &&
            search.PredictedActorHpLossAfterEnemyTurn <= heuristic.PredictedActorHpLossAfterEnemyTurn + 2 &&
            search.PredictedTeamHpLossAfterEnemyTurn <= heuristic.PredictedTeamHpLossAfterEnemyTurn + 4)
        {
            return true;
        }

        int damageMargin = context.HasSustainedAttackPressure
            ? Math.Max(8, DeepSearchOverrideDamageMargin / 2)
            : DeepSearchOverrideDamageMargin;
        if (search.DamageDealt >= heuristic.DamageDealt + damageMargin &&
            search.PredictedActorHpLossAfterEnemyTurn <= heuristic.PredictedActorHpLossAfterEnemyTurn + 2 &&
            search.PredictedTeamHpLossAfterEnemyTurn <= heuristic.PredictedTeamHpLossAfterEnemyTurn + 4)
        {
            return true;
        }

        bool sameOpeningAction = search.Plan.ActionIds.Count > 0 &&
                                 heuristic.Plan.ActionIds.Count > 0 &&
                                 string.Equals(search.Plan.ActionIds[0], heuristic.Plan.ActionIds[0], StringComparison.Ordinal);
        if (sameOpeningAction && search.Score >= heuristic.Score + DeepSearchOverrideScoreMargin / 3)
        {
            return true;
        }

        bool hasConcreteImprovement =
            search.EnemiesKilled > heuristic.EnemiesKilled ||
            search.NonMinionEnemiesKilled > heuristic.NonMinionEnemiesKilled ||
            search.DamageDealt > heuristic.DamageDealt ||
            actorHpSaved > 0 ||
            teamHpSaved > 0;
        return hasConcreteImprovement &&
               search.Score >= heuristic.Score + DeepSearchOverrideScoreMargin;
    }

    private static List<CombatResolverSimulationResult> ApplyOutcomeSafetyGate(
        DeterministicCombatContext context,
        List<CombatResolverSimulationResult> results)
    {
        if (results.Count <= 1)
        {
            return results;
        }

        List<CombatResolverSimulationResult> gated = results;
        List<CombatResolverSimulationResult> actorSurvives = gated
            .Where(result => !PredictsActorDeath(context, result))
            .ToList();
        if (actorSurvives.Count > 0)
        {
            gated = actorSurvives;
        }

        int minTeamDeaths = gated.Min(static result => result.PredictedTeamDeathsAfterEnemyTurn);
        if ((context.IsTeamInCrisis || minTeamDeaths == 0) &&
            gated.Any(result => result.PredictedTeamDeathsAfterEnemyTurn > minTeamDeaths))
        {
            gated = gated
                .Where(result => result.PredictedTeamDeathsAfterEnemyTurn == minTeamDeaths)
                .ToList();
        }

        if (context.HasSustainedAttackPressure &&
            !context.IsWaterfallSelfDestructDefenseWindow &&
            !context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            List<CombatResolverSimulationResult> progress = gated
                .Where(static result => result.DamageDealt > 0 ||
                                        result.EnemiesKilled > 0 ||
                                        result.KilledAllNonMinionEnemies)
                .ToList();
            if (progress.Count > 0 &&
                progress.Min(static result => result.PredictedTeamDeathsAfterEnemyTurn) <= minTeamDeaths)
            {
                gated = progress;
            }
        }

        return gated;
    }

    private static bool PredictsActorDeath(
        DeterministicCombatContext context,
        CombatResolverSimulationResult result)
    {
        return result.PredictedActorHpLossAfterEnemyTurn >= Math.Max(1, context.CurrentHp);
    }

    private static bool ShouldDeclineOverDefensiveOverride(
        DeterministicCombatContext context,
        CombatResolverSimulationResult best)
    {
        if (best.DamageDealt > 0 ||
            best.EnemiesKilled > 0 ||
            best.KilledAllEnemies ||
            best.KilledAllNonMinionEnemies ||
            !HasPlayableEnemyTargetingAction(context))
        {
            return false;
        }

        if (PredictsActorDeath(context, best) &&
            best.ActorDamagePreventedAfterEnemyTurn > 0)
        {
            return false;
        }

        if (context.IsWaterfallSelfDestructDefenseWindow ||
            context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            return false;
        }

        return best.PredictedTeamDeathsAfterEnemyTurn > 0 ||
               best.ActorDamagePreventedAfterEnemyTurn <= 0 ||
               context.HasSustainedAttackPressure ||
               context.TeamTactics.HasPrimaryTarget;
    }

    private static bool HasPlayableEnemyTargetingAction(DeterministicCombatContext context)
    {
        return context.LegalActions.Any(static action =>
            string.Equals(action.ActionType, AiTeammateActionKind.PlayCard.ToString(), StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(action.TargetId) &&
            action.TargetId.StartsWith("creature_", StringComparison.Ordinal));
    }

    private static CombatResolverSimulationResult? ApplyPotionConservationGate(
        DeterministicCombatContext context,
        CombatResolverSimulationResult bestByScore,
        IReadOnlyList<CombatResolverSimulationResult> simulatedResults)
    {
        if (bestByScore.PotionsUsed <= 0)
        {
            return bestByScore;
        }

        CombatResolverSimulationResult? bestNoPotion = simulatedResults
            .Where(static result => result.PotionsUsed <= 0)
            .OrderByDescending(static result => result.Score)
            .ThenBy(static result => string.Join("|", result.Plan.ActionIds), StringComparer.Ordinal)
            .FirstOrDefault();
        if (bestNoPotion == null)
        {
            return IsPotionPlanIndependentlyWarranted(context, bestByScore)
                ? bestByScore
                : null;
        }

        if (IsPotionPlanClearlyWarranted(context, bestByScore, bestNoPotion))
        {
            return bestByScore;
        }

        int requiredMargin = context.IsTeamInCrisis
            ? CrisisPotionMargin
            : context.IsEliteOrBossCombat
                ? EliteOrBossPotionMargin
                : NormalCombatPotionMargin;
        return bestByScore.Score >= bestNoPotion.Score + requiredMargin
            ? bestByScore
            : bestNoPotion;
    }

    private static bool IsPotionPlanIndependentlyWarranted(
        DeterministicCombatContext context,
        CombatResolverSimulationResult potionPlan)
    {
        if (potionPlan.PotionsUsed <= 0)
        {
            return true;
        }

        if (potionPlan.KilledAllEnemies || potionPlan.KilledAllNonMinionEnemies)
        {
            return true;
        }

        if (context.ShouldSpendPotionSlotForFutureDrop &&
            (potionPlan.DamageDealt > 0 ||
             potionPlan.EnemiesKilled > 0 ||
             potionPlan.PredictedActorHpLossAfterEnemyTurn <= Math.Max(0, context.IncomingDamageAfterBlock - 4)))
        {
            return true;
        }

        int actorDamagePrevented = Math.Max(0, context.IncomingDamageAfterBlock - potionPlan.PredictedActorHpLossAfterEnemyTurn);
        int teamDamagePrevented = Math.Max(0, context.TeamIncomingDamageAfterBlock - potionPlan.PredictedTeamHpLossAfterEnemyTurn);
        if (context.IsTeamInCrisis)
        {
            return potionPlan.PredictedActorHpLossAfterEnemyTurn < Math.Max(1, context.CurrentHp) &&
                   (actorDamagePrevented >= 4 || teamDamagePrevented >= 8);
        }

        int preventionThreshold = context.IsEliteOrBossCombat ? 8 : 14;
        return actorDamagePrevented + teamDamagePrevented / 2 >= preventionThreshold;
    }

    private static bool IsPotionPlanClearlyWarranted(
        DeterministicCombatContext context,
        CombatResolverSimulationResult potionPlan,
        CombatResolverSimulationResult noPotionPlan)
    {
        if (IsPotionPlanIndependentlyWarranted(context, potionPlan) &&
            (potionPlan.KilledAllEnemies ||
             potionPlan.KilledAllNonMinionEnemies ||
             context.ShouldSpendPotionSlotForFutureDrop))
        {
            return true;
        }

        if (potionPlan.PredictedActorHpLossAfterEnemyTurn <= 0 &&
            noPotionPlan.PredictedActorHpLossAfterEnemyTurn >= Math.Max(1, context.CurrentHp))
        {
            return true;
        }

        if (potionPlan.PredictedTeamHpLossAfterEnemyTurn < noPotionPlan.PredictedTeamHpLossAfterEnemyTurn &&
            noPotionPlan.PredictedTeamHpLossAfterEnemyTurn >= Math.Max(1, context.TeamCurrentHp / 2))
        {
            return true;
        }

        if ((potionPlan.KilledAllEnemies && !noPotionPlan.KilledAllEnemies) ||
            (potionPlan.KilledAllNonMinionEnemies && !noPotionPlan.KilledAllNonMinionEnemies))
        {
            return true;
        }

        int hpSaved = Math.Max(0, noPotionPlan.PredictedActorHpLossAfterEnemyTurn - potionPlan.PredictedActorHpLossAfterEnemyTurn) +
                      Math.Max(0, noPotionPlan.PredictedTeamHpLossAfterEnemyTurn - potionPlan.PredictedTeamHpLossAfterEnemyTurn) / 2;
        int hpSaveThreshold = context.IsTeamInCrisis || context.IsEliteOrBossCombat ? 6 : 12;
        if (hpSaved >= hpSaveThreshold)
        {
            return true;
        }

        return context.ShouldSpendPotionSlotForFutureDrop &&
               (potionPlan.KilledAllEnemies ||
                potionPlan.EnemiesKilled > noPotionPlan.EnemiesKilled ||
                hpSaved >= 4);
    }

    internal static int ScoreSimulationResult(
        DeterministicCombatContext context,
        CombatLinePlan plan,
        CombatResolverSimulationResult result)
    {
        bool brawlScoring = ShouldUseBrawlScoring(context);
        int damageValue = DamageValuePerPoint + (brawlScoring
            ? Math.Clamp(context.SustainedAttackPressure / 20, 20, 70)
            : 0);
        int actorHpLossPenalty = brawlScoring ? BrawlActorHpLossPenaltyPerPoint : ActorHpLossPenaltyPerPoint;
        int teamHpLossPenalty = brawlScoring ? BrawlTeamHpLossPenaltyPerPoint : TeamHpLossPenaltyPerPoint;
        int excessActorHpLossPenalty = brawlScoring ? BrawlExcessActorHpLossPenaltyPerPoint : ExcessPredictedActorHpLossPenaltyPerPoint;
        int excessTeamHpLossPenalty = brawlScoring ? BrawlExcessTeamHpLossPenaltyPerPoint : ExcessPredictedTeamHpLossPenaltyPerPoint;
        int actorDamagePreventedValue = brawlScoring ? BrawlActorDamagePreventedValuePerPoint : ActorDamagePreventedValuePerPoint;
        int teamDamagePreventedValue = brawlScoring ? BrawlTeamDamagePreventedValuePerPoint : TeamDamagePreventedValuePerPoint;

        int score = plan.Score / 4;
        score += result.DamageDealt * damageValue;
        score += result.EnemiesKilled * EnemyKilledBonus;
        score += result.NonMinionEnemiesKilled * NonMinionEnemyKilledBonus;
        if (result.KilledAllEnemies)
        {
            score += AllEnemiesKilledBonus;
        }
        else if (result.KilledAllNonMinionEnemies)
        {
            score += AllNonMinionEnemiesKilledBonus;
        }

        int usefulProtection = Math.Min(
            Math.Max(0, context.IncomingDamage + context.UnblockableIncomingDamage),
            Math.Max(0, result.ActorBlockAfter - context.CurrentBlock) + Math.Max(0, context.CurrentBlock));
        score += usefulProtection * UsefulProtectionValuePerPoint;
        score += result.ActorPowerScoreDelta;
        score += result.TeamPowerScoreDelta / 2;
        score += result.EnemyControlScoreDelta;
        score += result.RolloutScore / RolloutScoreDivisor;
        score -= result.ActorHpLost * actorHpLossPenalty;
        score -= result.TeamHpLost * teamHpLossPenalty;
        score -= Math.Max(0, result.PredictedActorHpLossAfterEnemyTurn - context.IncomingDamageAfterBlock) * excessActorHpLossPenalty;
        score -= Math.Max(0, result.PredictedTeamHpLossAfterEnemyTurn - context.TeamIncomingDamageAfterBlock) * excessTeamHpLossPenalty;
        score += result.ActorDamagePreventedAfterEnemyTurn * actorDamagePreventedValue;
        score += result.TeamDamagePreventedAfterEnemyTurn * teamDamagePreventedValue;
        if (PredictsActorDeath(context, result))
        {
            score -= ActorPredictedDeathPenalty;
        }

        if (result.PredictedTeamDeathsAfterEnemyTurn > 0)
        {
            score -= result.PredictedTeamDeathsAfterEnemyTurn * TeamPredictedDeathPenalty;
        }

        if (context.HasSustainedAttackPressure &&
            !context.IsWaterfallSelfDestructDefenseWindow &&
            !context.IsLagavulinMatriarchOpeningSetupWindow &&
            !result.KilledAllEnemies &&
            !result.KilledAllNonMinionEnemies &&
            result.DamageDealt <= 0)
        {
            score -= ScalingFightNoProgressPenalty + Math.Min(2_400, context.SustainedAttackPressure * 8);
        }

        score -= Math.Max(0, result.ActorEnergyAfter) * RemainingEnergyPenaltyPerPoint;
        score -= Math.Max(0, result.ActorStarsAfter) * RemainingStarsPenaltyPerPoint;
        if (context.IsTeamInCrisis && result.PredictedTeamHpLossAfterEnemyTurn > 0)
        {
            score -= Math.Min(1800, result.PredictedTeamHpLossAfterEnemyTurn * 120);
        }

        score -= EstimatePotionConservationPenalty(context, result);
        return score;
    }

    internal static bool ShouldUseBrawlScoring(DeterministicCombatContext context)
    {
        return context.HasSustainedAttackPressure &&
               !context.IsTeamInCrisis &&
               !context.IsWaterfallSelfDestructDefenseWindow &&
               !context.IsLagavulinMatriarchOpeningSetupWindow;
    }

    private static int EstimatePotionConservationPenalty(
        DeterministicCombatContext context,
        CombatResolverSimulationResult result)
    {
        if (result.PotionsUsed <= 0)
        {
            return 0;
        }

        int basePenalty = context.IsEliteOrBossCombat ? 1_450 : 3_800;
        if (context.IsBossCombat)
        {
            basePenalty = 900;
        }

        if (context.IsTeamInCrisis)
        {
            basePenalty = Math.Max(350, basePenalty - 750);
        }

        if (context.ShouldSpendPotionSlotForFutureDrop)
        {
            basePenalty = Math.Max(250, basePenalty - 1_000);
        }

        int penalty = result.PotionsUsed * basePenalty;
        if (result.PotionsUsed > 1)
        {
            penalty += (result.PotionsUsed - 1) * 1_800;
        }

        penalty += result.HighValuePotionsUsed * (context.IsEliteOrBossCombat ? 450 : 1_100);
        if (result.KilledAllEnemies || result.KilledAllNonMinionEnemies)
        {
            penalty -= context.IsEliteOrBossCombat ? 900 : 550;
        }

        if (result.PredictedActorHpLossAfterEnemyTurn <= 0 && context.IncomingDamageAfterBlock > 0)
        {
            penalty -= Math.Min(850, context.IncomingDamageAfterBlock * 45);
        }

        return Math.Max(0, penalty);
    }
}

internal sealed class CombatResolverSimulationResult
{
    public required CombatLinePlan Plan { get; init; }

    public required int Score { get; init; }

    public required int DamageDealt { get; init; }

    public required int EnemiesKilled { get; init; }

    public required int NonMinionEnemiesKilled { get; init; }

    public required bool KilledAllEnemies { get; init; }

    public required bool KilledAllNonMinionEnemies { get; init; }

    public required int ActorHpLost { get; init; }

    public required int TeamHpLost { get; init; }

    public required int PredictedActorHpLossAfterEnemyTurn { get; init; }

    public required int PredictedTeamHpLossAfterEnemyTurn { get; init; }

    public required int PredictedTeamDeathsAfterEnemyTurn { get; init; }

    public required int ActorDamagePreventedAfterEnemyTurn { get; init; }

    public required int TeamDamagePreventedAfterEnemyTurn { get; init; }

    public required int ActorBlockAfter { get; init; }

    public required int ActorEnergyAfter { get; init; }

    public required int ActorStarsAfter { get; init; }

    public required int ActorPowerScoreDelta { get; init; }

    public required int TeamPowerScoreDelta { get; init; }

    public required int EnemyControlScoreDelta { get; init; }

    public required int RolloutScore { get; init; }

    public required int PotionsUsed { get; init; }

    public required int HighValuePotionsUsed { get; init; }
}

internal static class CombatActionResolverSimulator
{
    private static readonly SemaphoreSlim SimulationSemaphore = new(1, 1);
    private const int SimulationActionTimeoutMs = 120;
    private static readonly MethodInfo? PotionOnUseMethod = typeof(PotionModel).GetMethod(
        "OnUse",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CombatStateNextCreatureIdField = typeof(CombatState).GetField(
        "_nextCreatureId",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CombatManagerStateField = typeof(CombatManager).GetField(
        "_state",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CombatHistoryChangedField = typeof(CombatHistory).GetField(
        "Changed",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CombatStateTrackerChangedField = typeof(CombatStateTracker).GetField(
        "CombatStateChanged",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NetCombatCardDbNextIdField = typeof(NetCombatCardDb).GetField(
        "_nextId",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NetCombatCardDbIdToCardField = typeof(NetCombatCardDb).GetField(
        "_idToCard",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NetCombatCardDbCardToIdField = typeof(NetCombatCardDb).GetField(
        "_cardToId",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public static async Task<CombatResolverSimulationResult?> TryEvaluateAsync(
        DeterministicCombatContext context,
        CombatLinePlan plan,
        IReadOnlyDictionary<string, AiLegalActionOption> actionsById,
        CancellationToken ct)
    {
        await SimulationSemaphore.WaitAsync(ct);
        try
        {
            using IsolatedCombatSimulation simulation = IsolatedCombatSimulation.Create(context);
            using NetCombatCardDbSimulationScope netCardDbScope = new();
            netCardDbScope.IndexCombatCards(simulation);
            using CombatManagerSimulationScope managerScope = new(simulation.CombatState);
            foreach (string actionId in plan.ActionIds)
            {
                ct.ThrowIfCancellationRequested();
                netCardDbScope.IndexCombatCards(simulation);
                if (!actionsById.TryGetValue(actionId, out AiLegalActionOption? action) ||
                    !await simulation.TryExecuteAsync(action))
                {
                    return null;
                }
            }

            return simulation.BuildResult(context, plan, actionsById);
        }
        catch (Exception ex)
        {
            Log.Debug($"[AITeammate][Sim] resolver-sim skipped plan=[{string.Join(", ", plan.ActionIds)}] reason={ex}");
            return null;
        }
        finally
        {
            SimulationSemaphore.Release();
        }
    }

    public static async Task<IReadOnlyList<CombatResolverSimulationResult>> TryDeepSearchAsync(
        DeterministicCombatContext context,
        int maxResults,
        CancellationToken ct)
    {
        await SimulationSemaphore.WaitAsync(ct);
        try
        {
            using IsolatedCombatSimulation root = IsolatedCombatSimulation.Create(context);
            using NetCombatCardDbSimulationScope netCardDbScope = new();
            netCardDbScope.IndexCombatCards(root);
            OriginalActionDeepSearch search = new(context, netCardDbScope, Math.Max(1, maxResults));
            IReadOnlyList<CombatResolverSimulationResult> results = await search.SearchAsync(root, ct);
            if (results.Count > 0)
            {
                CombatResolverSimulationResult best = results[0];
                Log.Info($"[AITeammate][Sim] resolver-deep-search actor={context.Actor.NetId} best=[{string.Join(", ", best.Plan.ActionIds)}] score={best.Score} damage={best.DamageDealt} kills={best.EnemiesKilled} actorHpLost={best.ActorHpLost} actorProjected={best.PredictedActorHpLossAfterEnemyTurn} teamHpLost={best.TeamHpLost} teamProjected={best.PredictedTeamHpLossAfterEnemyTurn} teamDeaths={best.PredictedTeamDeathsAfterEnemyTurn} prevented={best.ActorDamagePreventedAfterEnemyTurn}/{best.TeamDamagePreventedAfterEnemyTurn} block={best.ActorBlockAfter} energy={best.ActorEnergyAfter} stars={best.ActorStarsAfter} setup={best.ActorPowerScoreDelta}/{best.TeamPowerScoreDelta}/{best.EnemyControlScoreDelta} rollout={best.RolloutScore} rollingWindow={BattleAiRollingSearchTuning.InitialTargetTurn}/{BattleAiRollingSearchTuning.TargetTurnJump}/{BattleAiRollingSearchTuning.BackstepTurns} potions={best.PotionsUsed} candidates={results.Count} nodes={search.VisitedNodes} elapsedMs={search.ElapsedMilliseconds} truncated={search.WasTruncated}");
            }

            return results;
        }
        catch (Exception ex)
        {
            Log.Debug($"[AITeammate][Sim] resolver-deep-search skipped actor={context.Actor.NetId} reason={ex}");
            return [];
        }
        finally
        {
            SimulationSemaphore.Release();
        }
    }

    private sealed class IsolatedCombatSimulation : IDisposable
    {
        private static readonly ICardResolver RolloutCardResolver = new CardResolver(
            CardCatalogRepository.Shared,
            new CardDefinitionRepository(),
            new RunCardStateStore(),
            new CombatCardStateStore());

        private readonly DeterministicCombatContext _context;
        private readonly CombatState _combatState;
        private readonly Player _actor;
        private readonly Dictionary<uint, CardModel> _cardsByLiveCombatId = new();
        private readonly Dictionary<CardModel, uint> _knownCombatIdByCard = new();
        private readonly Dictionary<string, Creature> _creaturesByTargetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _initialEnemyEffectiveHpByTargetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _initialEnemyHpByTargetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _initialPlayerHpByTargetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _initialPlayerPowerScoreByTargetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _initialEnemyControlScoreByTargetId = new(StringComparer.Ordinal);
        private uint _nextSimulationActionId = 1;
        private bool _disposed;

        private IsolatedCombatSimulation(
            DeterministicCombatContext context,
            CombatState combatState,
            Player actor)
        {
            _context = context;
            _combatState = combatState;
            _actor = actor;
        }

        public CombatState CombatState => _combatState;

        public Player Actor => _actor;

        public static IsolatedCombatSimulation Create(DeterministicCombatContext context)
        {
            CombatState liveCombat = context.Actor.Creature.CombatState
                ?? throw new InvalidOperationException("No live combat state to clone.");
            RunState clonedRun = CloneRunState(context.Actor.RunState);
            Player actor = clonedRun.GetPlayer(context.Actor.NetId)
                ?? throw new InvalidOperationException("Could not find cloned actor.");
            CombatState clonedCombat = new(
                encounter: null,
                runState: clonedRun,
                modifiers: clonedRun.Modifiers,
                multiplayerScalingModel: clonedRun.MultiplayerScalingModel)
            {
                RoundNumber = liveCombat.RoundNumber,
                CurrentSide = liveCombat.CurrentSide
            };

            IsolatedCombatSimulation simulation = new(context, clonedCombat, actor);
            simulation.CloneCreaturesAndCombatPiles(liveCombat, clonedRun);
            simulation.CaptureInitialState();
            return simulation;
        }

        public IsolatedCombatSimulation Fork()
        {
            RunState clonedRun = CloneRunState(_actor.RunState);
            Player actor = clonedRun.GetPlayer(_actor.NetId)
                ?? throw new InvalidOperationException("Could not find forked actor.");
            CombatState clonedCombat = new(
                encounter: null,
                runState: clonedRun,
                modifiers: clonedRun.Modifiers,
                multiplayerScalingModel: clonedRun.MultiplayerScalingModel)
            {
                RoundNumber = _combatState.RoundNumber,
                CurrentSide = _combatState.CurrentSide
            };

            IsolatedCombatSimulation simulation = new(_context, clonedCombat, actor);
            simulation.CloneCreaturesAndCombatPiles(_combatState, clonedRun, this);
            simulation.CopyInitialStateFrom(this);
            return simulation;
        }

        public async Task<bool> TryExecuteAsync(AiLegalActionOption action)
        {
            if (string.Equals(action.ActionType, AiTeammateActionKind.PlayCard.ToString(), StringComparison.Ordinal))
            {
                return await TryExecuteCardAsync(SimulationAction.FromLegalAction(action));
            }

            if (string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
            {
                return await TryExecutePotionAsync(SimulationAction.FromLegalAction(action));
            }

            return false;
        }

        public async Task<bool> TryExecuteAsync(SimulationAction action)
        {
            if (action.Kind == SimulationActionKind.PlayCard)
            {
                return await TryExecuteCardAsync(action);
            }

            if (action.Kind == SimulationActionKind.UsePotion)
            {
                return await TryExecutePotionAsync(action);
            }

            return false;
        }

        public IReadOnlyList<SimulationAction> BuildLegalActions()
        {
            List<SimulationAction> actions = [];
            IReadOnlyList<CardModel> hand = PileType.Hand.GetPile(_actor).Cards;
            for (int handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                CardModel card = hand[handIndex];
                UnplayableReason reason;
                AbstractModel? preventer;
                if (!card.CanPlay(out reason, out preventer))
                {
                    continue;
                }

                bool addedAction = false;
                foreach (Creature? target in GetOrderedTargets(card.TargetType))
                {
                    if (target == null || IsPlayableTarget(card, target))
                    {
                        actions.Add(BuildPlayCardSimulationAction(card, handIndex, target));
                        addedAction = true;
                        if (!card.TargetType.IsSingleTarget())
                        {
                            break;
                        }
                    }
                }

                if (!addedAction && card.TargetType == TargetType.Self)
                {
                    actions.Add(BuildPlayCardSimulationAction(card, handIndex, null));
                }
            }

            List<PotionModel> potions = _actor.Potions.Where(static potion => !potion.IsQueued).ToList();
            for (int potionIndex = 0; potionIndex < potions.Count; potionIndex++)
            {
                PotionModel potion = potions[potionIndex];
                List<Creature?> targets = GetOrderedTargets(potion.TargetType).ToList();
                if (potion.TargetType.IsSingleTarget() && targets.Count == 0)
                {
                    continue;
                }

                if (!potion.TargetType.IsSingleTarget())
                {
                    targets = [targets.FirstOrDefault()];
                }

                foreach (Creature? target in targets)
                {
                    actions.Add(new SimulationAction
                    {
                        ActionId = BuildUsePotionActionId(potion, target, potionIndex),
                        Kind = SimulationActionKind.UsePotion,
                        ModelId = potion.Id.Entry,
                        TargetId = GetTargetId(target),
                        PotionIndex = potionIndex
                    });
                }
            }

            return actions;
        }

        public bool HasAliveEnemies()
        {
            return _combatState.Enemies.Any(static enemy => enemy.IsAlive);
        }

        public bool ActorCanAct()
        {
            return _actor.Creature.IsAlive && _actor.PlayerCombatState != null;
        }

        public int EstimateImmediateActionPriority(SimulationAction action)
        {
            if (action.Kind == SimulationActionKind.UsePotion)
            {
                int potionPriority = _context.IsEliteOrBossCombat || _context.IsTeamInCrisis ? 80 : -80;
                if (IsHighValuePotion(action))
                {
                    potionPriority -= _context.IsEliteOrBossCombat ? 80 : 260;
                }

                return potionPriority;
            }

            if (!TryResolveCard(action, out CardModel? card) || card == null)
            {
                return int.MinValue;
            }

            int priority = card.Type switch
            {
                CardType.Attack => _context.HasSustainedAttackPressure ? 240 : 170,
                CardType.Power => _context.IsEliteOrBossCombat || _context.HasSustainedAttackPressure ? 290 : 185,
                CardType.Skill => _context.TeamIncomingDamageAfterBlock > 0
                    ? 130
                    : _context.HasSustainedAttackPressure ? 55 : 80,
                _ => 20
            };
            priority -= Math.Max(0, card.EnergyCost.GetAmountToSpend()) * 8;
            priority += StatusCardStrategy.IsAllowedHandCleanupTarget(card, _actor) ? 70 : 0;
            return priority;
        }

        private SimulationAction BuildPlayCardSimulationAction(CardModel card, int handIndex, Creature? target)
        {
            uint? knownCombatCardId = _knownCombatIdByCard.TryGetValue(card, out uint combatCardId)
                ? combatCardId
                : null;
            string cardInstanceId = knownCombatCardId.HasValue
                ? $"combat_{knownCombatCardId.Value}"
                : $"sim_{handIndex}_{SanitizeActionToken(card.Id.Entry)}";
            Creature? executionTarget = card.TargetType == TargetType.Self ? null : target;
            return new SimulationAction
            {
                ActionId = BuildPlayCardActionId(cardInstanceId, executionTarget),
                Kind = SimulationActionKind.PlayCard,
                ModelId = card.Id.Entry,
                TargetId = GetTargetId(executionTarget),
                KnownCombatCardId = knownCombatCardId,
                HandIndex = knownCombatCardId.HasValue ? null : handIndex
            };
        }

        private IEnumerable<Creature?> GetOrderedTargets(TargetType targetType)
        {
            return targetType switch
            {
                TargetType.AnyEnemy => _combatState.HittableEnemies.OrderBy(static creature => creature.CombatId ?? uint.MaxValue).Cast<Creature?>(),
                TargetType.AnyAlly => _combatState.PlayerCreatures.Where(static creature => creature.IsAlive).OrderBy(static creature => creature.Player?.NetId ?? 0UL).Cast<Creature?>(),
                TargetType.AnyPlayer => _combatState.PlayerCreatures.Where(static creature => creature.IsAlive).OrderBy(static creature => creature.Player?.NetId ?? 0UL).Cast<Creature?>(),
                TargetType.Self => new Creature?[] { _actor.Creature },
                _ => new Creature?[] { null },
            };
        }

        private bool IsPlayableTarget(CardModel card, Creature target)
        {
            if (card.TargetType == TargetType.Self && ReferenceEquals(target, _actor.Creature))
            {
                return true;
            }

            return card.CanPlayTargeting(target);
        }

        public CombatResolverSimulationResult BuildResult(
            DeterministicCombatContext context,
            CombatLinePlan plan,
            IReadOnlyDictionary<string, AiLegalActionOption> actionsById)
        {
            int potionsUsed = 0;
            int highValuePotionsUsed = 0;
            foreach (string actionId in plan.ActionIds)
            {
                if (!actionsById.TryGetValue(actionId, out AiLegalActionOption? action) ||
                    !string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal))
                {
                    continue;
                }

                potionsUsed++;
                if (IsHighValuePotion(action.CardId))
                {
                    highValuePotionsUsed++;
                }
            }

            return BuildResult(context, plan, potionsUsed, highValuePotionsUsed);
        }

        public CombatResolverSimulationResult BuildResult(
            DeterministicCombatContext context,
            CombatLinePlan plan,
            int potionsUsed,
            int highValuePotionsUsed)
        {
            int damageDealt = 0;
            int enemiesKilled = 0;
            int nonMinionEnemiesKilled = 0;
            foreach (KeyValuePair<string, int> initial in _initialEnemyEffectiveHpByTargetId)
            {
                if (!_creaturesByTargetId.TryGetValue(initial.Key, out Creature? enemy))
                {
                    continue;
                }

                int finalEffectiveHp = Math.Max(0, enemy.CurrentHp) + Math.Max(0, enemy.Block);
                damageDealt += Math.Max(0, initial.Value - finalEffectiveHp);
                if (_initialEnemyHpByTargetId.GetValueOrDefault(initial.Key) > 0 && enemy.CurrentHp <= 0)
                {
                    enemiesKilled++;
                    if (!context.EnemiesById.TryGetValue(initial.Key, out DeterministicEnemyState? enemyState) ||
                        !enemyState.IsLikelySummonedAdd)
                    {
                        nonMinionEnemiesKilled++;
                    }
                }
            }

            int initialTeamHp = _initialPlayerHpByTargetId.Values.Sum();
            int finalTeamHp = _creaturesByTargetId
                .Where(static pair => pair.Key.StartsWith("player_", StringComparison.Ordinal))
                .Sum(static pair => Math.Max(0, pair.Value.CurrentHp));
            int actorInitialHp = _initialPlayerHpByTargetId.GetValueOrDefault($"player_{context.Actor.NetId}");
            int actorHpLost = Math.Max(0, actorInitialHp - Math.Max(0, _actor.Creature.CurrentHp));
            int actorPowerScoreDelta = EstimatePlayerPowerScore(_actor.Creature) -
                                       _initialPlayerPowerScoreByTargetId.GetValueOrDefault($"player_{context.Actor.NetId}");
            int teamPowerScoreDelta = EstimateOtherTeamPowerScoreDelta(context);
            int enemyControlScoreDelta = EstimateEnemyControlScoreDelta();
            bool killedAllEnemies = _initialEnemyEffectiveHpByTargetId.Count > 0 &&
                                    _initialEnemyEffectiveHpByTargetId.Keys.All(targetId =>
                                        _creaturesByTargetId.TryGetValue(targetId, out Creature? enemy) &&
                                        enemy.CurrentHp <= 0);
            bool killedAllNonMinionEnemies = _initialEnemyEffectiveHpByTargetId.Keys
                .Where(targetId => !context.EnemiesById.TryGetValue(targetId, out DeterministicEnemyState? enemy) ||
                                   !enemy.IsLikelySummonedAdd)
                .All(targetId => _creaturesByTargetId.TryGetValue(targetId, out Creature? enemy) &&
                                 enemy.CurrentHp <= 0);
            EndTurnDamageProjection endTurnProjection = EstimateEndTurnDamageProjection(context, killedAllEnemies);
            int rolloutScore = EstimateRolloutScore(context, killedAllEnemies, endTurnProjection);
            CombatResolverSimulationResult result = new()
            {
                Plan = plan,
                Score = 0,
                DamageDealt = damageDealt,
                EnemiesKilled = enemiesKilled,
                NonMinionEnemiesKilled = nonMinionEnemiesKilled,
                KilledAllEnemies = killedAllEnemies,
                KilledAllNonMinionEnemies = killedAllNonMinionEnemies,
                ActorHpLost = actorHpLost,
                TeamHpLost = Math.Max(0, initialTeamHp - finalTeamHp),
                PredictedActorHpLossAfterEnemyTurn = endTurnProjection.ActorHpLoss,
                PredictedTeamHpLossAfterEnemyTurn = endTurnProjection.TeamHpLoss,
                PredictedTeamDeathsAfterEnemyTurn = endTurnProjection.TeamDeaths,
                ActorDamagePreventedAfterEnemyTurn = endTurnProjection.ActorDamagePrevented,
                TeamDamagePreventedAfterEnemyTurn = endTurnProjection.TeamDamagePrevented,
                ActorBlockAfter = Math.Max(0, _actor.Creature.Block),
                ActorEnergyAfter = Math.Max(0, _actor.PlayerCombatState?.Energy ?? 0),
                ActorStarsAfter = Math.Max(0, _actor.PlayerCombatState?.Stars ?? 0),
                ActorPowerScoreDelta = actorPowerScoreDelta,
                TeamPowerScoreDelta = teamPowerScoreDelta,
                EnemyControlScoreDelta = enemyControlScoreDelta,
                RolloutScore = rolloutScore,
                PotionsUsed = potionsUsed,
                HighValuePotionsUsed = highValuePotionsUsed
            };

            return new CombatResolverSimulationResult
            {
                Plan = result.Plan,
                Score = CombatResolverSimulationRefiner.ScoreSimulationResult(context, plan, result),
                DamageDealt = result.DamageDealt,
                EnemiesKilled = result.EnemiesKilled,
                NonMinionEnemiesKilled = result.NonMinionEnemiesKilled,
                KilledAllEnemies = result.KilledAllEnemies,
                KilledAllNonMinionEnemies = result.KilledAllNonMinionEnemies,
                ActorHpLost = result.ActorHpLost,
                TeamHpLost = result.TeamHpLost,
                PredictedActorHpLossAfterEnemyTurn = result.PredictedActorHpLossAfterEnemyTurn,
                PredictedTeamHpLossAfterEnemyTurn = result.PredictedTeamHpLossAfterEnemyTurn,
                PredictedTeamDeathsAfterEnemyTurn = result.PredictedTeamDeathsAfterEnemyTurn,
                ActorDamagePreventedAfterEnemyTurn = result.ActorDamagePreventedAfterEnemyTurn,
                TeamDamagePreventedAfterEnemyTurn = result.TeamDamagePreventedAfterEnemyTurn,
                ActorBlockAfter = result.ActorBlockAfter,
                ActorEnergyAfter = result.ActorEnergyAfter,
                ActorStarsAfter = result.ActorStarsAfter,
                ActorPowerScoreDelta = result.ActorPowerScoreDelta,
                TeamPowerScoreDelta = result.TeamPowerScoreDelta,
                EnemyControlScoreDelta = result.EnemyControlScoreDelta,
                RolloutScore = result.RolloutScore,
                PotionsUsed = result.PotionsUsed,
                HighValuePotionsUsed = result.HighValuePotionsUsed
            };
        }

        private int EstimateOtherTeamPowerScoreDelta(DeterministicCombatContext context)
        {
            int delta = 0;
            string actorTargetId = $"player_{context.Actor.NetId}";
            foreach (KeyValuePair<string, int> initial in _initialPlayerPowerScoreByTargetId)
            {
                if (string.Equals(initial.Key, actorTargetId, StringComparison.Ordinal) ||
                    !_creaturesByTargetId.TryGetValue(initial.Key, out Creature? playerCreature))
                {
                    continue;
                }

                int finalScore = playerCreature.CurrentHp > 0
                    ? EstimatePlayerPowerScore(playerCreature)
                    : 0;
                delta += Math.Clamp(finalScore - initial.Value, -1_200, 2_500);
            }

            return delta;
        }

        private int EstimateEnemyControlScoreDelta()
        {
            int delta = 0;
            foreach (KeyValuePair<string, int> initial in _initialEnemyControlScoreByTargetId)
            {
                if (!_creaturesByTargetId.TryGetValue(initial.Key, out Creature? enemy) ||
                    enemy.CurrentHp <= 0)
                {
                    continue;
                }

                int finalScore = EstimateEnemyControlScore(enemy);
                delta += Math.Clamp(finalScore - initial.Value, -1_600, 2_800);
            }

            return delta;
        }

        private int EstimateRolloutScore(
            DeterministicCombatContext context,
            bool killedAllEnemies,
            EndTurnDamageProjection endTurnProjection)
        {
            if (killedAllEnemies)
            {
                return 0;
            }

            if (endTurnProjection.ActorHpLoss >= Math.Max(1, _actor.Creature.CurrentHp))
            {
                return -24_000;
            }

            int score = 0;
            score -= endTurnProjection.ActorHpLoss * (CombatResolverSimulationRefiner.ShouldUseBrawlScoring(context) ? 70 : 115);
            score -= Math.Max(0, endTurnProjection.TeamDeaths) * 8_000;
            score -= Math.Max(0, endTurnProjection.TeamHpLoss - endTurnProjection.ActorHpLoss) * 35;
            score += EstimateRollingHorizonPotential(context, _actor, isActor: true);

            foreach (Player player in _combatState.Players)
            {
                if (player.NetId == _actor.NetId || player.PlayerCombatState == null || player.Creature.IsDead)
                {
                    continue;
                }

                score += EstimateRollingHorizonPotential(context, player, isActor: false) / 4;
            }

            int livingEnemyEffectiveHp = _combatState.Enemies
                .Where(static enemy => enemy.IsAlive)
                .Sum(static enemy => Math.Max(0, enemy.CurrentHp) + Math.Max(0, enemy.Block));
            if (livingEnemyEffectiveHp > 0 && context.HasSustainedAttackPressure)
            {
                score -= Math.Min(2_400, livingEnemyEffectiveHp * 8);
            }

            return Math.Clamp(score, -30_000, 18_000);
        }

        private int EstimateRollingHorizonPotential(
            DeterministicCombatContext context,
            Player player,
            bool isActor)
        {
            if (player.PlayerCombatState == null || player.Creature.IsDead)
            {
                return -4_000;
            }

            RolloutDeckState deck = BuildRolloutDeckState(player);
            if (!deck.HasCards)
            {
                return -850;
            }

            int score = 0;
            int abstractLoads = 0;
            int livingEnemyCount = Math.Max(1, _combatState.Enemies.Count(static enemy => enemy.IsAlive));
            for (int turn = 1; turn <= BattleAiRollingSearchTuning.InitialTargetTurn; turn++)
            {
                IReadOnlyList<CardModel> hand = DrawRollingHand(context, player, deck, firstTurn: turn == 1);
                if (hand.Count == 0)
                {
                    score -= GetRollingTurnWeight(turn) * 120 / 100;
                    continue;
                }

                RollingTurnEvaluation turnEvaluation = EstimateRollingTurnPotential(context, player, hand, turn, isActor);
                score += turnEvaluation.Score * GetRollingTurnWeight(turn) / 100;
                AdvanceRollingDeck(deck, hand, turnEvaluation.PlayedCards);

                abstractLoads += Math.Max(1, hand.Count) * livingEnemyCount;
                if (abstractLoads >= BattleAiRollingSearchTuning.MaxTurnLoads)
                {
                    break;
                }
            }

            int projectedHp = Math.Max(0, player.Creature.CurrentHp);
            int maxHp = Math.Max(1, player.Creature.MaxHp);
            if (projectedHp * 100 / maxHp <= 30)
            {
                score -= isActor ? 900 : 300;
            }

            int statusBurden = deck.DrawPile.Concat(deck.DiscardPile)
                .Select(card => ResolveRolloutCard(card))
                .Count(StatusCardStrategy.IsNegativeStatusOrCurse);
            score -= Math.Min(2_400, statusBurden * (isActor ? 180 : 70));

            return Math.Clamp(score, isActor ? -10_000 : -5_000, isActor ? 16_000 : 9_000);
        }

        private RolloutDeckState BuildRolloutDeckState(Player player)
        {
            List<CardModel> retained = PileType.Hand.GetPile(player).Cards
                .Where(static card => card.ShouldRetainThisTurn)
                .Take(10)
                .ToList();
            List<CardModel> drawPile = PileType.Draw.GetPile(player).Cards.ToList();
            IEnumerable<CardModel> flushableHand = PileType.Hand.GetPile(player).Cards
                .Where(static card => !card.ShouldRetainThisTurn && !card.Keywords.Contains(CardKeyword.Ethereal));
            List<CardModel> discardPile = PileType.Discard.GetPile(player).Cards
                .Concat(flushableHand)
                .ToList();
            return new RolloutDeckState(drawPile, discardPile, retained);
        }

        private IReadOnlyList<CardModel> DrawRollingHand(
            DeterministicCombatContext context,
            Player player,
            RolloutDeckState deck,
            bool firstTurn)
        {
            List<CardModel> hand = firstTurn ? deck.RetainedHand.Take(10).ToList() : [];
            int drawSlots = Math.Max(0, Math.Min(5, 10 - hand.Count));
            for (int draw = 0; draw < drawSlots; draw++)
            {
                CardModel? card = DrawRollingCard(context, player, deck);
                if (card == null)
                {
                    break;
                }

                hand.Add(card);
            }

            return hand;
        }

        private CardModel? DrawRollingCard(
            DeterministicCombatContext context,
            Player player,
            RolloutDeckState deck)
        {
            if (deck.DrawPile.Count == 0)
            {
                if (deck.DiscardPile.Count == 0)
                {
                    return null;
                }

                deck.DrawPile.AddRange(deck.DiscardPile
                    .Select(card => BuildRolloutCardValue(context, player, card))
                    .OrderByDescending(static value => value.Priority)
                    .ThenBy(static value => value.CardId, StringComparer.Ordinal)
                    .Select(static value => value.Card));
                deck.DiscardPile.Clear();
            }

            CardModel drawn = deck.DrawPile[0];
            deck.DrawPile.RemoveAt(0);
            return drawn;
        }

        private RollingTurnEvaluation EstimateRollingTurnPotential(
            DeterministicCombatContext context,
            Player player,
            IReadOnlyList<CardModel> hand,
            int turn,
            bool isActor)
        {
            int remainingEnergy = Math.Max(0, player.PlayerCombatState?.MaxEnergy ?? 0);
            int remainingStars = Math.Max(0, player.PlayerCombatState?.Stars ?? 0);
            int score = 0;
            List<CardModel> playedCards = [];
            foreach (RolloutCardValue candidate in hand
                         .Select(card => BuildRolloutCardValue(context, player, card))
                         .Select(value => value with
                         {
                             Priority = AdjustRollingCardPriority(context, player, value, turn, isActor)
                         })
                         .OrderByDescending(static value => value.Priority)
                         .ThenBy(static value => value.CardId, StringComparer.Ordinal))
            {
                if (candidate.IsNegativeStatus)
                {
                    score += candidate.Priority;
                    continue;
                }

                if (candidate.EnergyCost > remainingEnergy || candidate.StarCost > remainingStars)
                {
                    score += Math.Min(100, candidate.Priority / 7);
                    continue;
                }

                remainingEnergy -= candidate.EnergyCost;
                remainingStars -= candidate.StarCost;
                score += candidate.Priority;
                playedCards.Add(candidate.Card);
                remainingEnergy = Math.Min(9, remainingEnergy + candidate.EnergyGain);
                remainingStars = Math.Min(99, remainingStars + candidate.StarsGenerated);
            }

            int handBurden = hand
                .Select(card => ResolveRolloutCard(card))
                .Count(StatusCardStrategy.IsNegativeStatusOrCurse);
            score -= handBurden * (turn <= BattleAiRollingSearchTuning.BackstepTurns ? 420 : 260);

            bool dealtDamage = playedCards
                .Select(card => ResolveRolloutCard(card))
                .Any(card => card.GetEstimatedDamage() > 0);
            if (context.HasSustainedAttackPressure && turn <= BattleAiRollingSearchTuning.BackstepTurns && !dealtDamage)
            {
                score -= isActor ? 850 : 260;
            }

            return new RollingTurnEvaluation(Math.Clamp(score, -6_000, isActor ? 9_000 : 5_000), playedCards);
        }

        private int AdjustRollingCardPriority(
            DeterministicCombatContext context,
            Player player,
            RolloutCardValue value,
            int turn,
            bool isActor)
        {
            if (value.IsNegativeStatus)
            {
                return value.Priority;
            }

            ResolvedCardView resolved = ResolveRolloutCard(value.Card);
            OrbEvokeEstimate evoke = resolved.EstimateOrbEvoke(player, player.PlayerCombatState?.MaxEnergy ?? 0, Math.Max(0, resolved.EffectiveCost));
            int damage = resolved.GetEstimatedDamage() + evoke.Damage;
            int block = resolved.GetEstimatedBlock() + evoke.Block;
            int draw = resolved.GetCardsDrawn();
            int priority = value.Priority;

            if (turn <= BattleAiRollingSearchTuning.BackstepTurns)
            {
                priority += damage * (context.HasSustainedAttackPressure ? 34 : 18);
                priority += block * (context.TeamIncomingDamageAfterBlock > 0 ? 16 : 4);
            }
            else if (turn <= BattleAiRollingSearchTuning.TargetTurnJump)
            {
                priority += resolved.Type == CardType.Power ? (isActor ? 210 : 80) : 0;
                priority += draw * 70;
                priority += Math.Max(0, value.EnergyGain) * 55;
            }
            else
            {
                priority += resolved.Type == CardType.Power ? (isActor ? 150 : 55) : 0;
                priority += draw * 45;
            }

            if (context.HasSustainedAttackPressure && damage > 0)
            {
                priority += Math.Max(0, BattleAiRollingSearchTuning.InitialTargetTurn - turn + 1) * 24;
            }

            if (StatusCardStrategy.IsLikelyHandCleanupCard(resolved) && EstimateFutureCleanupNeed(player) > 0)
            {
                priority += turn <= BattleAiRollingSearchTuning.BackstepTurns ? 140 : 80;
            }

            return Math.Clamp(priority, -2_000, 5_500);
        }

        private static int GetRollingTurnWeight(int turn)
        {
            if (turn <= BattleAiRollingSearchTuning.BackstepTurns)
            {
                return 100;
            }

            if (turn <= BattleAiRollingSearchTuning.TargetTurnJump)
            {
                return 72;
            }

            return 50;
        }

        private void AdvanceRollingDeck(
            RolloutDeckState deck,
            IReadOnlyList<CardModel> hand,
            IReadOnlyList<CardModel> playedCards)
        {
            HashSet<CardModel> played = playedCards.ToHashSet();
            foreach (CardModel card in hand)
            {
                bool wasPlayed = played.Contains(card);
                ResolvedCardView resolved = ResolveRolloutCard(card);
                if (wasPlayed && resolved.Exhaust)
                {
                    continue;
                }

                if (!wasPlayed && card.Keywords.Contains(CardKeyword.Ethereal))
                {
                    continue;
                }

                deck.DiscardPile.Add(card);
            }
        }

        private int EstimateNextPlayerTurnPotential(
            DeterministicCombatContext context,
            Player player,
            bool isActor)
        {
            if (player.PlayerCombatState == null || player.Creature.IsDead)
            {
                return -4_000;
            }

            IReadOnlyList<CardModel> nextHand = PredictNextTurnHand(player);
            if (nextHand.Count == 0)
            {
                return -850;
            }

            int energy = Math.Max(0, player.PlayerCombatState.MaxEnergy);
            int stars = Math.Max(0, player.PlayerCombatState.Stars);
            int score = 0;
            int remainingEnergy = energy;
            int remainingStars = stars;
            foreach (RolloutCardValue candidate in nextHand
                         .Select(card => BuildRolloutCardValue(context, player, card))
                         .OrderByDescending(static value => value.Priority)
                         .ThenBy(static value => value.CardId, StringComparer.Ordinal))
            {
                if (candidate.IsNegativeStatus)
                {
                    score += candidate.Priority;
                    continue;
                }

                if (candidate.EnergyCost > remainingEnergy || candidate.StarCost > remainingStars)
                {
                    score += Math.Min(80, candidate.Priority / 6);
                    continue;
                }

                remainingEnergy -= candidate.EnergyCost;
                remainingStars -= candidate.StarCost;
                score += candidate.Priority;
                remainingEnergy = Math.Min(9, remainingEnergy + candidate.EnergyGain);
                remainingStars = Math.Min(99, remainingStars + candidate.StarsGenerated);
            }

            int handBurden = nextHand
                .Select(card => ResolveRolloutCard(card))
                .Count(StatusCardStrategy.IsNegativeStatusOrCurse);
            score -= handBurden * 360;

            int projectedHp = Math.Max(0, player.Creature.CurrentHp);
            int maxHp = Math.Max(1, player.Creature.MaxHp);
            if (projectedHp * 100 / maxHp <= 30)
            {
                score -= isActor ? 900 : 300;
            }

            return Math.Clamp(score, isActor ? -8_000 : -4_000, isActor ? 12_000 : 8_000);
        }

        private IReadOnlyList<CardModel> PredictNextTurnHand(Player player)
        {
            if (player.PlayerCombatState == null)
            {
                return [];
            }

            List<CardModel> retained = PileType.Hand.GetPile(player).Cards
                .Where(static card => card.ShouldRetainThisTurn)
                .Take(10)
                .ToList();
            int drawSlots = Math.Max(0, Math.Min(5, 10 - retained.Count));
            if (drawSlots == 0)
            {
                return retained;
            }

            List<CardModel> predicted = retained.ToList();
            List<CardModel> drawPile = PileType.Draw.GetPile(player).Cards.ToList();
            foreach (CardModel card in drawPile.Take(drawSlots))
            {
                predicted.Add(card);
            }

            int remainingDraws = drawSlots - Math.Max(0, predicted.Count - retained.Count);
            if (remainingDraws <= 0)
            {
                return predicted;
            }

            IEnumerable<CardModel> flushableHand = PileType.Hand.GetPile(player).Cards
                .Where(static card => !card.ShouldRetainThisTurn && !card.Keywords.Contains(CardKeyword.Ethereal));
            List<CardModel> shufflePool = PileType.Discard.GetPile(player).Cards
                .Concat(flushableHand)
                .Where(card => !predicted.Contains(card))
                .Select(card => BuildRolloutCardValue(_context, player, card))
                .OrderByDescending(static value => value.Priority)
                .ThenBy(static value => value.CardId, StringComparer.Ordinal)
                .Select(static value => value.Card)
                .Take(remainingDraws)
                .ToList();
            predicted.AddRange(shufflePool);
            return predicted;
        }

        private RolloutCardValue BuildRolloutCardValue(
            DeterministicCombatContext context,
            Player player,
            CardModel card)
        {
            ResolvedCardView resolved = ResolveRolloutCard(card);
            bool negativeStatus = StatusCardStrategy.IsNegativeStatusOrCurse(resolved);
            if (negativeStatus)
            {
                return new RolloutCardValue(card, resolved.CardId, -520, 0, 0, 0, 0, IsNegativeStatus: true);
            }

            int energyCost = Math.Max(0, resolved.EffectiveCost);
            int starCost = Math.Max(0, resolved.StarCost);
            OrbEvokeEstimate evoke = resolved.EstimateOrbEvoke(player, player.PlayerCombatState?.MaxEnergy ?? 0, energyCost);
            int damage = resolved.GetEstimatedDamage() + evoke.Damage;
            int block = resolved.GetEstimatedBlock() + evoke.Block;
            int summon = resolved.GetSummonAmount();
            int cardsDrawn = resolved.GetCardsDrawn();
            int energyGain = Math.Max(0, resolved.GetEnergyGain() + evoke.Energy);
            int starsGenerated = Math.Max(0, resolved.GetStarsGenerated());
            int enemyDebuff = Math.Max(0, resolved.GetEnemyVulnerableAmount()) +
                              Math.Max(0, resolved.GetEnemyWeakAmount());
            int selfScaling = Math.Max(0, resolved.GetSelfStrengthAmount()) +
                              Math.Max(0, resolved.GetSelfDexterityAmount());

            int priority = 0;
            priority += damage * (context.HasSustainedAttackPressure ? 115 : 82);
            priority += block * (context.TeamIncomingDamageAfterBlock > 0 ? 48 : 18);
            priority += summon * (context.TeamIncomingDamageAfterBlock > 0 ? 70 : 34);
            priority += cardsDrawn * 115;
            priority += energyGain * 95;
            priority += starsGenerated * 40;
            priority += enemyDebuff * 260;
            priority += selfScaling * 240;
            priority += resolved.Type switch
            {
                CardType.Power => context.IsEliteOrBossCombat || context.HasSustainedAttackPressure ? 520 : 260,
                CardType.Attack => damage > 0 ? 90 : 0,
                CardType.Skill => block > 0 || summon > 0 ? 70 : 0,
                _ => 0
            };

            if (StatusCardStrategy.IsLikelyHandCleanupCard(resolved))
            {
                priority += EstimateFutureCleanupNeed(player) > 0 ? 360 : -120;
            }

            if (resolved.Exhaust && priority < 260)
            {
                priority -= 160;
            }

            priority -= energyCost * 28;
            priority -= starCost * 18;
            return new RolloutCardValue(
                card,
                resolved.CardId,
                Math.Clamp(priority, -1_500, 4_000),
                energyCost,
                starCost,
                energyGain,
                starsGenerated,
                IsNegativeStatus: false);
        }

        private int EstimateFutureCleanupNeed(Player player)
        {
            if (player.PlayerCombatState == null)
            {
                return 0;
            }

            return player.PlayerCombatState.AllCards
                .Select(card => ResolveRolloutCard(card))
                .Count(StatusCardStrategy.IsNegativeStatusOrCurse);
        }

        private ResolvedCardView ResolveRolloutCard(CardModel card)
        {
            string cardInstanceId = _knownCombatIdByCard.TryGetValue(card, out uint combatCardId)
                ? $"combat_{combatCardId}"
                : $"rollout_{SanitizeActionToken(card.Id.Entry)}";
            return RolloutCardResolver.Resolve(card, cardInstanceId);
        }

        private EndTurnDamageProjection EstimateEndTurnDamageProjection(
            DeterministicCombatContext context,
            bool killedAllEnemies)
        {
            int actorHpLoss = 0;
            int teamHpLoss = 0;
            int teamDeaths = 0;
            int actorPrevented = 0;
            int teamPrevented = 0;
            foreach (DeterministicPlayerState playerState in context.PlayerStatesById.Values)
            {
                if (!_creaturesByTargetId.TryGetValue(playerState.Id, out Creature? creature))
                {
                    continue;
                }

                int initialHp = Math.Max(0, _initialPlayerHpByTargetId.GetValueOrDefault(playerState.Id));
                int finalHp = Math.Max(0, creature.CurrentHp);
                int immediateHpLoss = Math.Max(0, initialHp - finalHp);
                int projectedEnemyTurnDamage = killedAllEnemies
                    ? 0
                    : EstimateProjectedEnemyTurnDamage(playerState, creature);
                int projectedTotalLoss = immediateHpLoss + projectedEnemyTurnDamage;
                int prevented = Math.Max(0, playerState.IncomingDamageAfterBlock - projectedEnemyTurnDamage);

                teamHpLoss += projectedTotalLoss;
                teamPrevented += prevented;
                if (projectedTotalLoss >= Math.Max(1, initialHp))
                {
                    teamDeaths++;
                }

                if (playerState.IsActor)
                {
                    actorHpLoss = projectedTotalLoss;
                    actorPrevented = prevented;
                }
            }

            return new EndTurnDamageProjection(
                actorHpLoss,
                teamHpLoss,
                teamDeaths,
                actorPrevented,
                teamPrevented);
        }

        private static int EstimateProjectedEnemyTurnDamage(
            DeterministicPlayerState playerState,
            Creature finalCreature)
        {
            int finalBlock = Math.Max(0, finalCreature.Block);
            int automaticProtectionDelta = Math.Max(0, playerState.ExpectedBlockAtEnemyTurn - playerState.Block);
            int projectedBlockAtEnemyTurn = Math.Max(finalBlock, finalBlock + automaticProtectionDelta);
            return Math.Max(0, playerState.IncomingDamage - projectedBlockAtEnemyTurn) +
                   Math.Max(0, playerState.UnblockableIncomingDamage);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (Player player in _combatState.Players.ToList())
            {
                player.AfterCombatEnd();
            }

            foreach (Creature creature in _combatState.Creatures.ToList())
            {
                creature.RemoveAllPowersInternalExcept();
                if (creature.CombatState == _combatState)
                {
                    _combatState.RemoveCreature(creature);
                }
            }

            _disposed = true;
        }

        private static RunState CloneRunState(IRunState liveRun)
        {
            SerializableRun save = new()
            {
                Acts = liveRun.Acts.Select(ToSerializableAct).ToList(),
                Modifiers = liveRun.Modifiers.Select(ToSerializableModifier).ToList(),
                CurrentActIndex = liveRun.CurrentActIndex,
                SerializableOdds = liveRun.Odds.ToSerializable(),
                SerializableSharedRelicGrabBag = liveRun.SharedRelicGrabBag.ToSerializable(),
                Players = liveRun.Players.Select(static player => player.ToSerializable()).ToList(),
                SerializableRng = liveRun.Rng.ToSerializable(),
                Ascension = liveRun.AscensionLevel,
                GameMode = liveRun.GameMode,
                MapPointHistory = []
            };

            return RunState.FromSerializable(save);
        }

        private static SerializableActModel ToSerializableAct(ActModel act)
        {
            return act.IsMutable
                ? act.ToSave()
                : act.ToMutable().ToSave();
        }

        private static SerializableModifier ToSerializableModifier(ModifierModel modifier)
        {
            return modifier.IsMutable
                ? modifier.ToSerializable()
                : modifier.ToMutable().ToSerializable();
        }

        private void CloneCreaturesAndCombatPiles(
            CombatState liveCombat,
            RunState clonedRun,
            IsolatedCombatSimulation? sourceSimulation = null)
        {
            foreach (Player clonedPlayer in clonedRun.Players)
            {
                _combatState.AddPlayer(clonedPlayer);
                clonedPlayer.ResetCombatState();
            }

            foreach (Player livePlayer in liveCombat.Players)
            {
                Player clonedPlayer = clonedRun.GetPlayer(livePlayer.NetId)
                    ?? throw new InvalidOperationException($"Missing cloned player {livePlayer.NetId}.");
                CopyCreatureRuntimeState(livePlayer.Creature, clonedPlayer.Creature);
                CopyCombatPiles(livePlayer, clonedPlayer, sourceSimulation);
                CopyOrbs(livePlayer, clonedPlayer);
                RegisterCreatureTarget(clonedPlayer.Creature);
            }

            foreach (Creature liveAlly in liveCombat.Allies
                         .Where(static creature => !creature.IsPlayer)
                         .OrderBy(static creature => creature.CombatId ?? uint.MaxValue))
            {
                CloneMonsterCreature(liveAlly, clonedRun);
            }

            foreach (Creature liveEnemy in liveCombat.Enemies.OrderBy(static creature => creature.CombatId ?? uint.MaxValue))
            {
                CloneMonsterCreature(liveEnemy, clonedRun);
            }

            uint nextCreatureId = _combatState.Creatures
                .Select(static creature => creature.CombatId ?? 0u)
                .DefaultIfEmpty(0u)
                .Max() + 1u;
            CombatStateNextCreatureIdField?.SetValue(_combatState, nextCreatureId);
        }

        private void CloneMonsterCreature(Creature liveCreature, RunState clonedRun)
        {
            if (liveCreature.Monster == null)
            {
                return;
            }

            MonsterModel clonedMonster = ModelDb.GetById<MonsterModel>(liveCreature.Monster.Id).ToMutable();
            Creature clonedCreature = _combatState.CreateCreature(clonedMonster, liveCreature.Side, liveCreature.SlotName);
            clonedMonster.SetUpForCombat();
            ForceMonsterMove(liveCreature.Monster, clonedMonster);
            if (liveCreature.PetOwner != null)
            {
                clonedCreature.PetOwner = clonedRun.GetPlayer(liveCreature.PetOwner.NetId);
                clonedCreature.PetOwner?.PlayerCombatState?.AddPetInternal(clonedCreature);
            }

            _combatState.AddCreature(clonedCreature);
            CopyCreatureRuntimeState(liveCreature, clonedCreature);
            RegisterCreatureTarget(clonedCreature);
        }

        private static void ForceMonsterMove(MonsterModel liveMonster, MonsterModel clonedMonster)
        {
            string liveMoveId = liveMonster.NextMove?.StateId ?? string.Empty;
            if (string.IsNullOrEmpty(liveMoveId) ||
                clonedMonster.MoveStateMachine == null ||
                !clonedMonster.MoveStateMachine.States.TryGetValue(liveMoveId, out MonsterState? state) ||
                state is not MoveState move)
            {
                return;
            }

            clonedMonster.SetMoveImmediate(move, forceTransition: true);
        }

        private void CopyCreatureRuntimeState(Creature liveCreature, Creature clonedCreature)
        {
            clonedCreature.CombatId = liveCreature.CombatId;
            clonedCreature.SetMaxHpInternal(liveCreature.MaxHp);
            clonedCreature.SetCurrentHpInternal(liveCreature.CurrentHp);
            clonedCreature.LoseBlockInternal(clonedCreature.Block);
            if (liveCreature.Block > 0)
            {
                clonedCreature.GainBlockInternal(liveCreature.Block);
            }

            clonedCreature.RemoveAllPowersInternalExcept();
            foreach (PowerModel livePower in liveCreature.Powers)
            {
                PowerModel clonedPower = ModelDb.GetById<PowerModel>(livePower.Id).ToMutable();
                clonedPower.SkipNextDurationTick = livePower.SkipNextDurationTick;
                clonedPower.ApplyInternal(clonedCreature, livePower.Amount, silent: true);
                clonedPower.AmountOnTurnStart = livePower.AmountOnTurnStart;
            }
        }

        private void CopyCombatPiles(
            Player livePlayer,
            Player clonedPlayer,
            IsolatedCombatSimulation? sourceSimulation)
        {
            if (livePlayer.PlayerCombatState == null || clonedPlayer.PlayerCombatState == null)
            {
                return;
            }

            foreach (CardPile livePile in livePlayer.PlayerCombatState.AllPiles)
            {
                CardPile? clonedPile = CardPile.Get(livePile.Type, clonedPlayer);
                if (clonedPile == null)
                {
                    continue;
                }

                foreach (CardModel liveCard in livePile.Cards)
                {
                    CardModel clonedCard = CloneCombatCard(liveCard, clonedPlayer);
                    clonedPile.AddInternal(clonedCard, silent: true);
                    if (TryGetKnownCombatId(liveCard, sourceSimulation, out uint liveCombatCardId))
                    {
                        _cardsByLiveCombatId[liveCombatCardId] = clonedCard;
                        _knownCombatIdByCard[clonedCard] = liveCombatCardId;
                    }
                }
            }

            clonedPlayer.PlayerCombatState.Energy = livePlayer.PlayerCombatState.Energy;
            clonedPlayer.PlayerCombatState.Stars = livePlayer.PlayerCombatState.Stars;
        }

        private static bool TryGetKnownCombatId(
            CardModel card,
            IsolatedCombatSimulation? sourceSimulation,
            out uint combatCardId)
        {
            if (sourceSimulation != null)
            {
                return sourceSimulation._knownCombatIdByCard.TryGetValue(card, out combatCardId);
            }

            try
            {
                return NetCombatCardDb.Instance.TryGetCardId(card, out combatCardId);
            }
            catch
            {
                combatCardId = 0;
                return false;
            }
        }

        private CardModel CloneCombatCard(CardModel liveCard, Player clonedOwner)
        {
            CardModel clonedCard = CardModel.FromSerializable(liveCard.ToSerializable());
            _combatState.AddCard(clonedCard, clonedOwner);
            CopyCardRuntimeState(liveCard, clonedCard);
            return clonedCard;
        }

        private static void CopyCardRuntimeState(CardModel liveCard, CardModel clonedCard)
        {
            foreach (CardKeyword keyword in liveCard.Keywords)
            {
                if (!clonedCard.Keywords.Contains(keyword))
                {
                    clonedCard.AddKeyword(keyword);
                }
            }

            foreach (CardKeyword keyword in clonedCard.Keywords.ToList())
            {
                if (!liveCard.Keywords.Contains(keyword))
                {
                    clonedCard.RemoveKeyword(keyword);
                }
            }

            if (liveCard.Affliction != null)
            {
                AfflictionModel clonedAffliction = ModelDb.GetById<AfflictionModel>(liveCard.Affliction.Id).ToMutable();
                clonedCard.AfflictInternal(clonedAffliction, liveCard.Affliction.Amount);
            }

            if (!liveCard.EnergyCost.CostsX)
            {
                int liveLocalCost = liveCard.EnergyCost.GetWithModifiers(CostModifiers.Local);
                int clonedLocalCost = clonedCard.EnergyCost.GetWithModifiers(CostModifiers.Local);
                if (liveLocalCost != clonedLocalCost)
                {
                    clonedCard.EnergyCost.SetThisTurnOrUntilPlayed(liveLocalCost);
                }
            }
        }

        private static void CopyOrbs(Player livePlayer, Player clonedPlayer)
        {
            if (livePlayer.PlayerCombatState == null || clonedPlayer.PlayerCombatState == null)
            {
                return;
            }

            OrbQueue liveQueue = livePlayer.PlayerCombatState.OrbQueue;
            OrbQueue clonedQueue = clonedPlayer.PlayerCombatState.OrbQueue;
            clonedQueue.Clear();
            clonedQueue.AddCapacity(liveQueue.Capacity);
            foreach (OrbModel liveOrb in liveQueue.Orbs)
            {
                OrbModel clonedOrb = ModelDb.GetById<OrbModel>(liveOrb.Id).ToMutable();
                clonedOrb.Owner = clonedPlayer;
                clonedQueue.Insert(clonedQueue.Orbs.Count, clonedOrb);
            }
        }

        private static int EstimatePlayerPowerScore(Creature creature)
        {
            int score = 0;
            foreach (PowerModel power in creature.Powers)
            {
                string token = NormalizePowerToken(power.Id.Entry);
                int amount = power.Amount;
                int stacks = Math.Max(1, amount);
                bool temporary = ContainsAny(token, "TEMPORARY", "THIS_TURN", "UNTIL_TURN_END", "UNTIL_END_OF_TURN");

                if (ContainsAny(token, "VULNERABLE"))
                {
                    score -= stacks * 90;
                }
                else if (ContainsAny(token, "WEAK"))
                {
                    score -= stacks * 75;
                }
                else if (ContainsAny(token, "FRAIL"))
                {
                    score -= stacks * 70;
                }
                else if (ContainsAny(token, "POISON"))
                {
                    score -= stacks * 100;
                }
                else if (ContainsAny(token, "ENTANGLED", "NO_BLOCK", "CANNOT_PLAY_ATTACK"))
                {
                    score -= stacks * 160;
                }
                else if (ContainsAny(token, "STRENGTH"))
                {
                    score += amount * (temporary ? 45 : 220);
                }
                else if (ContainsAny(token, "DEXTERITY"))
                {
                    score += amount * (temporary ? 40 : 180);
                }
                else if (ContainsAny(token, "FOCUS"))
                {
                    score += amount * 240;
                }
                else if (ContainsAny(token, "BUFFER"))
                {
                    score += stacks * 460;
                }
                else if (ContainsAny(token, "ARTIFACT"))
                {
                    score += stacks * 260;
                }
                else if (ContainsAny(token, "ECHO_FORM"))
                {
                    score += stacks * 950;
                }
                else if (ContainsAny(token, "DEMON_FORM"))
                {
                    score += stacks * 720;
                }
                else if (ContainsAny(token, "EVOLVE"))
                {
                    score += stacks * 620;
                }
                else if (ContainsAny(token, "DARK_EMBRACE"))
                {
                    score += stacks * 560;
                }
                else if (ContainsAny(token, "BARRICADE"))
                {
                    score += stacks * 520;
                }
                else if (ContainsAny(token, "FEEL_NO_PAIN"))
                {
                    score += stacks * 440;
                }
                else if (ContainsAny(token, "SERPENT_FORM"))
                {
                    score += stacks * 400;
                }
                else if (ContainsAny(token, "ACCURACY"))
                {
                    score += stacks * 360;
                }
                else if (ContainsAny(token, "FIRE_BREATHING"))
                {
                    score += stacks * 340;
                }
                else if (ContainsAny(token, "NOXIOUS_FUMES"))
                {
                    score += stacks * 340;
                }
                else if (ContainsAny(token, "INFINITE_BLADES", "LOOP", "STORM"))
                {
                    score += stacks * 300;
                }
                else if (ContainsAny(token, "COMBUST"))
                {
                    score += stacks * 260;
                }
                else if (ContainsAny(token, "METALLICIZE", "PLATED_ARMOR"))
                {
                    score += stacks * 220;
                }
                else if (ContainsAny(token, "RAGE"))
                {
                    score += stacks * 180;
                }
            }

            return Math.Clamp(score, -3_000, 6_000);
        }

        private static int EstimateEnemyControlScore(Creature creature)
        {
            int score = 0;
            foreach (PowerModel power in creature.Powers)
            {
                string token = NormalizePowerToken(power.Id.Entry);
                int amount = power.Amount;
                int stacks = Math.Max(1, amount);
                if (ContainsAny(token, "VULNERABLE"))
                {
                    score += stacks * 160;
                }
                else if (ContainsAny(token, "WEAK"))
                {
                    score += stacks * 145;
                }
                else if (ContainsAny(token, "POISON"))
                {
                    score += stacks * 105;
                }
                else if (ContainsAny(token, "LOCK_ON", "LOCKON"))
                {
                    score += stacks * 120;
                }
                else if (ContainsAny(token, "TENDER", "SHACKLE", "SHACKLED"))
                {
                    score += stacks * 90;
                }
                else if (ContainsAny(token, "STRENGTH"))
                {
                    score -= amount * 190;
                }
                else if (ContainsAny(token, "DEXTERITY"))
                {
                    score -= amount * 110;
                }
                else if (ContainsAny(token, "RITUAL", "GROWTH", "CURIOSITY", "CULTIST"))
                {
                    score -= stacks * 260;
                }
                else if (ContainsAny(token, "INTANGIBLE", "INCORPOREAL", "WRAITH"))
                {
                    score -= stacks * 550;
                }
                else if (ContainsAny(token, "BUFFER"))
                {
                    score -= stacks * 220;
                }
                else if (ContainsAny(token, "ARTIFACT"))
                {
                    score -= stacks * 170;
                }
                else if (ContainsAny(token, "THORNS", "SHARP_HIDE", "SPIKES"))
                {
                    score -= stacks * 90;
                }
                else if (ContainsAny(token, "REGEN", "REGENERATE"))
                {
                    score -= stacks * 80;
                }
                else if (ContainsAny(token, "BARRICADE"))
                {
                    score -= stacks * 180;
                }
            }

            return Math.Clamp(score, -6_000, 4_500);
        }

        private static string NormalizePowerToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static bool ContainsAny(string token, params string[] needles)
        {
            return needles.Any(needle => token.Contains(needle, StringComparison.Ordinal));
        }

        private void CaptureInitialState()
        {
            foreach (Creature creature in _combatState.Creatures)
            {
                RegisterCreatureTarget(creature);
                string targetId = GetTargetId(creature);
                if (creature.IsEnemy)
                {
                    _initialEnemyHpByTargetId[targetId] = Math.Max(0, creature.CurrentHp);
                    _initialEnemyEffectiveHpByTargetId[targetId] = Math.Max(0, creature.CurrentHp) + Math.Max(0, creature.Block);
                    _initialEnemyControlScoreByTargetId[targetId] = EstimateEnemyControlScore(creature);
                }
                else if (creature.IsPlayer)
                {
                    _initialPlayerHpByTargetId[targetId] = Math.Max(0, creature.CurrentHp);
                    _initialPlayerPowerScoreByTargetId[targetId] = EstimatePlayerPowerScore(creature);
                }
            }
        }

        private void CopyInitialStateFrom(IsolatedCombatSimulation source)
        {
            foreach (KeyValuePair<string, int> pair in source._initialEnemyEffectiveHpByTargetId)
            {
                _initialEnemyEffectiveHpByTargetId[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, int> pair in source._initialEnemyHpByTargetId)
            {
                _initialEnemyHpByTargetId[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, int> pair in source._initialPlayerHpByTargetId)
            {
                _initialPlayerHpByTargetId[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, int> pair in source._initialPlayerPowerScoreByTargetId)
            {
                _initialPlayerPowerScoreByTargetId[pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, int> pair in source._initialEnemyControlScoreByTargetId)
            {
                _initialEnemyControlScoreByTargetId[pair.Key] = pair.Value;
            }
        }

        private void RegisterCreatureTarget(Creature creature)
        {
            _creaturesByTargetId[GetTargetId(creature)] = creature;
        }

        private async Task<bool> TryExecuteCardAsync(SimulationAction action)
        {
            if (!TryResolveCard(action, out CardModel? card) || card == null)
            {
                return false;
            }

            Creature? target = ResolveTarget(action.TargetId);
            if (!card.CanPlay(out _, out _) || !card.IsValidTarget(target))
            {
                return false;
            }

            return await ExecuteOriginalGameActionAsync(new PlayCardAction(card, target));
        }

        private async Task<bool> TryExecutePotionAsync(SimulationAction action)
        {
            PotionModel? potion = ResolvePotion(action);
            if (potion == null)
            {
                return false;
            }

            Creature? target = ResolveTarget(action.TargetId);
            if (potion.TargetType.IsSingleTarget() && target == null)
            {
                return false;
            }

            return await ExecuteOriginalGameActionAsync(new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress));
        }

        private async Task<bool> ExecuteOriginalGameActionAsync(GameAction action)
        {
            using CombatManagerSimulationScope managerScope = new(_combatState);
            using IDisposable presentationScope = AiTeammateSimulationRuntime.SuppressPresentation();
            action.OnEnqueued(static _ => { }, _nextSimulationActionId++);
            Task executionTask = action.Execute();
            Task completedTask = await Task.WhenAny(executionTask, Task.Delay(SimulationActionTimeoutMs));
            if (completedTask != executionTask)
            {
                action.Cancel();
                return false;
            }

            if (executionTask.IsFaulted ||
                action.State is GameActionState.Canceled or GameActionState.GatheringPlayerChoice)
            {
                action.Cancel();
                return false;
            }

            return action.State == GameActionState.Finished;
        }

        private bool TryResolveCard(SimulationAction action, out CardModel? card)
        {
            card = null;
            if (action.KnownCombatCardId.HasValue &&
                _cardsByLiveCombatId.TryGetValue(action.KnownCombatCardId.Value, out card))
            {
                return true;
            }

            if (action.HandIndex.HasValue)
            {
                IReadOnlyList<CardModel> hand = PileType.Hand.GetPile(_actor).Cards;
                int index = action.HandIndex.Value;
                if (index >= 0 &&
                    index < hand.Count &&
                    string.Equals(hand[index].Id.Entry, action.ModelId, StringComparison.Ordinal))
                {
                    card = hand[index];
                    return true;
                }
            }

            return false;
        }

        private PotionModel? ResolvePotion(SimulationAction action)
        {
            string potionId = action.ModelId ?? string.Empty;
            int actionPotionIndex = action.PotionIndex ?? ParsePotionActionIndex(action.ActionId);
            List<PotionModel> matchingPotions = _actor.Potions
                .Where(potion => !potion.IsQueued &&
                                 string.Equals(potion.Id.Entry, potionId, StringComparison.Ordinal))
                .ToList();
            if (actionPotionIndex >= 0 && actionPotionIndex < matchingPotions.Count)
            {
                return matchingPotions[actionPotionIndex];
            }

            return matchingPotions.FirstOrDefault();
        }

        private static int ParsePotionActionIndex(string actionId)
        {
            int targetIndex = actionId.IndexOf("_target_", StringComparison.Ordinal);
            if (targetIndex <= 0)
            {
                return -1;
            }

            int separatorIndex = actionId.LastIndexOf('_', targetIndex - 1);
            if (separatorIndex < 0)
            {
                return -1;
            }

            string indexText = actionId[(separatorIndex + 1)..targetIndex];
            return int.TryParse(indexText, out int index) ? index : -1;
        }

        private Creature? ResolveTarget(string? targetId)
        {
            if (string.IsNullOrEmpty(targetId) ||
                string.Equals(targetId, "none", StringComparison.Ordinal))
            {
                return null;
            }

            return _creaturesByTargetId.TryGetValue(targetId, out Creature? target)
                ? target
                : null;
        }

        private static async Task ExecutePotionWithoutVfxAsync(PotionModel potion, Creature? target)
        {
            CombatState? combatState = potion.Owner.Creature.CombatState;
            BlockingPlayerChoiceContext choiceContext = new();
            potion.RemoveBeforeUse();
            choiceContext.PushModel(potion);
            await Hook.BeforePotionUsed(potion.Owner.RunState, combatState, potion, target);
            await InvokePotionOnUseAsync(potion, choiceContext, target);
            potion.InvokeExecutionFinished();
            if (combatState != null)
            {
                await Hook.AfterPotionUsed(potion.Owner.RunState, combatState, potion, target);
                await CombatManager.Instance.CheckForEmptyHand(choiceContext, potion.Owner);
            }

            choiceContext.PopModel(potion);
        }

        private static async Task InvokePotionOnUseAsync(
            PotionModel potion,
            PlayerChoiceContext choiceContext,
            Creature? target)
        {
            MethodInfo? method = potion.GetType().GetMethod("OnUse", BindingFlags.Instance | BindingFlags.NonPublic) ??
                                 PotionOnUseMethod;
            if (method == null)
            {
                return;
            }

            object? result = method.Invoke(potion, [choiceContext, target]);
            if (result is Task task)
            {
                await task;
            }
        }

        public static bool IsHighValuePotion(SimulationAction action)
        {
            return IsHighValuePotion(action.ModelId);
        }

        private static bool IsHighValuePotion(string? potionId)
        {
            potionId ??= string.Empty;
            return potionId.Contains("FAIRY", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("SMOKE", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("ENTROPIC", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("DUPLIC", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("GHOST", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("STABLE_SERUM", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("FOCUS", StringComparison.OrdinalIgnoreCase) ||
                   potionId.Contains("CULTIST", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPlayCardActionId(string cardInstanceId, Creature? target)
        {
            return $"play_card_{cardInstanceId}_target_{GetTargetId(target)}";
        }

        private static string BuildUsePotionActionId(PotionModel potion, Creature? target, int potionIndex)
        {
            return $"use_potion_{SanitizeActionToken(potion.Id.Entry)}_{potionIndex}_target_{GetTargetId(target)}";
        }

        private static string GetTargetId(Creature? creature)
        {
            if (creature == null)
            {
                return "none";
            }

            if (creature.Player != null)
            {
                return $"player_{creature.Player.NetId}";
            }

            return $"creature_{creature.CombatId?.ToString() ?? "unknown"}";
        }

        private static string SanitizeActionToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            return value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
        }

        private readonly record struct EndTurnDamageProjection(
            int ActorHpLoss,
            int TeamHpLoss,
            int TeamDeaths,
            int ActorDamagePrevented,
            int TeamDamagePrevented);

        private readonly record struct RolloutCardValue(
            CardModel Card,
            string CardId,
            int Priority,
            int EnergyCost,
            int StarCost,
            int EnergyGain,
            int StarsGenerated,
            bool IsNegativeStatus);

        private readonly record struct RollingTurnEvaluation(
            int Score,
            IReadOnlyList<CardModel> PlayedCards);

        private sealed class RolloutDeckState
        {
            public RolloutDeckState(
                List<CardModel> drawPile,
                List<CardModel> discardPile,
                List<CardModel> retainedHand)
            {
                DrawPile = drawPile;
                DiscardPile = discardPile;
                RetainedHand = retainedHand;
            }

            public List<CardModel> DrawPile { get; }

            public List<CardModel> DiscardPile { get; }

            public List<CardModel> RetainedHand { get; }

            public bool HasCards => DrawPile.Count > 0 ||
                                    DiscardPile.Count > 0 ||
                                    RetainedHand.Count > 0;
        }
    }

    private sealed class OriginalActionDeepSearch
    {
        private const int MaxDepth = BattleAiRollingSearchTuning.SameTurnCommandDepthLimit;
        private const int MaxNodes = 160;
        private const int BranchWidth = 8;
        private const int MaxElapsedMs = 70;

        private readonly DeterministicCombatContext _context;
        private readonly NetCombatCardDbSimulationScope _netCardDbScope;
        private readonly int _maxResults;
        private readonly List<CombatResolverSimulationResult> _results = [];
        private readonly Stopwatch _stopwatch = new();
        private int _visitedNodes;
        private bool _wasTruncated;

        public OriginalActionDeepSearch(
            DeterministicCombatContext context,
            NetCombatCardDbSimulationScope netCardDbScope,
            int maxResults)
        {
            _context = context;
            _netCardDbScope = netCardDbScope;
            _maxResults = maxResults;
        }

        public int VisitedNodes => _visitedNodes;

        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        public bool WasTruncated => _wasTruncated;

        public async Task<IReadOnlyList<CombatResolverSimulationResult>> SearchAsync(
            IsolatedCombatSimulation root,
            CancellationToken ct)
        {
            _stopwatch.Restart();
            await ExpandAsync(root, [], 0, 0, ct);
            _stopwatch.Stop();
            return _results
                .OrderByDescending(static result => result.Score)
                .ThenBy(static result => string.Join("|", result.Plan.ActionIds), StringComparer.Ordinal)
                .Take(_maxResults)
                .ToList();
        }

        private async Task ExpandAsync(
            IsolatedCombatSimulation simulation,
            IReadOnlyList<SimulationAction> trace,
            int depth,
            int potionsUsed,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (trace.Count > 0)
            {
                AddResult(simulation, trace, potionsUsed);
            }

            if (_visitedNodes >= MaxNodes ||
                _stopwatch.ElapsedMilliseconds >= MaxElapsedMs ||
                depth >= MaxDepth ||
                !simulation.ActorCanAct() ||
                !simulation.HasAliveEnemies())
            {
                _wasTruncated = _wasTruncated ||
                                _visitedNodes >= MaxNodes ||
                                _stopwatch.ElapsedMilliseconds >= MaxElapsedMs;
                return;
            }

            _netCardDbScope.IndexCombatCards(simulation);
            List<SimulationAction> actions = simulation.BuildLegalActions()
                .OrderByDescending(simulation.EstimateImmediateActionPriority)
                .ThenBy(static action => action.ActionId, StringComparer.Ordinal)
                .Take(BranchWidth)
                .ToList();
            foreach (SimulationAction action in actions)
            {
                if (_visitedNodes >= MaxNodes)
                {
                    _wasTruncated = true;
                    return;
                }

                ct.ThrowIfCancellationRequested();
                if (_stopwatch.ElapsedMilliseconds >= MaxElapsedMs)
                {
                    _wasTruncated = true;
                    return;
                }

                using IsolatedCombatSimulation child = simulation.Fork();
                _netCardDbScope.IndexCombatCards(child);
                _visitedNodes++;
                if (!await child.TryExecuteAsync(action))
                {
                    continue;
                }

                List<SimulationAction> nextTrace = trace.Concat([action]).ToList();
                int nextPotionsUsed = potionsUsed + (action.Kind == SimulationActionKind.UsePotion ? 1 : 0);
                await ExpandAsync(child, nextTrace, depth + 1, nextPotionsUsed, ct);
            }
        }

        private void AddResult(
            IsolatedCombatSimulation simulation,
            IReadOnlyList<SimulationAction> trace,
            int potionsUsed)
        {
            CombatLinePlan plan = new()
            {
                ActionIds = trace.Select(static action => action.ActionId).ToList(),
                Score = 0,
                EstimatedDamageDealt = 0,
                EstimatedDamageTaken = 0,
                EstimatedBlockAfterEnemyTurn = 0
            };
            int highValuePotionsUsed = trace.Count(action => action.Kind == SimulationActionKind.UsePotion && IsolatedCombatSimulation.IsHighValuePotion(action));
            CombatResolverSimulationResult result = simulation.BuildResult(_context, plan, potionsUsed, highValuePotionsUsed);
            _results.Add(result);
            if (_results.Count > _maxResults * 4)
            {
                _results.Sort(static (left, right) => right.Score.CompareTo(left.Score));
                _results.RemoveRange(_maxResults * 2, _results.Count - _maxResults * 2);
            }
        }
    }

    private enum SimulationActionKind
    {
        PlayCard,
        UsePotion
    }

    private sealed class SimulationAction
    {
        public required string ActionId { get; init; }

        public required SimulationActionKind Kind { get; init; }

        public required string? ModelId { get; init; }

        public required string? TargetId { get; init; }

        public uint? KnownCombatCardId { get; init; }

        public int? HandIndex { get; init; }

        public int? PotionIndex { get; init; }

        public static SimulationAction FromLegalAction(AiLegalActionOption action)
        {
            uint? knownCombatCardId = null;
            if (!string.IsNullOrEmpty(action.CardInstanceId) &&
                action.CardInstanceId.StartsWith("combat_", StringComparison.Ordinal) &&
                uint.TryParse(action.CardInstanceId["combat_".Length..], out uint combatCardId))
            {
                knownCombatCardId = combatCardId;
            }

            return new SimulationAction
            {
                ActionId = action.ActionId,
                Kind = string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal)
                    ? SimulationActionKind.UsePotion
                    : SimulationActionKind.PlayCard,
                ModelId = action.CardId,
                TargetId = action.TargetId,
                KnownCombatCardId = knownCombatCardId,
                PotionIndex = string.Equals(action.ActionType, AiTeammateActionKind.UsePotion.ToString(), StringComparison.Ordinal)
                    ? ParsePotionActionIndexFromActionId(action.ActionId)
                    : null
            };
        }

        private static int ParsePotionActionIndexFromActionId(string actionId)
        {
            int targetIndex = actionId.IndexOf("_target_", StringComparison.Ordinal);
            if (targetIndex <= 0)
            {
                return -1;
            }

            int separatorIndex = actionId.LastIndexOf('_', targetIndex - 1);
            if (separatorIndex < 0)
            {
                return -1;
            }

            string indexText = actionId[(separatorIndex + 1)..targetIndex];
            return int.TryParse(indexText, out int index) ? index : -1;
        }
    }

    private sealed class NetCombatCardDbSimulationScope : IDisposable
    {
        private readonly uint _previousNextId;
        private readonly Dictionary<uint, CardModel> _previousIdToCard;
        private readonly Dictionary<CardModel, uint> _previousCardToId;
        private bool _disposed;

        public NetCombatCardDbSimulationScope()
        {
            _previousNextId = NetCombatCardDbNextIdField?.GetValue(NetCombatCardDb.Instance) is uint nextId ? nextId : 0u;
            _previousIdToCard = CloneDictionary<uint, CardModel>(NetCombatCardDbIdToCardField?.GetValue(NetCombatCardDb.Instance));
            _previousCardToId = CloneDictionary<CardModel, uint>(NetCombatCardDbCardToIdField?.GetValue(NetCombatCardDb.Instance));
            NetCombatCardDb.Instance.ClearCardsForTesting();
        }

        public void IndexCombatCards(IsolatedCombatSimulation simulation)
        {
            foreach (Player player in simulation.CombatState.Players)
            {
                if (player.PlayerCombatState == null)
                {
                    continue;
                }

                foreach (CardModel card in player.PlayerCombatState.AllPiles.SelectMany(static pile => pile.Cards))
                {
                    try
                    {
                        NetCombatCardDb.Instance.IdCardForTesting(card);
                    }
                    catch
                    {
                        // Simulation indexing is best-effort; unindexed branches are discarded when the action cannot resolve.
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            NetCombatCardDbNextIdField?.SetValue(NetCombatCardDb.Instance, _previousNextId);
            NetCombatCardDbIdToCardField?.SetValue(NetCombatCardDb.Instance, _previousIdToCard);
            NetCombatCardDbCardToIdField?.SetValue(NetCombatCardDb.Instance, _previousCardToId);
            _disposed = true;
        }

        private static Dictionary<TKey, TValue> CloneDictionary<TKey, TValue>(object? value)
            where TKey : notnull
        {
            return value is Dictionary<TKey, TValue> dictionary
                ? new Dictionary<TKey, TValue>(dictionary)
                : [];
        }
    }

    private sealed class CombatManagerSimulationScope : IDisposable
    {
        private readonly CombatState? _previousState;
        private readonly object? _previousHistoryChanged;
        private readonly object? _previousStateTrackerChanged;
        private readonly bool _previousTestMode;
        private bool _disposed;

        public CombatManagerSimulationScope(CombatState simulationState)
        {
            _previousState = CombatManager.Instance.DebugOnlyGetState();
            _previousHistoryChanged = CombatHistoryChangedField?.GetValue(CombatManager.Instance.History);
            _previousStateTrackerChanged = CombatStateTrackerChangedField?.GetValue(CombatManager.Instance.StateTracker);
            _previousTestMode = TestMode.IsOn;
            TestMode.IsOn = true;
            CombatHistoryChangedField?.SetValue(CombatManager.Instance.History, null);
            CombatStateTrackerChangedField?.SetValue(CombatManager.Instance.StateTracker, null);
            CombatManagerStateField?.SetValue(CombatManager.Instance, simulationState);
            CombatManager.Instance.StateTracker.SetState(simulationState);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CombatManagerStateField?.SetValue(CombatManager.Instance, _previousState);
            CombatHistoryChangedField?.SetValue(CombatManager.Instance.History, _previousHistoryChanged);
            CombatStateTrackerChangedField?.SetValue(CombatManager.Instance.StateTracker, _previousStateTrackerChanged);
            TestMode.IsOn = _previousTestMode;
            if (_previousState != null)
            {
                CombatManager.Instance.StateTracker.SetState(_previousState);
            }

            _disposed = true;
        }
    }
}
