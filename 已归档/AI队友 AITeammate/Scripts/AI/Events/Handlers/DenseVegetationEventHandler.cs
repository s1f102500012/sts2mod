using System;

namespace AITeammate.Scripts;

internal sealed class DenseVegetationEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(DenseVegetationEventHandler);

    protected override string EventTypeName => "DenseVegetation";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.TextKey.Contains(".TRUDGE_ON", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DenseVegetation", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.RemoveCard, EventOptionKind.LoseHp], new EventOutcomeSummary
            {
                RemoveCount = 1,
                HpDelta = -11,
                Notes = ["remove one card and take hp loss"]
            });
        }

        if (option.TextKey.Contains(".REST", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DenseVegetation", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.Heal, EventOptionKind.EnterCombat, EventOptionKind.MultiStep], new EventOutcomeSummary
            {
                StartsCombat = true,
                HasUnknownEffects = true,
                Notes = ["rests first, then exposes a follow-up fight option"]
            }, ["heal amount is runtime dependent", "combat outcome is not normalized in pass 1"]);
        }

        if (option.TextKey.Contains(".FIGHT", StringComparison.Ordinal))
        {
            return WithKnownOutcome(option, HandlerName, "special:DenseVegetation", false, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Low, false, [EventOptionKind.EnterCombat], new EventOutcomeSummary
            {
                StartsCombat = true,
                HasUnknownEffects = true,
                Notes = ["fight encounter after rest branch"]
            }, ["combat outcome not normalized in pass 1"]);
        }

        return option;
    }
}
