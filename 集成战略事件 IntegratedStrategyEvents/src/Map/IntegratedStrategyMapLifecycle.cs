using IntegratedStrategyEvents.TreeHoles;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Models;

namespace IntegratedStrategyEvents.Map;

public sealed class IntegratedStrategyMapLifecycle : HookedSingletonModel
{
	public IntegratedStrategyMapLifecycle() : base(HookType.Run) { }

	// 新图与读档都分发 Late，恢复只执行一次且不依赖地图界面被创建。
	public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)
	{
		return runState is RunState state && state.CurrentActIndex == actIndex
			? TreeHoleSessionManager.RestoreMapForGeneration(state, map)
			: map;
	}

	public override Task AfterMapGenerated(ActMap map, int actIndex)
	{
		if (CurrentRunState is RunState state && IntegratedStrategyPatcher.IsAvailable("forced-events"))
			IntegratedStrategySecretMapNodeController.MarkSecretNodes(state, map, actIndex);
		return Task.CompletedTask;
	}
}
