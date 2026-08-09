using IntegratedStrategyEvents.Encounters;
using MegaCrit.Sts2.Core.Events;

namespace IntegratedStrategyEvents.Events;

public sealed partial class PrimalEntertainmentEvent : IntegratedStrategyEventModel
{
	public override bool IsShared => true;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		return
		[
			Choice(FaceOpponent, "FACE_OPPONENT"),
			Choice(AvoidGaze, "AVOID_GAZE")
		];
	}

	private Task FaceOpponent()
	{
		ShowFightPage<PrimalEntertainmentMioEncounter>("FACE_OPPONENT");
		return Task.CompletedTask;
	}

	private Task AvoidGaze()
	{
		Finish("AVOID_GAZE");
		return Task.CompletedTask;
	}
}
