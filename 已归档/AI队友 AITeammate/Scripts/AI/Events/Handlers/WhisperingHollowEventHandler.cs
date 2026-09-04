using System;

namespace AITeammate.Scripts;

internal sealed class WhisperingHollowEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(WhisperingHollowEventHandler);

    protected override string EventTypeName => "WhisperingHollow";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".GOLD", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WhisperingHollow", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.GainPotion, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                GoldDelta = -50,
                PotionRewardCount = 2,
                HasRandomness = true,
                Notes = ["spend gold for two potion rewards"]
            }, ["potion identities are runtime-random"]);
        }

        if (option.TextKey.Contains(".HUG", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WhisperingHollow", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.TransformCard, EventOptionKind.LoseHp], new EventOutcomeSummary
            {
                TransformCount = 1,
                HpDelta = -9,
                Notes = ["transform one card and take hp loss"]
            });
        }

        return option;
    }
}
