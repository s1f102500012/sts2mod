using System;

namespace AITeammate.Scripts;

internal sealed class TrialEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(TrialEventHandler);

    protected override string EventTypeName => "Trial";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".INITIAL.options.ACCEPT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.Randomized, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["advance into one of three random trial branches"]
            }, ["accept branches into merchant, noble, or nondescript page"]);
        }

        if (option.TextKey.Contains(".INITIAL.options.REJECT", StringComparison.Ordinal) ||
            option.TextKey.Contains(".REJECT.options.ACCEPT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                HasUnknownEffects = true,
                Notes = ["reject path only moves to another trial decision page"]
            }, ["follow-up page still requires another choice"]);
        }

        if (option.TextKey.Contains(".DOUBLE_DOWN", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.Proceed], new EventOutcomeSummary
            {
                ProceedLike = true,
                HasUnknownEffects = true,
                Notes = ["opens abandon run confirm popup instead of normal event resolution"]
            }, ["double down is UI/modal driven and intentionally not trusted for later planner execution"]);
        }

        if (option.TextKey.Contains(".MERCHANT.options.GUILTY", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.AddCurse, EventOptionKind.GainRelic, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                CurseCardIds = ["REGRET"],
                RelicIds = ["RANDOM_RELIC", "RANDOM_RELIC"],
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["gain two random relics and add Regret"]
            }, ["relic identities are runtime-random"]);
        }

        if (option.TextKey.Contains(".MERCHANT.options.INNOCENT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.AddCurse, EventOptionKind.UpgradeCard], new EventOutcomeSummary
            {
                CurseCardIds = ["SHAME"],
                UpgradeCount = 2,
                Notes = ["add Shame and upgrade two cards"]
            });
        }

        if (option.TextKey.Contains(".NOBLE.options.GUILTY", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.Heal], new EventOutcomeSummary
            {
                Notes = ["heal 10 hp"]
            }, ["heal amount is fixed but current-value impact is state-dependent"]);
        }

        if (option.TextKey.Contains(".NOBLE.options.INNOCENT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.AddCurse, EventOptionKind.GainGold], new EventOutcomeSummary
            {
                CurseCardIds = ["REGRET"],
                GoldDelta = 300,
                Notes = ["gain 300 gold and add Regret"]
            });
        }

        if (option.TextKey.Contains(".NONDESCRIPT.options.GUILTY", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.AddCurse, EventOptionKind.CardReward, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                CurseCardIds = ["DOUBT"],
                CardRewardCount = 2,
                HasRandomness = true,
                Notes = ["add Doubt and receive two card reward offerings"]
            }, ["reward card identities are runtime-random"]);
        }

        if (option.TextKey.Contains(".NONDESCRIPT.options.INNOCENT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:Trial", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.AddCurse, EventOptionKind.TransformCard], new EventOutcomeSummary
            {
                CurseCardIds = ["DOUBT"],
                TransformCount = 2,
                Notes = ["add Doubt and transform two cards"]
            });
        }

        return option;
    }
}
