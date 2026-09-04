#if !STS2_109_OR_NEWER
using MegaCrit.Sts2.Core.Multiplayer.Game;
namespace IntegratedStrategyEvents.TreeHoles;
internal static class IntegratedStrategyFinaleActChangeGuardPatch
{
	internal static void ResetTransitionMemory(ActChangeSynchronizer synchronizer) { }
}
#endif
