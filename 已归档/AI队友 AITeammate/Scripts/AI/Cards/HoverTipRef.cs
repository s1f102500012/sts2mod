namespace AITeammate.Scripts;

internal sealed class HoverTipRef
{
    public required HoverTipRefKind Kind { get; init; }

    public required string RefId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }
}
