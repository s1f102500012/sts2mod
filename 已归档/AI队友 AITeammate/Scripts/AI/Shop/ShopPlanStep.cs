using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class ShopPlanStep
{
    public required string ActionId { get; init; }

    public required ShopActionKind Kind { get; init; }

    public required string Description { get; init; }

    public required double ScoreContribution { get; init; }

    public required int GoldBefore { get; init; }

    public required int GoldAfter { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string Describe()
    {
        return $"action={ActionId} kind={Kind} score={ScoreContribution:F1} gold={GoldBefore}->{GoldAfter} desc=\"{Description}\" reasons=[{string.Join("; ", Reasons)}]";
    }
}
