using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class ShopActionEvaluation
{
    public required string ActionId { get; init; }

    public required ShopActionKind Kind { get; init; }

    public required string Description { get; init; }

    public required double ImmediateScore { get; init; }

    public required bool IsLegalNow { get; init; }

    public required bool IsConsideredByPlanner { get; init; }

    public ShopOfferEvaluation? OfferEvaluation { get; init; }

    public ShopRemovalCandidate? RemovalCandidate { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string Describe()
    {
        return $"action={ActionId} kind={Kind} score={ImmediateScore:F1} legal={IsLegalNow} considered={IsConsideredByPlanner} reasons=[{string.Join("; ", Reasons)}]";
    }
}
