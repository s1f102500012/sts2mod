namespace AITeammate.Scripts;

internal sealed class CombatActionScore
{
    public required string ActionId { get; init; }

    public required CombatActionCategory Category { get; init; }

    public required int TotalScore { get; init; }
}
