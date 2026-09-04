using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class EventOptionDescriptor
{
    public required int OptionIndex { get; init; }

    public required string TextKey { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required bool IsLocked { get; init; }

    public required bool IsProceed { get; init; }

    public required bool IsLikelyLeaveOrExit { get; init; }

    public required bool WillKillPlayer { get; init; }

    public required bool IsFullyNormalized { get; init; }

    public required string NormalizationSource { get; init; }

    public required string HandlerName { get; init; }

    public required EventSupportLevel SupportLevel { get; init; }

    public required EventPlannerTrustLevel TrustLevel { get; init; }

    public required bool IsSafeForPlannerSelectionLater { get; init; }

    public required EventRuntimeLocator RuntimeLocator { get; init; }

    public string? RelicId { get; init; }

    public string? RelicName { get; init; }

    public IReadOnlyList<string> HoverTipKinds { get; init; } = [];

    public IReadOnlyList<EventOptionKind> Kinds { get; init; } = [];

    public IReadOnlyList<string> UnknownReasons { get; init; } = [];

    public required EventOutcomeSummary Outcome { get; init; }

    public string Describe()
    {
        return $"index={OptionIndex} textKey={TextKey} title=\"{Title}\" locked={IsLocked} proceed={IsProceed} leaveLike={IsLikelyLeaveOrExit} killRisk={WillKillPlayer} kinds=[{string.Join(", ", Kinds)}] source={NormalizationSource} handler={HandlerName} support={SupportLevel} trust={TrustLevel} safeLater={IsSafeForPlannerSelectionLater} fullyNormalized={IsFullyNormalized} relic={RelicId ?? "none"} hoverTips=[{string.Join(", ", HoverTipKinds)}] unknownReasons=[{string.Join("; ", UnknownReasons)}] outcome=({Outcome.Describe()})";
    }
}
