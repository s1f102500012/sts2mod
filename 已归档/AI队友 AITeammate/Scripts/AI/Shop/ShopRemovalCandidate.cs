using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class ShopRemovalCandidate
{
    public required string CardId { get; init; }

    public required string Name { get; init; }

    public required double BurdenScore { get; init; }

    public required ShopDeckCard DeckCard { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];

    public string Describe()
    {
        return $"card={CardId} name={Name} burden={BurdenScore:F1} reasons=[{string.Join("; ", Reasons)}]";
    }
}
