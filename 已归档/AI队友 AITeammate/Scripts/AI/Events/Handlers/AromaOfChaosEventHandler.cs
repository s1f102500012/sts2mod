using System;

namespace AITeammate.Scripts;

internal sealed class AromaOfChaosEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(AromaOfChaosEventHandler);

    protected override string EventTypeName => "AromaOfChaos";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".LET_GO", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:AromaOfChaos", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.TransformCard], new EventOutcomeSummary
            {
                TransformCount = 1,
                Notes = ["select 1 deck card to transform"]
            });
        }

        if (option.TextKey.Contains(".MAINTAIN_CONTROL", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:AromaOfChaos", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.UpgradeCard], new EventOutcomeSummary
            {
                UpgradeCount = 1,
                Notes = ["select 1 deck card to upgrade"]
            });
        }

        return option;
    }
}
