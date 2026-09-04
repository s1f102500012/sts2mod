namespace AITeammate.Scripts;

internal sealed class DeckSummary
{
    public int CardCount { get; init; }

    public int UpgradedCardCount { get; init; }

    public int AttackCount { get; init; }

    public int SkillCount { get; init; }

    public int PowerCount { get; init; }

    public int FrontloadDamageSources { get; init; }

    public int QualityDamageSources { get; init; }

    public int BlockSources { get; init; }

    public int QualityDefenseSources { get; init; }

    public int DrawSources { get; init; }

    public int EnergySources { get; init; }

    public int VulnerableSources { get; init; }

    public int WeakSources { get; init; }

    public int ScalingSources { get; init; }

    public int AoESources { get; init; }

    public int BadCards { get; init; }

    public int ControlledHandCleanupCards { get; init; }

    public int StatusHandlingCards { get; init; }

    public int BasicCards { get; init; }

    public int StrikeCards { get; init; }

    public int DefendCards { get; init; }

    public int ExhaustPayoffCards { get; init; }

    public int RetainCards { get; init; }

    public int ExhaustCards { get; init; }

    public int ZeroCostCards { get; init; }

    public int HighCostCards { get; init; }

    public int EngineCards { get; init; }

    public int OrbCards { get; init; }

    public int FocusCards { get; init; }

    public int OrbSlotCards { get; init; }

    public int PowerPayoffCards { get; init; }

    public int RecursionCards { get; init; }

    public double AverageCost { get; init; }

    public double AverageDamage { get; init; }

    public double AverageBlock { get; init; }

    public bool HasDrawEnergyEngine => DrawSources >= 2 && EnergySources >= 2;

    public bool HasOrbEngine => OrbCards >= 2 || FocusCards > 0 || OrbSlotCards > 0;

    public bool HasPowerEngine => PowerCount >= 3 || PowerPayoffCards > 0;
}
