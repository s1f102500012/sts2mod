using IntegratedStrategyEvents.Map;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Relics;

public sealed class PetalRelic : IntegratedStrategyEventRelic
{
	public const int TargetActIndex = 2;

	public PetalRelic()
		: base("petal.png")
	{
	}

	public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex)
	{
		return MaybeCreatePetalMap(runState, map, actIndex, allowSavedMapReplacement: true);
	}

	public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)
	{
		return MaybeCreatePetalMap(runState, map, actIndex, allowSavedMapReplacement: false);
	}

	internal static bool IsActiveInRun(IRunState runState)
	{
		return runState.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is PetalRelic));
	}

	private static ActMap MaybeCreatePetalMap(
		IRunState runState,
		ActMap map,
		int actIndex,
		bool allowSavedMapReplacement)
	{
		if (actIndex != TargetActIndex ||
			map is PetalActMap ||
			map is ProphetHornActMap ||
			IntegratedStrategySecretMapNodeController.ShouldSkipMapMutation(runState, map) ||
			(!allowSavedMapReplacement && map is SavedActMap) ||
			!IsActiveInRun(runState))
		{
			return map;
		}

		return new PetalActMap(runState);
	}
}
