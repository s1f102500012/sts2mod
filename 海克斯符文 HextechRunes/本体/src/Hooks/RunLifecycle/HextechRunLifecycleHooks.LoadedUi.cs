using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{
	private const int EnemyUiRefreshFrameBudget = 45;

	private static void LoadRunPostfix(RunState runState, ref Task __result)
	{
		__result = LoadRunAfterOriginal(__result, runState);
	}

	private static async Task LoadRunAfterOriginal(Task original, RunState runState)
	{
		await original;

		// mod 延续体异常不能把原版 LoadRun 任务链打成 faulted。
		try
		{
			await RefreshEnemyUiForRunWhenReady(runState, "LoadRun", EnemyUiRefreshFrameBudget);
			_ = TaskHelper.RunSafely(ResumePendingActSelectionAfterLoad(runState));
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] LoadRun continuation failed: {ex}");
		}
	}

	private static async Task ResumePendingActSelectionAfterLoad(RunState runState)
	{
		const int frameBudget = 300;
		for (int frame = 0; frame <= frameBudget; frame++)
		{
			if (!IsCurrentRun(runState))
			{
				return;
			}

			HextechMayhemModifier? modifier = GetMayhemModifier(runState);
			if (modifier != null)
			{
				int actIndex = runState.CurrentActIndex;
				if (actIndex is < 0 or > 2 || modifier.IsActResolved(actIndex))
				{
					return;
				}

				if (ShouldDeferActSelectionUntilAfterCurrentEvent(runState))
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResumePendingActSelectionAfterLoad: deferred for current event act={actIndex}");
					return;
				}

				if (NOverlayStack.Instance != null
					&& NRun.Instance?.GlobalUi?.TopBar != null
					&& ShouldScheduleActSelectionOnRoomEntered(runState, modifier))
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResumePendingActSelectionAfterLoad: reopening unresolved selection act={actIndex} frame={frame} room={runState.CurrentRoom?.GetType().Name ?? "null"}");
					await HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier);
					return;
				}
			}

			await WaitOneFrame();
		}

		Log.Warn($"[{ModInfo.Id}][Mayhem] ResumePendingActSelectionAfterLoad timed out: currentRun={IsCurrentRun(runState)} act={runState.CurrentActIndex} room={runState.CurrentRoom?.GetType().Name ?? "null"}");
	}

	private static void TopBarInitializePostfix(IRunState runState)
	{
		if (runState is RunState concreteRunState)
		{
			ScheduleEnemyUiRefresh(concreteRunState, "NTopBar.Initialize", EnemyUiRefreshFrameBudget);
			return;
		}

		ScheduleEnemyUiRefreshForCurrentRun("NTopBar.Initialize", EnemyUiRefreshFrameBudget);
	}

	private static void ScheduleEnemyUiRefresh(RunState runState, string reason, int frameBudget)
	{
		TaskHelper.RunSafely(RefreshEnemyUiForRunWhenReady(runState, reason, frameBudget));
	}

	private static void ScheduleEnemyUiRefreshForCurrentRun(string reason, int frameBudget)
	{
		TaskHelper.RunSafely(RefreshEnemyUiForCurrentRunWhenReady(reason, frameBudget));
	}

	private static async Task RefreshEnemyUiForCurrentRunWhenReady(string reason, int frameBudget)
	{
		for (int frame = 0; frame <= frameBudget; frame++)
		{
			if (RunManager.Instance.DebugOnlyGetState() is RunState runState)
			{
				bool refreshed = TryRefreshEnemyUiForRun(runState, reason, frame);
				if (refreshed)
				{
					return;
				}
			}

			await WaitOneFrame();
		}

		HextechEnemyUi.HideMayhemModifierBadge();
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyUi delayed refresh skipped: reason={reason} no current run after {frameBudget} frames");
	}

	private static async Task RefreshEnemyUiForRunWhenReady(RunState runState, string reason, int frameBudget)
	{
		for (int frame = 0; frame <= frameBudget; frame++)
		{
			if (TryRefreshEnemyUiForRun(runState, reason, frame))
			{
				return;
			}

			await WaitOneFrame();
		}

		HextechEnemyUi.HideMayhemModifierBadge();
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyUi delayed refresh skipped: reason={reason} topbar/modifier not ready after {frameBudget} frames");
	}

	private static bool TryRefreshEnemyUiForRun(RunState runState, string reason, int frame)
	{
		if (!IsCurrentRun(runState))
		{
			return true;
		}

		if (NRun.Instance?.GlobalUi?.TopBar == null || !HextechEnemyUi.IsTopBarReady())
		{
			return false;
		}

		HextechMayhemModifier? modifier = GetMayhemModifier(runState);
		if (modifier == null)
		{
			HextechEnemyUi.HideMayhemModifierBadge();
			return false;
		}

		SubscribeRoomEnteredIfNeeded();
		SubscribeRoomExitedIfNeeded();
		bool recovered = !modifier.IsActResolved(runState.CurrentActIndex)
			&& modifier.TryRecoverResolvedActsFromPlayerRelics(reason);
		HextechEnemyUi.Refresh(modifier);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyUi delayed refresh: reason={reason} frame={frame} recovered={recovered} actIndex={runState.CurrentActIndex} {modifier.DescribeActState()}");
		return true;
	}
}
