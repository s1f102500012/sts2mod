using System;

namespace AITeammate.Scripts;

internal sealed class WellspringEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(WellspringEventHandler);

    protected override string EventTypeName => "Wellspring";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".BOTTLE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Wellspring", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.GainPotion, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                PotionRewardCount = 1,
                HasRandomness = true,
                Notes = ["random potion reward"]
            }, ["potion identity is runtime-random"]);
        }

        if (option.TextKey.Contains(".BATHE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Wellspring", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.RemoveCard, EventOptionKind.AddCurse], new EventOutcomeSummary
            {
                RemoveCount = 1,
                CurseCardIds = ["GUILTY"],
                Notes = ["remove one card and add Guilty"]
            });
        }

        return option;
    }
}
