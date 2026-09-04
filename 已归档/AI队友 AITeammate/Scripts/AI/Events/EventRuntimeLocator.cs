namespace AITeammate.Scripts;

internal sealed class EventRuntimeLocator
{
    public required string LocatorId { get; init; }

    public required int OptionIndex { get; init; }

    public required string TextKey { get; init; }

    public string Describe()
    {
        return $"locator={LocatorId} optionIndex={OptionIndex} textKey={TextKey}";
    }
}
