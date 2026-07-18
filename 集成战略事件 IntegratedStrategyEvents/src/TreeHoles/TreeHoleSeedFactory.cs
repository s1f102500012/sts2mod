using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class TreeHoleSeedFactory
{
	public const string TreeHoleMapRngName = "integrated_strategy_tree_hole_map";

	public static uint CreateTreeHoleMapSeed(RunState state, string destinationActName, string stageLabel)
	{
		uint destinationHash = IntegratedStrategyStableRng.HashString(destinationActName);
		uint stageHash = IntegratedStrategyStableRng.HashString(stageLabel);
		return IntegratedStrategyStableRng.CreateSeed(
			state.Rng.Seed,
			TreeHoleMapRngName,
			unchecked((uint)state.CurrentActIndex),
			IntegratedStrategyStableRng.HashCoord(state.CurrentMapCoord),
			destinationHash,
			stageHash);
	}

	public static uint CreateRadiantApexCombatNodeSeed(RunState state)
	{
		uint foldedRunSeed = unchecked((uint)(state.Rng.Seed ^ (state.Rng.Seed >> 32)));
		return unchecked(foldedRunSeed ^
			(uint)state.CurrentActIndex * 0x9e3779b9u ^
			(uint)state.ActFloor * 0x85ebca6bu ^
			0xA9E5_2026u);
	}
}
