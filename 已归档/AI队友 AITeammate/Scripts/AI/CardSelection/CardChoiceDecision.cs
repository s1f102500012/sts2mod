using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CardChoiceDecision
{
    public required IReadOnlyList<CardEvaluationResult> RankedResults { get; init; }

    public required double SkipThreshold { get; init; }

    public required bool ShouldTakeCard { get; init; }

    public CardEvaluationResult? BestEvaluation => RankedResults.Count > 0 ? RankedResults[0] : null;

    public string Describe()
    {
        string best = BestEvaluation?.Describe() ?? "none";
        return $"shouldTake={ShouldTakeCard} threshold={SkipThreshold:F1} best={best}";
    }
}
