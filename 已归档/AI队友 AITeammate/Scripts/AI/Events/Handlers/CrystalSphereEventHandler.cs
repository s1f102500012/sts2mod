using System;

namespace AITeammate.Scripts;

internal sealed class CrystalSphereEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(CrystalSphereEventHandler);

    protected override string EventTypeName => "CrystalSphere";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".UNCOVER_FUTURE", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:CrystalSphere", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.SpendGold, EventOptionKind.Randomized, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                GoldDelta = -75,
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["pay variable gold, then resolve crystal sphere minigame with mixed reward pool"]
            }, ["uncover future cost varies from 51 to 100", "minigame reward subset is stochastic and multi-step"]);
        }

        if (option.TextKey.Contains(".PAYMENT_PLAN", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:CrystalSphere", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.AddCurse, EventOptionKind.Randomized, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                CurseCardIds = ["DEBT"],
                HasRandomness = true,
                HasUnknownEffects = true,
                Notes = ["take Debt curse, then resolve larger crystal sphere minigame reward pool"]
            }, ["minigame reward subset is stochastic and multi-step"]);
        }

        return option;
    }
}
