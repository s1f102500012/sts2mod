using System;

namespace AITeammate.Scripts;

internal sealed class SunkenStatueEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(SunkenStatueEventHandler);

    protected override string EventTypeName => "SunkenStatue";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".DIVE_INTO_WATER", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:SunkenStatue", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.LoseHp, EventOptionKind.GainGold], new EventOutcomeSummary
            {
                GoldDelta = 111,
                HpDelta = -7,
                Notes = ["preferred stone sword branch: lose hp and gain gold"]
            });
        }

        if (option.TextKey.Contains(".GRAB_SWORD", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:SunkenStatue", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainRelic], new EventOutcomeSummary
            {
                RelicIds = ["SWORD_OF_STONE"],
                Notes = ["take Sword of Stone relic"]
            });
        }

        return option;
    }
}
