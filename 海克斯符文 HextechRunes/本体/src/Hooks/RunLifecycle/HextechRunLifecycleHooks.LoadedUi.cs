using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{
	private const int EnemyUiRefreshFrameBudget = 45;

	private static void LoadRunPostfix(RunState runState, ref Task __result)
	{
		HextechRunLogBudget.Reset();
		HextechCombatHooks.ResetTransientCombatState();
		HextechEnemyHexEffects.ResetAllRunScopedState();
		HextechGoldrendSync.ResetForRun(runState);
		__result = LoadRunAfterOriginal(__result, runState);
	}

	private static async Task LoadRunAfterOriginal(Task original, RunState runState)
	{
		await original;

		// mod 延续体异常不能把原版 LoadRun 任务链打成 faulted。
		try
		{
			await RefreshEnemyUiForRunWhenReady(runState, "LoadRun", EnemyUiRefreshFrameBudget);
			_ = TaskHelper.RunSafely(ResumePendingSelectionTransactionsAfterLoad(runState));
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] LoadRun continuation failed: {ex}");
		}
	}

	private static async Task ResumePendingSelectionTransactionsAfterLoad(RunState runState)
	{
		if (!await ResumePendingInitialForgeGrantsAfterLoad(runState))
		{
			return;
		}

		await ResumePendingActSelectionAfterLoad(runState);
	}

	private static async Task<bool> ResumePendingInitialForgeGrantsAfterLoad(RunState runState)
	{
		List<InitialForgeGrantRune> pending = runState.Players
			.SelectMany(static player => player.Relics.OfType<InitialForgeGrantRune>())
			.Where(static rune => rune.SavedInitialForgeGrantPending)
			.ToList();
		if (pending.Count == 0)
		{
			return true;
		}

		const int frameBudget = 300;
		for (int frame = 0; frame <= frameBudget; frame++)
		{
			if (!IsCurrentRun(runState))
			{
				return false;
			}

			if (NOverlayStack.Instance != null
				&& NRun.Instance?.GlobalUi?.TopBar != null
				&& NOverlayStack.Instance.Peek() == null)
			{
				bool reopenMap = NMapScreen.Instance?.IsOpen == true && NGame.Instance != null;
				if (reopenMap)
				{
					NMapScreen.Instance!.Close(animateOut: false);
					await WaitOneFrame();
				}

				try
				{
					foreach (InitialForgeGrantRune rune in pending)
					{
						if (!IsCurrentRun(runState))
						{
							return false;
						}

						HextechLog.Info(
							$"[{ModInfo.Id}][ForgeChoice] Resuming pending initial forge grants after load: "
							+ $"player={rune.Owner?.NetId.ToString() ?? "none"} rune={rune.Id.Entry}");
						if (!await rune.ResumePendingInitialForgeGrant())
						{
							HextechLog.Info(
								$"[{ModInfo.Id}][ForgeChoice] Pending initial forge grants remain unresolved after load: "
								+ $"player={rune.Owner?.NetId.ToString() ?? "none"} rune={rune.Id.Entry}");
							return false;
						}
					}

					return true;
				}
				finally
				{
					if (reopenMap
						&& IsCurrentRun(runState)
						&& NMapScreen.Instance != null
						&& !NMapScreen.Instance.IsOpen)
					{
						NMapScreen.Instance.Open();
					}
				}
			}

			await WaitOneFrame();
		}

		Log.Warn(
			$"[{ModInfo.Id}][ForgeChoice] Pending initial forge grant recovery timed out: "
			+ $"currentRun={IsCurrentRun(runState)} count={pending.Count}");
		return false;
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

			HextechMayhemModifier? modifier = HextechMayhemModifier.FindIn(runState);
			if (modifier != null)
			{
				int stageIndex = ResolveCurrentStageIndex(runState, modifier, out _);
				if (stageIndex < 0 || modifier.IsStageResolved(stageIndex))
				{
					return;
				}

				if (ShouldDeferActSelectionUntilAfterCurrentEvent(runState))
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResumePendingActSelectionAfterLoad: deferred for current event act={runState.CurrentActIndex} stage={stageIndex}");
					return;
				}

				if (NOverlayStack.Instance != null
					&& NRun.Instance?.GlobalUi?.TopBar != null
					&& ShouldScheduleActSelectionOnRoomEntered(runState, modifier, stageIndex))
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResumePendingActSelectionAfterLoad: reopening unresolved selection act={runState.CurrentActIndex} stage={stageIndex} frame={frame} room={runState.CurrentRoom?.GetType().Name ?? "null"}");
					await HextechRuneSelectionCoordinator.HandleStageSelection(runState, modifier, stageIndex);
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

		HextechMayhemModifier? modifier = HextechMayhemModifier.FindIn(runState);
		if (modifier == null)
		{
			HextechEnemyUi.HideMayhemModifierBadge();
			return false;
		}

		SubscribeRoomEnteredIfNeeded();
		SubscribeRoomExitedIfNeeded();
		int stageIndex = ResolveCurrentStageIndex(runState, modifier, out _);
		bool recovered = !modifier.IsStageResolved(stageIndex)
			&& modifier.TryRecoverResolvedActsFromPlayerRelics(reason, stageIndex);
		HextechEnemyUi.Refresh(modifier);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] EnemyUi delayed refresh: reason={reason} frame={frame} recovered={recovered} actIndex={runState.CurrentActIndex} {modifier.DescribeActState()}");
		return true;
	}
}
