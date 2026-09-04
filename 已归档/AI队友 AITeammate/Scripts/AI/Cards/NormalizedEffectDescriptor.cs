using System;

namespace AITeammate.Scripts;

internal readonly record struct EffectAdjustmentKey(EffectKind Kind, string? AppliedPowerId = null);

internal sealed class NormalizedEffectDescriptor
{
    public required EffectKind Kind { get; init; }

    public required TargetScope TargetScope { get; init; }

    public int Amount { get; init; }

    public int RepeatCount { get; init; } = 1;

    public string? AppliedPowerId { get; init; }

    public DurationHint DurationHint { get; init; } = DurationHint.Unknown;

    public ValueTiming ValueTiming { get; init; } = ValueTiming.Immediate;

    public string Describe()
    {
        return Kind switch
        {
            EffectKind.ApplyPower when !string.IsNullOrEmpty(AppliedPowerId) =>
                $"{Kind}:{AppliedPowerId}:{Amount}:{DurationHint}",
            _ when !string.IsNullOrEmpty(AppliedPowerId) =>
                $"{Kind}:{AppliedPowerId}:{Amount}:{DurationHint}",
            _ when RepeatCount > 1 =>
                $"{Kind}:{Amount}x{RepeatCount}:{DurationHint}",
            _ => $"{Kind}:{Amount}:{DurationHint}"
        };
    }
}
