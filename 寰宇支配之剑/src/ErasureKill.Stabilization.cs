using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static Task RestabilizeLineage(LineageBinding seed)
	{
		CombatLedger ledger = seed.Ledger;
		TaskCompletionSource owner;
		lock (ledger.Gate)
		{
			if (ledger.Restabilizations.TryGetValue(
				seed.Lineage,
				out Task? existing))
			{
				return existing;
			}

			owner = new TaskCompletionSource(
				TaskCreationOptions.RunContinuationsAsynchronously);
			ledger.Restabilizations.Add(seed.Lineage, owner.Task);
		}

		_ = RunTerminationTransaction(seed, owner);
		return owner.Task;
	}

	private static async Task RunTerminationTransaction(
		LineageBinding seed,
		TaskCompletionSource owner)
	{
		Exception? failure = null;
		try
		{
			await TerminateAndConverge(seed);
		}
		catch (Exception exception)
		{
			failure = exception;
		}
		finally
		{
			lock (seed.Ledger.Gate)
			{
				if (seed.Ledger.Restabilizations.TryGetValue(
						seed.Lineage,
						out Task? active)
					&& ReferenceEquals(active, owner.Task))
				{
					seed.Ledger.Restabilizations.Remove(seed.Lineage);
				}
			}
		}

		if (failure != null)
		{
			owner.TrySetException(failure);
			return;
		}

		ErasureSettlementTimingDecision timing =
			ErasureSettlementTimingPolicy.Evaluate(
				new ErasureSettlementTimingSnapshot(
					RunManager.Instance.ActionExecutor
						.CurrentlyRunningAction != null));
		if (timing == ErasureSettlementTimingDecision.EvaluateImmediately)
		{
			await RequestImmediateCombatCompletion(seed.Ledger);
		}
		else
		{
			GameAction? action = RunManager.Instance.ActionExecutor
				.CurrentlyRunningAction;
			if (action != null)
			{
				ScheduleActionBoundaryCompletion(seed.Ledger, action);
			}
		}
		owner.TrySetResult();
	}

	private static void ScheduleActionBoundaryCompletion(
		CombatLedger ledger,
		GameAction action)
	{
		PendingActionSettlements.Remove(action);
		PendingActionSettlements.Add(action, ledger);
		Log.Info(
			$"[{ModInfo.Id}] Normal combat completion is queued for the " +
			"current game-action boundary.");
	}

	private static void ScheduleRestabilization(LineageBinding seed)
	{
		try
		{
			if (seed.IsCausalOverflow)
			{
				ConvergeMember(seed);
			}
			SettleAndCertify(seed);
		}
		catch (Exception exception)
		{
			Log.Warn(
				$"[{ModInfo.Id}] Event-driven erasure convergence failed " +
				$"for operation {seed.Lineage.OperationSequence}: " +
				$"{exception.GetBaseException().Message}");
		}
	}

	private static async Task TerminateAndConverge(LineageBinding seed)
	{
		CombatLedger ledger = seed.Ledger;
		if (!IsActiveCombat(ledger))
		{
			return;
		}

		bool beginCanonicalTermination;
		lock (ledger.Gate)
		{
			beginCanonicalTermination =
				seed.Lineage.TryBeginCanonicalTermination();
			if (beginCanonicalTermination)
			{
				ledger.ActiveTerminationCount++;
				ledger.ActiveTerminationLineages.Add(seed.Lineage);
			}
		}

		if (beginCanonicalTermination)
		{
			CausalScope? previous = ActiveScope.Value;
			ActiveScope.Value = new CausalScope(
				ledger,
				seed.Lineage,
				seed.Member);
			try
			{
				CaptureCurrentNodes(ledger, seed.Creature);
				ReserveCanonicalVisualExit(ledger, seed.Creature);
				SetRawHpZero(seed.Creature);
				try
				{
					Log.Info(
						$"[{ModInfo.Id}] Entering isolated canonical death " +
						$"transition for operation " +
						$"{seed.Lineage.OperationSequence}.");
					await InvokeOriginalKillWithoutCheckingWinCondition(
						seed.Creature,
						force: true,
						recursion: 0);
					Log.Info(
						$"[{ModInfo.Id}] Canonical death transition " +
						$"completed for operation " +
						$"{seed.Lineage.OperationSequence}.");
				}
				catch (Exception exception)
				{
					Log.Warn(
						$"[{ModInfo.Id}] Canonical creature termination " +
						$"did not complete for {SafeModelId(seed.Creature)}; " +
						$"committing exact erasure state instead: " +
						$"{exception.GetBaseException().Message}");
				}
				EnsureCanonicalVisualExit(ledger, seed.Creature);
			}
			finally
			{
				ActiveScope.Value = previous;
				lock (ledger.Gate)
				{
					ledger.ActiveTerminationCount = Math.Max(
						0,
						ledger.ActiveTerminationCount - 1);
					ledger.ActiveTerminationLineages.Remove(seed.Lineage);
				}
			}
		}

		if (IsActiveCombat(ledger))
		{
			SettleAndCertify(seed);
		}
	}

	private static void SettleAndCertify(LineageBinding seed)
	{
		if (!IsActiveCombat(seed.Ledger))
		{
			return;
		}

		SettleLineage(seed);
		lock (seed.Ledger.Gate)
		{
			if (seed.Ledger.ActiveTerminationCount != 0)
			{
				return;
			}

			bool converged = seed.Lineage.Members.All(member =>
				member.Evidence.CreatureRef is Creature creature
				&& ReadLayerState(seed.Ledger, creature).IsConverged);
			if (converged)
			{
				seed.Lineage.TryIssueCompletionCertificate(
					seed.Lineage.ActivityRevision,
					seed.Lineage.MemberCount);
			}
		}
	}

	private static async Task RequestImmediateCombatCompletion(
		CombatLedger ledger)
	{
		if (!IsActiveCombat(ledger))
		{
			return;
		}

		try
		{
			CombatManager manager = CombatManager.Instance;
			CheckWinInvocation invocation = CaptureCheckWinInvocation(
				manager,
				invocationTurnState: null);
			if (!invocation.WasCurrentAtEntry
				|| invocation.Ledger == null
				|| !ReferenceEquals(invocation.Ledger, ledger))
			{
				return;
			}

			await CoordinateCombatCompletion(
				manager,
				invocation,
				ledger);
		}
		catch (Exception exception)
		{
			Log.Warn(
				$"[{ModInfo.Id}] Immediate normal combat completion check " +
				$"failed after erasure: {exception}");
		}
	}

	private static bool IsActiveCombat(CombatLedger ledger)
	{
		ManagerCombatSnapshot snapshot = ReadManagerSnapshot(
			CombatManager.Instance,
			invocationTurnState: null);
		return snapshot.IsCurrentInvocation
			&& ReferenceEquals(snapshot.CombatState, ledger.CombatState)
			&& (ledger.CombatEpoch == null
				|| ReferenceEquals(snapshot.TurnState, ledger.CombatEpoch));
	}
}
