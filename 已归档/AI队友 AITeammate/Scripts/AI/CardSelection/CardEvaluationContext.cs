using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;

namespace AITeammate.Scripts;

internal sealed class CardEvaluationContext
{
    public required Player Player { get; init; }

    public required CardChoiceSource ChoiceSource { get; init; }

    public required IReadOnlyList<ResolvedCardView> DeckCards { get; init; }

    public required DeckSummary DeckSummary { get; init; }

    public required IReadOnlySet<string> RelicIds { get; init; }

    public required IReadOnlySet<string> ModifierIds { get; init; }

    public bool SkipAllowed { get; init; }

    public int Gold { get; init; }

    public int AscensionLevel { get; init; }

    public int CurrentActIndex { get; init; }

    public int ActFloor { get; init; }

    public int TotalFloor { get; init; }

    public int? CandidateGoldCost { get; init; }

    public FutureRewardRouteEvaluation? FutureRewards { get; init; }

    public string? DebugSource { get; init; }
}
