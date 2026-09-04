using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;

namespace AITeammate.Scripts;

internal sealed class DeterministicCombatDecisionBackend : IAiDecisionBackend
{
    private readonly DeterministicCombatContextBuilder _contextBuilder = new();
    private readonly CombatActionScorer _scorer = new();
    private readonly CombatResolverSimulationRefiner _simulationRefiner = new();
    private readonly CombatTurnLinePlanner _linePlanner;
    private readonly IAiDecisionBackend _fallbackBackend;

    public DeterministicCombatDecisionBackend(IAiDecisionBackend fallbackBackend)
    {
        _fallbackBackend = fallbackBackend;
        _linePlanner = new CombatTurnLinePlanner(_scorer);
    }

    public async Task<AiDecisionResult> DecideAsync(AiDecisionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        DeterministicCombatContext? context = _contextBuilder.Build(request.ActorId, request.LegalActions);
        if (context == null)
        {
            return await _fallbackBackend.DecideAsync(request, ct);
        }

        IReadOnlyList<CombatLinePlan> candidatePlans = _linePlanner.BuildCandidatePlans(context, maxPlans: 12);
        CombatLinePlan? bestPlan = candidatePlans.FirstOrDefault();
        CombatLinePlan? simulatedPlan = await _simulationRefiner.TryChooseBestPlanAsync(context, candidatePlans, ct);
        if (simulatedPlan != null)
        {
            bestPlan = simulatedPlan;
        }

        List<CombatActionScore> scoredActions = request.LegalActions
            .Select(action => _scorer.Score(context, action))
            .OrderByDescending(static score => score.TotalScore)
            .ThenBy(static score => score.ActionId, StringComparer.Ordinal)
            .ToList();

        if (simulatedPlan == null &&
            ShouldPreferTopScoredActionOverDefensiveLine(context, bestPlan, scoredActions))
        {
            CombatActionScore top = scoredActions[0];
            Log.Info($"[AITeammate] Ignoring over-defensive combat line actor={request.ActorId} line=[{string.Join(", ", bestPlan!.ActionIds)}] lineScore={bestPlan.Score} topAction={top.ActionId} topScore={top.TotalScore}");
            bestPlan = null;
        }

        foreach (CombatActionScore score in scoredActions)
        {
            Log.Info($"[AITeammate] Combat score actor={request.ActorId} actionId={score.ActionId} category={score.Category} total={score.TotalScore}");
        }

        CombatActionScore chosen = bestPlan != null
            ? scoredActions.First(score => string.Equals(score.ActionId, bestPlan.FirstActionId, StringComparison.Ordinal))
            : scoredActions.First();
        string reason = bestPlan != null
            ? $"{(simulatedPlan != null ? "Resolver-sim refined" : "Deterministic combat line")} chose {chosen.Category} with score {chosen.TotalScore}. line=[{string.Join(", ", bestPlan.ActionIds)}] lineScore={bestPlan.Score} estDamage={bestPlan.EstimatedDamageDealt} estTaken={bestPlan.EstimatedDamageTaken} estRetainedBlock={bestPlan.EstimatedBlockAfterEnemyTurn} team={context.TeamTactics.Describe()}."
            : $"Deterministic combat score chose {chosen.Category} with score {chosen.TotalScore}. team={context.TeamTactics.Describe()}.";
        if (bestPlan != null)
        {
            Log.Info($"[AITeammate] Combat line actor={request.ActorId} pressure={context.SustainedAttackPressure} team={context.TeamTactics.Describe()} actions=[{string.Join(", ", bestPlan.ActionIds)}] score={bestPlan.Score} estDamage={bestPlan.EstimatedDamageDealt} estTaken={bestPlan.EstimatedDamageTaken} estRetainedBlock={bestPlan.EstimatedBlockAfterEnemyTurn}");
        }

        return new AiDecisionResult
        {
            ChosenActionId = chosen.ActionId,
            RankedActionIds = scoredActions.Select(static score => score.ActionId).ToList(),
            Reason = reason
        };
    }

    private static bool ShouldPreferTopScoredActionOverDefensiveLine(
        DeterministicCombatContext context,
        CombatLinePlan? bestPlan,
        IReadOnlyList<CombatActionScore> scoredActions)
    {
        if (bestPlan == null ||
            bestPlan.EstimatedDamageDealt > 0 ||
            bestPlan.ActionIds.Count == 0 ||
            scoredActions.Count == 0 ||
            context.IsWaterfallSelfDestructDefenseWindow ||
            context.IsLagavulinMatriarchOpeningSetupWindow)
        {
            return false;
        }

        CombatActionScore top = scoredActions[0];
        if (top.Category != CombatActionCategory.Attack)
        {
            return false;
        }

        CombatActionScore? lineFirst = scoredActions.FirstOrDefault(score =>
            string.Equals(score.ActionId, bestPlan.FirstActionId, StringComparison.Ordinal));
        if (lineFirst == null)
        {
            return false;
        }

        return top.TotalScore >= lineFirst.TotalScore + 120;
    }
}
