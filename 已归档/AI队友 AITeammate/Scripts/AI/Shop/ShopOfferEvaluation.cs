using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class ShopOfferEvaluation
{
    public required string OfferId { get; init; }

    public required ShopOfferKind Kind { get; init; }

    public required string Name { get; init; }

    public required double TotalScore { get; init; }

    public required bool IsAffordable { get; init; }

    public required bool IsLegalNow { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string Describe()
    {
        return $"offer={OfferId} kind={Kind} name={Name} score={TotalScore:F1} affordable={IsAffordable} legalNow={IsLegalNow} reasons=[{string.Join("; ", Reasons)}]";
    }
}
