using System;

namespace AITeammate.Scripts;

internal sealed class OrobasEventHandler : EventSpecialHandlerBase
{
    public override string HandlerName => nameof(OrobasEventHandler);

    protected override string EventTypeName => "Orobas";

    public override EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option)
    {
        if (option.RelicId == null ||
            !option.TextKey.Contains("OROBAS.pages.INITIAL.options.", StringComparison.Ordinal))
        {
            return option;
        }

        return WithKnownOutcome(option, HandlerName, "special:Orobas:attached_relic_choice", true, EventSupportLevel.SpecialHighConfidence, EventPlannerTrustLevel.High, true, [EventOptionKind.GainRelic], new EventOutcomeSummary
        {
            RelicIds = [option.RelicId],
            Notes = [$"orobasRelic={option.RelicId}"]
        });
    }
}
