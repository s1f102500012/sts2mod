#if STS2_109_OR_NEWER
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace IntegratedStrategyEvents;

internal static partial class IntegratedStrategyPrivateMembers
{
	// STS2 0.110.1、0.111.0 的换幕防重字段；0.107.1 不存在。
	private static IEnumerable<Contract> VersionContracts =>
		[new(typeof(ActChangeSynchronizer), "_lastTransitioningActIndex", "temporary-map")];
}
#endif
