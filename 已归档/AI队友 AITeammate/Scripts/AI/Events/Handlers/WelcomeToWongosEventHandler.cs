using System;

namespace AITeammate.Scripts;

internal sealed class WelcomeToWongosEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(WelcomeToWongosEventHandler);

    protected override string EventTypeName => "WelcomeToWongos";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".BARGAIN_BIN", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WelcomeToWongos", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.GainRelic, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                GoldDelta = -100,
                RelicIds = ["COMMON_RANDOM_RELIC"],
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["buy random common relic"]
            }, ["relic identity comes from runtime relic factory"]);
        }

        if (option.TextKey.Contains(".FEATURED_ITEM", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WelcomeToWongos", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.GainRelic], new EventOutcomeSummary
            {
                GoldDelta = -200,
                RelicIds = option.RelicId != null ? [option.RelicId] : ["FEATURED_ITEM_RELIC"],
                Notes = ["buy displayed featured relic"]
            });
        }

        if (option.TextKey.Contains(".MYSTERY_BOX", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WelcomeToWongos", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.GainRelic], new EventOutcomeSummary
            {
                GoldDelta = -300,
                RelicIds = ["WONGOS_MYSTERY_TICKET"],
                Notes = ["buy mystery ticket relic"]
            });
        }

        if (option.TextKey.Contains(".LEAVE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:WelcomeToWongos", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.Leave, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                LeaveLike = true,
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["leave can downgrade a random upgraded card"]
            }, ["leave branch is not a pure safe exit because it can downgrade a random upgraded card"]);
        }

        return option;
    }
}
