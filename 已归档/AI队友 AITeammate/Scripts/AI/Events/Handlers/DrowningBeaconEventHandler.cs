using System;

namespace AITeammate.Scripts;

internal sealed class DrowningBeaconEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(DrowningBeaconEventHandler);

    protected override string EventTypeName => "DrowningBeacon";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".BOTTLE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DrowningBeacon", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainPotion], new EventOutcomeSummary
            {
                PotionRewardCount = 1,
                PotionIds = ["GLOWWATER_POTION"],
                Notes = ["custom offered potion reward"]
            });
        }

        if (option.TextKey.Contains(".CLIMB", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DrowningBeacon", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.LoseMaxHp, EventOptionKind.GainRelic], new EventOutcomeSummary
            {
                MaxHpDelta = -13,
                RelicIds = ["FRESNEL_LENS"],
                Notes = ["lose max hp for relic"]
            });
        }

        return option;
    }
}
