using System.Collections.Generic;

namespace AITeammate.Scripts;

internal abstract class EventSpecialHandlerBase : IEventSpecialHandler
{
    public abstract string HandlerName { get; }

    protected abstract string EventTypeName { get; }

    public bool CanHandle(EventVisitState snapshot)
    {
        return snapshot.EventTypeName == EventTypeName;
    }

    public abstract EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option);

    protected static EventOptionDescriptor WithKnownOutcome(
        EventOptionDescriptor option,
        string handlerName,
        string normalizationSource,
        bool fullyNormalized,
        EventSupportLevel supportLevel,
        EventPlannerTrustLevel trustLevel,
        bool safeForPlannerSelectionLater,
        IReadOnlyList<EventOptionKind> kinds,
        EventOutcomeSummary outcome,
        IReadOnlyList<string>? unknownReasons = null)
    {
        return new EventOptionDescriptor
        {
            OptionIndex = option.OptionIndex,
            TextKey = option.TextKey,
            Title = option.Title,
            Description = option.Description,
            IsLocked = option.IsLocked,
            IsProceed = option.IsProceed,
            IsLikelyLeaveOrExit = option.IsLikelyLeaveOrExit || outcome.LeaveLike,
            WillKillPlayer = option.WillKillPlayer,
            IsFullyNormalized = fullyNormalized,
            NormalizationSource = normalizationSource,
            HandlerName = handlerName,
            SupportLevel = supportLevel,
            TrustLevel = trustLevel,
            IsSafeForPlannerSelectionLater = safeForPlannerSelectionLater,
            RuntimeLocator = option.RuntimeLocator,
            RelicId = option.RelicId,
            RelicName = option.RelicName,
            HoverTipKinds = option.HoverTipKinds,
            Kinds = kinds,
            UnknownReasons = unknownReasons ?? [],
            Outcome = outcome
        };
    }
}
