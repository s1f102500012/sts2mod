using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CombatLinePlan
{
    public required IReadOnlyList<string> ActionIds { get; init; }

    public required int Score { get; init; }

    public required int EstimatedDamageDealt { get; init; }

    public required int EstimatedDamageTaken { get; init; }

    public required int EstimatedBlockAfterEnemyTurn { get; init; }

    public string FirstActionId => ActionIds[0];
}
