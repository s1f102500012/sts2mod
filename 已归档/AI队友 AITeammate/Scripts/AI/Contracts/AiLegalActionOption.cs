using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class AiLegalActionOption
{
    public required string ActionId { get; init; }

    public required string ActionType { get; init; }

    public required string Description { get; init; }

    public string? Label { get; init; }

    public string? Summary { get; init; }

    public string? CardId { get; init; }

    public string? CardInstanceId { get; init; }

    public string? TargetId { get; init; }

    public string? TargetLabel { get; init; }

    public int? EnergyCost { get; init; }

    public List<string>? PriorityTags { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}
