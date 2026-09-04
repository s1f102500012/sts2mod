using System;

namespace AITeammate.Scripts;

internal sealed class EndlessConveyorEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(EndlessConveyorEventHandler);

    protected override string EventTypeName => "EndlessConveyor";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".LEAVE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.Leave], new EventOutcomeSummary
            {
                LeaveLike = true,
                Notes = ["leave the conveyor event"]
            });
        }

        if (option.TextKey.Contains(".OBSERVE_CHEF", StringComparison.Ordinal) || option.TextKey.Contains(".SPICY_SNAPPY", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.UpgradeCard, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                UpgradeCount = 1,
                HasRandomness = true,
                Notes = ["randomly upgrade one upgradable card"]
            }, ["upgrade target is random"]);
        }

        if (option.TextKey.Contains(".CAVIAR", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.GainMaxHp], new EventOutcomeSummary
            {
                GoldDelta = -35,
                MaxHpDelta = 4,
                Notes = ["buy max hp gain dish"]
            });
        }

        if (option.TextKey.Contains(".CLAM_ROLL", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.Heal], new EventOutcomeSummary
            {
                GoldDelta = -35,
                Notes = ["buy heal dish; exact heal value depends on current hp state"]
            }, ["heal value is state-dependent"]);
        }

        if (option.TextKey.Contains(".JELLY_LIVER", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.SpendGold, EventOptionKind.TransformCard], new EventOutcomeSummary
            {
                GoldDelta = -35,
                TransformCount = 1,
                Notes = ["buy transform dish"]
            });
        }

        if (option.TextKey.Contains(".FRIED_EEL", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.CardReward, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                GoldDelta = -35,
                CardRewardCount = 1,
                HasRandomness = true,
                Notes = ["gain one random colorless-style reward card"]
            }, ["exact reward card is random"]);
        }

        if (option.TextKey.Contains(".SUSPICIOUS_CONDIMENT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.GainPotion, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                GoldDelta = -35,
                PotionRewardCount = 1,
                HasRandomness = true,
                Notes = ["gain one random potion"]
            }, ["exact potion is random"]);
        }

        if (option.TextKey.Contains(".GOLDEN_FYSH", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainGold], new EventOutcomeSummary
            {
                GoldDelta = 75,
                Notes = ["gain gold without paying the normal dish cost"]
            });
        }

        if (option.TextKey.Contains(".SEAPUNK_SALAD", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.AddFixedCard], new EventOutcomeSummary
            {
                GoldDelta = -35,
                FixedCardIds = ["FEEDING_FRENZY"],
                Notes = ["gain Feeding Frenzy"]
            });
        }

        if (option.TextKey.Contains(".LOCKED", StringComparison.Ordinal))
        {
            return option;
        }

        return WithKnownOutcome(option, HandlerName, "special:EndlessConveyor", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.Randomized, EventOptionKind.MultiStep], new EventOutcomeSummary
        {
            HasRandomness = true,
            HasUnknownEffects = true,
            Notes = ["unrecognized conveyor dish option"]
        }, ["dish identity/effects are not fully mapped by the handler"]);
    }
}
