namespace AITeammate.Scripts;

internal interface IEventSpecialHandler
{
    string HandlerName { get; }

    bool CanHandle(EventVisitState snapshot);

    EventOptionDescriptor Normalize(EventVisitState snapshot, EventOptionDescriptor option);
}
