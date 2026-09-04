namespace AITeammate.Scripts;

internal sealed class CardFlags
{
    public bool Exhaust { get; init; }

    public bool Ethereal { get; init; }

    public bool Retain { get; init; }

    public bool Innate { get; init; }

    public bool Unplayable { get; init; }

    public int ReplayCount { get; init; }
}
