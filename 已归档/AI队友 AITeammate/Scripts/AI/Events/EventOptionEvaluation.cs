using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class EventOptionEvaluation
{
    public required int OptionIndex { get; init; }

    public required string TextKey { get; init; }

    public required string Title { get; init; }

    public required double TotalScore { get; init; }

    public required bool IsBaselineOption { get; init; }

    public required bool IsSupported { get; init; }

    public required bool IsFullyNormalized { get; init; }

    public required EventSupportLevel SupportLevel { get; init; }

    public required EventPlannerTrustLevel TrustLevel { get; init; }

    public required bool IsSafeForPlannerSelectionLater { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public required EventOptionDescriptor Option { get; init; }

    public string Describe()
    {
        return $"index={OptionIndex} textKey={TextKey} title=\"{Title}\" score={TotalScore:F1} baseline={IsBaselineOption} supported={IsSupported} fullyNormalized={IsFullyNormalized} support={SupportLevel} trust={TrustLevel} safeLater={IsSafeForPlannerSelectionLater} reasons=[{string.Join("; ", Reasons)}]";
    }
}
