using System;

namespace AITeammate.Scripts;

internal sealed class ZenWeaverEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(ZenWeaverEventHandler);

    protected override string EventTypeName => "ZenWeaver";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".BREATHING_TECHNIQUES", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:ZenWeaver", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.AddFixedCard], new EventOutcomeSummary
            {
                GoldDelta = -50,
                FixedCardIds = ["ENLIGHTENMENT", "ENLIGHTENMENT"],
                Notes = ["buy two Enlightenment cards"]
            });
        }

        if (option.TextKey.Contains(".EMOTIONAL_AWARENESS", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:ZenWeaver", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.RemoveCard], new EventOutcomeSummary
            {
                GoldDelta = -125,
                RemoveCount = 1,
                Notes = ["pay gold to remove one card"]
            });
        }

        if (option.TextKey.Contains(".ARACHNID_ACUPUNCTURE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:ZenWeaver", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.RemoveCard], new EventOutcomeSummary
            {
                GoldDelta = -250,
                RemoveCount = 2,
                Notes = ["pay gold to remove two cards"]
            });
        }

        return option;
    }
}
