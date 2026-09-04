using System.Collections.Generic;
using System.Linq;

namespace AITeammate.Scripts;

internal sealed class EventHandlerRegistry
{
    private readonly IReadOnlyList<IEventSpecialHandler> _handlers =
    [
        new AromaOfChaosEventHandler(),
        new DenseVegetationEventHandler(),
        new DrowningBeaconEventHandler(),
        new DollRoomEventHandler(),
        new EndlessConveyorEventHandler(),
        new CrystalSphereEventHandler(),
        new OrobasEventHandler(),
        new PotionCourierEventHandler(),
        new SunkenStatueEventHandler(),
        new TabletOfTruthEventHandler(),
        new TrialEventHandler(),
        new WelcomeToWongosEventHandler(),
        new WellspringEventHandler(),
        new WhisperingHollowEventHandler(),
        new WoodCarvingsEventHandler(),
        new ZenWeaverEventHandler()
    ];

    public IEventSpecialHandler? Resolve(EventVisitState snapshot)
    {
        return _handlers.FirstOrDefault(handler => handler.CanHandle(snapshot));
    }
}
