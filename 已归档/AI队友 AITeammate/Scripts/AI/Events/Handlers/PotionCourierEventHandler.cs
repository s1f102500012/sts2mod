namespace AITeammate.Scripts;

internal sealed class PotionCourierEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(PotionCourierEventHandler);

    protected override string EventTypeName => "PotionCourier";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.OptionIndex == 1)
        {
            return WithKnownOutcome(option, HandlerName, "special:PotionCourier:rare_potion", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainPotion, EventOptionKind.Randomized], new EventOutcomeSummary
            {
                PotionRewardCount = 1,
                HasRandomness = true,
                Notes = ["forced courier branch: gain one random rare potion"]
            });
        }

        return WithKnownOutcome(option, HandlerName, "special:PotionCourier:nonpreferred", true, EventSupportLevel.SpecialPartial, EventPlannerTrustLevel.Medium, false, [EventOptionKind.Randomized], new EventOutcomeSummary
        {
            HasRandomness = true,
            Notes = ["nonpreferred courier branch"]
        }, ["AI policy always chooses the second Potion Courier option"]);
    }
}
