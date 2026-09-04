using System;

namespace AITeammate.Scripts;

internal sealed class DollRoomEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(DollRoomEventHandler);

    protected override string EventTypeName => "DollRoom";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".INITIAL.options.RANDOM", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DollRoom", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.GainRelic, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                RelicIds = ["DAUGHTER_OF_THE_WIND", "MR_STRUGGLES", "BING_BONG"],
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["gain one random relic from the three doll choices"]
            }, ["exact relic outcome is random"]);
        }

        if (option.TextKey.Contains(".INITIAL.options.TAKE_SOME_TIME", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DollRoom", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.LoseHp, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                HpDelta = -5,
                HasUnknownEffects = true,
                Notes = ["take hp loss, then reveal a two-relic choice subset"]
            }, ["follow-up page reveals only a subset of relic choices"]);
        }

        if (option.TextKey.Contains(".INITIAL.options.EXAMINE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DollRoom", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.LoseHp, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                HpDelta = -15,
                HasUnknownEffects = true,
                Notes = ["take hp loss, then reveal all three relic choices"]
            }, ["follow-up page still requires a second choice"]);
        }

        if (!string.IsNullOrEmpty(option.RelicId))
        {
            return WithKnownOutcome(option, HandlerName, "special:DollRoom", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainRelic], new EventOutcomeSummary
            {
                RelicIds = [option.RelicId],
                Notes = ["follow-up page exact relic choice"]
            });
        }

        return option;
    }
}
