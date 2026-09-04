using MegaCrit.Sts2.Core.Models;

namespace AITeammate.Scripts;

internal sealed class ShopDeckCard
{
    public required string CardId { get; init; }

    public required string Name { get; init; }

    public required bool IsRemovable { get; init; }

    public required ResolvedCardView ResolvedCard { get; init; }

    public CardModel? RuntimeCard { get; init; }
}
