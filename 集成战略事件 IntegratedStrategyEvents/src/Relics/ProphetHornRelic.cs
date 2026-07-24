using IntegratedStrategyEvents.Map;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.Relics;

public sealed class ProphetHornRelic : IntegratedStrategyEventRelic
{
	public const int TargetActIndex = 2;

	public ProphetHornRelic()
		: base("prophet_horn.png")
	{
	}

	public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex)
	{
		return MaybeCreateProphetHornMap(runState, map, actIndex, allowSavedMapReplacement: true);
	}

	public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)
	{
		return MaybeCreateProphetHornMap(runState, map, actIndex, allowSavedMapReplacement: false);
	}

	internal static bool IsActiveInRun(IRunState runState)
	{
		return runState.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is ProphetHornRelic));
	}

	private static ActMap MaybeCreateProphetHornMap(
		IRunState runState,
		ActMap map,
		int actIndex,
		bool allowSavedMapReplacement)
	{
		if (actIndex != TargetActIndex ||
			map is ProphetHornActMap ||
			IntegratedStrategySecretMapNodeController.ShouldSkipWholeMapReplacement(runState, map) ||
			(!allowSavedMapReplacement && map is SavedActMap) ||
			!IsActiveInRun(runState))
		{
			return map;
		}

		return new ProphetHornActMap(runState);
	}
}
