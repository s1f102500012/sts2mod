using MegaCrit.Sts2.Core.Nodes;

namespace HextechRunes;

internal static partial class HextechRunLifecycleHooks
{

	private static async Task FinalizeStartingRelicsAfterOriginal(Task original, RunManager self)
	{
		await original;

		// mod 延续体异常不能把原版任务链打成 faulted(单端中断即联机分叉)。
		try
		{
			RunState? runState = self.DebugOnlyGetState();
			if (runState == null)
			{
				return;
			}

			foreach (Player player in runState.Players)
			{
				HextechRuneSelectionCoordinator.RemoveRunesFromGrabBags(player);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] FinalizeStartingRelics continuation failed: {ex}");
		}
	}


	private static async Task StartRunAfterOriginal(Task original, RunState runState)
	{
		try
		{
			await original;
		}
		finally
		{
			RunsInsideStartRunOrig.Remove(runState);
		}

		// mod 延续体异常不能把原版 StartRun 任务链打成 faulted(单端中断即联机分叉)。
		try
		{
			HextechMayhemModifier modifier = EnsureMayhemModifier(runState);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour end: currentRoom={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={runState.CurrentActIndex} {DescribeCurrentEventState(runState)}");
			try
			{
				HextechEnemyUi.HideMayhemModifierBadge();
				HextechEnemyUi.Refresh(modifier);
			}
			catch (Exception ex)
			{
				Log.Error($"[{ModInfo.Id}][Mayhem] StartRunDetour UI refresh failed: {ex}");
			}

			if (!modifier.IsActResolved(runState.CurrentActIndex)
				&& IsCurrentRun(runState))
			{
				if (ShouldDeferActSelectionUntilAfterCurrentEvent(runState))
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour: deferring act{runState.CurrentActIndex} selection until ancient event finishes {DescribeCurrentEventState(runState)}");
				}
				else
				{
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour: selecting act{runState.CurrentActIndex} hex immediately after StartRun");
					await HextechRuneSelectionCoordinator.HandleActSelection(runState, modifier);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] StartRunDetour continuation failed: {ex}");
		}
	}

	[HarmonyPatch(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics), new Type[0])]
	[HextechPatch("run.finalize-starting-relics", "跑局生命周期")]
	private static class FinalizeStartingRelicsPatch
	{
		[HarmonyPostfix]
		private static void Postfix(RunManager __instance, ref Task __result)
		{
			__result = FinalizeStartingRelicsAfterOriginal(__result, __instance);
		}
	}

	[HarmonyPatch(typeof(NGame), "StartRun", typeof(RunState))]
	[HextechPatch("run.start", "跑局生命周期")]
	private static class StartRunPatch
	{
		[HarmonyPrefix]
		private static void Prefix(RunState runState)
		{
			HextechRunLogBudget.Reset();
			HextechCombatHooks.ResetTransientCombatState();
			HextechEnemyHexEffects.ResetAllRunScopedState();
			HextechGoldrendSync.ResetForRun(runState);
			HextechRuneSelectionCoordinator.ResetActSelectionState();
			HextechEnemyUi.Clear();
			HextechEnemyUi.HideMayhemModifierBadge();
			SubscribeRoomEnteredIfNeeded();
			SubscribeRoomExitedIfNeeded();
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] StartRunDetour begin: seed={runState.Rng.StringSeed} actIndex={runState.CurrentActIndex} startedWithNeow={runState.ExtraFields.StartedWithNeow}");
			RunsInsideStartRunOrig.Add(runState);
		}

		[HarmonyPostfix]
		private static void Postfix(RunState runState, ref Task __result)
		{
	#if STS2_109_OR_NEWER
			HextechSavedPropertyBootstrap.RunOfficialCacheAuditOnce();
	#endif
			__result = StartRunAfterOriginal(__result, runState);
		}
	}
}
