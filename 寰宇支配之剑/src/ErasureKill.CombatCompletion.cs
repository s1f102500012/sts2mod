using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;

namespace UniversalDominionSword;

internal enum CompletionDisposition
{
	Idle,
	Running,
	Completed,
	Indeterminate
}

internal static partial class ErasureKill
{
	private static readonly AsyncLocal<CombatLedger?>
		ActiveCompletionEvaluation = new();

	private static async Task<bool> FinishCheckWinCondition(
		CombatManager manager,
		CheckWinInvocation? invocation,
		bool ranOriginal,
		Task<bool>? originalTask)
	{
		bool originalResult = await (
			originalTask ?? Task.FromResult(false));
		if (invocation == null)
		{
			return originalResult;
		}
		CombatLedger? ledger = invocation.Ledger;
		if (!invocation.WasCurrentAtEntry
			|| invocation.CombatState == null
			|| ledger == null)
		{
			return originalResult;
		}

		ManagerCombatSnapshot snapshot = ReadManagerSnapshot(
			manager,
			invocation.TurnState);
		if (!snapshot.IsCurrentInvocation
			|| !ReferenceEquals(
				snapshot.CombatState,
				invocation.CombatState))
		{
			return originalResult;
		}
		if (!snapshot.IsInProgress)
		{
			return true;
		}

		if (originalResult)
		{
			lock (ledger.Gate)
			{
				if (!ledger.LoggedPseudoSuccess)
				{
					ledger.LoggedPseudoSuccess = true;
					Log.Warn(
						$"[{ModInfo.Id}] A win check reported success " +
						$"while the same combat remained active; " +
						$"treating the result as incomplete.");
				}
			}
		}

		_ = ranOriginal;
		return await CoordinateCombatCompletion(
			manager,
			invocation,
			ledger);
	}

	private static Task<bool> CoordinateCombatCompletion(
		CombatManager manager,
		CheckWinInvocation invocation,
		CombatLedger ledger)
	{
		if (ReferenceEquals(
			ActiveCompletionEvaluation.Value,
			ledger))
		{
			return Task.FromResult(false);
		}

		TaskCompletionSource<bool> owner;
		lock (ledger.Gate)
		{
			if (ledger.CompletionDisposition
				== CompletionDisposition.Indeterminate)
			{
				return Task.FromResult(false);
			}
			if (ledger.CompletionFlight != null)
			{
				return ledger.CompletionFlight;
			}

			owner = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			ledger.CompletionFlight = owner.Task;
			ledger.CompletionDisposition =
				CompletionDisposition.Running;
		}

		_ = RunCompletionFlight(
			manager,
			invocation,
			ledger,
			owner);
		return owner.Task;
	}

	private static async Task RunCompletionFlight(
		CombatManager manager,
		CheckWinInvocation invocation,
		CombatLedger ledger,
		TaskCompletionSource<bool> owner)
	{
		CombatLedger? previous = ActiveCompletionEvaluation.Value;
		ActiveCompletionEvaluation.Value = ledger;
		try
		{
			SettleLedger(ledger);
			Creature[] terminalBaseline = OrderObservedCreatures(
				ledger.CombatState.Enemies);
			ErasureCompletionDecision beforeHook =
				EvaluateCompletion(
					manager,
					invocation,
					ledger,
					blockedByHook: false);
			if (beforeHook
				!= ErasureCompletionDecision.AllowNormalEnd)
			{
				FinishCompletionFlight(
					ledger,
					owner,
					CompletionDisposition.Idle,
					result: false);
				return;
			}

			CommitTerminalCombat(ledger, terminalBaseline);
			SweepTerminalIngresses(ledger);
			await InvokeOriginalCombatSettlement(
				manager,
				invocation.TurnState);
			SweepTerminalIngresses(ledger);

			ManagerCombatSnapshot afterEnd = ReadManagerSnapshot(
				manager,
				invocation.TurnState);
			bool completed =
				!afterEnd.IsCurrentInvocation
				|| !ReferenceEquals(
					afterEnd.CombatState,
					ledger.CombatState)
				|| !afterEnd.IsInProgress;
			if (completed)
			{
				SealTerminalCombat(ledger);
				FinishCompletionFlight(
					ledger,
					owner,
					CompletionDisposition.Completed,
					result: true);
				return;
			}

			lock (ledger.Gate)
			{
				if (!ledger.LoggedIndeterminateCompletion)
				{
					ledger.LoggedIndeterminateCompletion = true;
					Log.Warn(
						$"[{ModInfo.Id}] Normal combat settlement " +
						$"returned without a terminal state; " +
						$"automatic retries are disabled for this combat.");
				}
			}
			FinishCompletionFlight(
				ledger,
				owner,
				CompletionDisposition.Indeterminate,
				result: false);
		}
		catch (Exception exception)
		{
			ManagerCombatSnapshot afterFailure = ReadManagerSnapshot(
				manager,
				invocation.TurnState);
			bool settlementCommitted =
				!afterFailure.IsCurrentInvocation
				|| !ReferenceEquals(
					afterFailure.CombatState,
					ledger.CombatState)
				|| !afterFailure.IsInProgress;
			if (settlementCommitted)
			{
				SealTerminalCombat(ledger);
				FinishCompletionFlight(
					ledger,
					owner,
					CompletionDisposition.Completed,
					result: true);
				return;
			}

			FailCompletionFlight(
				ledger,
				owner,
				ledger.TerminalSealed
					? CompletionDisposition.Indeterminate
					: CompletionDisposition.Idle,
				exception);
		}
		finally
		{
			ActiveCompletionEvaluation.Value = previous;
		}
	}

	private static void FinishCompletionFlight(
		CombatLedger ledger,
		TaskCompletionSource<bool> owner,
		CompletionDisposition disposition,
		bool result)
	{
		lock (ledger.Gate)
		{
			ledger.CompletionDisposition = disposition;
			if (disposition == CompletionDisposition.Completed)
			{
				ledger.TerminalSealed = true;
			}
			if (disposition == CompletionDisposition.Idle)
			{
				ledger.CompletionFlight = null;
			}
		}
		owner.TrySetResult(result);
	}

	private static void FailCompletionFlight(
		CombatLedger ledger,
		TaskCompletionSource<bool> owner,
		CompletionDisposition disposition,
		Exception exception)
	{
		lock (ledger.Gate)
		{
			ledger.CompletionDisposition = disposition;
			if (disposition == CompletionDisposition.Idle)
			{
				ledger.CompletionFlight = null;
			}
		}
		owner.TrySetException(exception);
	}

	private static ErasureCompletionDecision EvaluateCompletion(
		CombatManager manager,
		CheckWinInvocation invocation,
		CombatLedger ledger,
		bool blockedByHook)
	{
		ManagerCombatSnapshot managerState = ReadManagerSnapshot(
			manager,
			invocation.TurnState);
		ErasureLineage[] lineages;
		HashSet<Creature> trackedCreatures = new(
			ReferenceEqualityComparer.Instance);
		bool completionArmed;
		bool hasOpenPersistenceLease;
		bool allCertified;
		bool hasActiveConvergence;
		lock (ledger.Gate)
		{
			lineages = ledger.Lineages.ToArray();
			completionArmed = ledger.CompletionArmed;
			hasOpenPersistenceLease =
				ledger.PersistenceLeaseCount != 0;
			allCertified = lineages.All(lineage =>
				lineage.TryGetCompletionCertificate(out _));
			hasActiveConvergence =
				ledger.ActiveTerminationCount != 0
				|| ledger.Settling.Count != 0
				|| ledger.Converging.Count != 0
				|| ledger.Restabilizations.Count != 0;
			foreach (ErasureLineageMember member in
				lineages.SelectMany(lineage => lineage.Members))
			{
				if (member.Evidence.CreatureRef is Creature creature)
				{
					trackedCreatures.Add(creature);
				}
			}
		}

		ICombatState combatState = ledger.CombatState;
		bool hasLivingPlayer = combatState.Players.Any(
			player => ReadRawHp(player.Creature) > 0
				|| player.Creature.IsAlive);
		bool hasLivingUntrackedPrimaryEnemy =
			combatState.Enemies.Any(enemy =>
				(ReadRawHp(enemy) > 0 || enemy.IsAlive)
				&& enemy.IsPrimaryEnemy
				&& !trackedCreatures.Contains(enemy));

		ErasureCompletionSnapshot policyState = new(
			IsExpectedCombat:
				invocation.WasCurrentAtEntry
					&& managerState.IsCurrentInvocation
					&& ReferenceEquals(
						managerState.CombatState,
						combatState)
					&& (ledger.CombatEpoch == null
						|| ReferenceEquals(
							managerState.TurnState,
							ledger.CombatEpoch)),
			IsInProgress: managerState.IsInProgress,
			IsStarting: managerState.IsStarting,
			HasPendingLoss: managerState.HasPendingLoss,
			HasLivingPlayer: hasLivingPlayer,
			HasTrackedLineage: lineages.Length != 0,
			HasOpenPersistenceLease: hasOpenPersistenceLease,
			IsCompletionArmed: completionArmed,
			AreAllLineagesCertified: allCertified,
			HasActiveConvergence: hasActiveConvergence,
			HasLivingUntrackedPrimaryEnemy:
				hasLivingUntrackedPrimaryEnemy,
			IsBlockedByCombatEndHook: blockedByHook);
		return ErasureCompletionPolicy.Evaluate(policyState);
	}

	private static void SettleLedger(CombatLedger ledger)
	{
		foreach (ErasureLineage lineage in SnapshotLineages(ledger))
		{
			LineageBinding? binding = GetAnyBinding(ledger, lineage);
			if (binding != null)
			{
				SettleLineage(binding);
			}
		}
	}

	private sealed record CheckWinInvocation(
		object? TurnState,
		ICombatState? CombatState,
		CombatLedger? Ledger,
		bool WasCurrentAtEntry);

}
