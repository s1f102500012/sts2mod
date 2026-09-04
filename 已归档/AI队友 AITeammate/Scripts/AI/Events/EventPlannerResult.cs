using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class EventPlannerResult
{
    public required IReadOnlyList<EventOptionEvaluation> RankedOptions { get; init; }

    public required EventOptionEvaluation BestOption { get; init; }

    public EventOptionEvaluation? BaselineOption { get; init; }

    public required int SupportedOptionCount { get; init; }

    public required int FullyNormalizedOptionCount { get; init; }

    public required int HighTrustOptionCount { get; init; }

    public required bool IsBestOptionSafeForPlannerSelectionLater { get; init; }

    public required EventPlannerTrustLevel OverallTrustLevel { get; init; }

    public required string CoverageSummary { get; init; }

    public string Describe()
    {
        return $"best={BestOption.TextKey} score={BestOption.TotalScore:F1} baseline={BaselineOption?.TextKey ?? "none"} baselineScore={(BaselineOption?.TotalScore.ToString("F1") ?? "n/a")} supported={SupportedOptionCount}/{RankedOptions.Count} fullyNormalized={FullyNormalizedOptionCount}/{RankedOptions.Count} highTrust={HighTrustOptionCount}/{RankedOptions.Count} overallTrust={OverallTrustLevel} bestSafeLater={IsBestOptionSafeForPlannerSelectionLater} coverage=\"{CoverageSummary}\"";
    }
}
