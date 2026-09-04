using System;

namespace AITeammate.Scripts;

internal sealed class WoodCarvingsEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(WoodCarvingsEventHandler);

    protected override string EventTypeName => "WoodCarvings";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".BIRD", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WoodCarvings", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.TransformCard, EventOptionKind.AddFixedCard], new EventOutcomeSummary
            {
                TransformCount = 1,
                FixedCardIds = ["PECK"],
                Notes = ["transform a basic card into Peck"]
            });
        }

        if (option.TextKey.Contains(".SNAKE", StringComparison.Ordinal) && !option.IsLocked)
        {
            return WithKnownOutcome(option, HandlerName, "special:WoodCarvings", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.EnchantCard], new EventOutcomeSummary
            {
                EnchantCount = 1,
                Notes = ["apply Slither enchantment to one card"]
            });
        }

        if (option.TextKey.Contains(".TORUS", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WoodCarvings", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.TransformCard, EventOptionKind.AddFixedCard], new EventOutcomeSummary
            {
                TransformCount = 1,
                FixedCardIds = ["TORIC_TOUGHNESS"],
                Notes = ["transform a basic card into Toric Toughness"]
            });
        }

        return option;
    }
}
