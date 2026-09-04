namespace AITeammate.Scripts;

internal sealed class ShopAction
{
    public required string ActionId { get; init; }

    public required ShopActionKind Kind { get; init; }

    public required string Description { get; init; }

    public required bool IsCurrentlyLegal { get; init; }

    public int? GoldCost { get; init; }

    public string? OfferId { get; init; }

    public string? ModelId { get; init; }

    public bool RequiresInventoryOpen { get; init; }

    public bool EndsVisit { get; init; }

    public ShopRuntimeLocator? RuntimeLocator { get; init; }

    public string Describe()
    {
        return $"kind={Kind} id={ActionId} legal={IsCurrentlyLegal} cost={GoldCost?.ToString() ?? "n/a"} offer={OfferId ?? "n/a"} model={ModelId ?? "n/a"} requiresInventoryOpen={RequiresInventoryOpen} endsVisit={EndsVisit} desc=\"{Description}\"";
    }
}
