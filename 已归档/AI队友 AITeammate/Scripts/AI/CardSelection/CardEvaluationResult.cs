using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class CardEvaluationResult
{
    public required CardModel CandidateCard { get; init; }

    public required ResolvedCardView Candidate { get; init; }

    public double FinalScore { get; init; }

    public double IntrinsicScore { get; init; }

    public double DeckFitScore { get; init; }

    public double NeedCoverageScore { get; init; }

    public double RedundancyPenalty { get; init; }

    public double ContextAdjustmentScore { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string Describe()
    {
        return $"{Candidate.CardId} score={FinalScore:F1} intrinsic={IntrinsicScore:F1} fit={DeckFitScore:F1} needs={NeedCoverageScore:F1} redundancy={RedundancyPenalty:F1} context={ContextAdjustmentScore:F1}";
    }
}
