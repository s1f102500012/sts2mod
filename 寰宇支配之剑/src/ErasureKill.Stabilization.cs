using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;

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

		_ = RunRestabilization(seed, owner);
		return owner.Task;
	}

	private static async Task RunRestabilization(
		LineageBinding seed,
		TaskCompletionSource owner)
	{
		try
		{
			await SettleAcrossContinuationLease(seed);
			owner.TrySetResult();
		}
		catch (Exception exception)
		{
			owner.TrySetException(exception);
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
	}

	private static void ScheduleRestabilization(LineageBinding seed)
	{
		_ = ObserveRestabilization(RestabilizeLineage(seed), seed);
	}

	private static async Task ObserveRestabilization(
		Task task,
		LineageBinding seed)
	{
		try
		{
			await task;
		}
		catch (Exception exception)
		{
			Log.Warn(
				$"[{ModInfo.Id}] Deferred erasure stabilization failed " +
				$"for operation {seed.Lineage.OperationSequence}: " +
				$"{exception.GetBaseException().Message}");
		}
	}

	private static async Task SettleAcrossContinuationLease(
		LineageBinding seed)
	{
		CombatLedger ledger = seed.Ledger;
		if (!IsActiveCombat(ledger))
		{
			return;
		}

		CausalScope? previous = ActiveScope.Value;
		CausalScope scope = new(
			ledger,
			seed.Lineage,
			seed.Member);
		lock (ledger.Gate)
		{
			seed.Lineage.AcquireContinuationLease();
		}
		ActiveScope.Value = scope;

		Exception? bodyFailure = null;
		LineageStabilizationResult stabilization = default;
		try
		{
			SettleLineage(seed);
			stabilization = await StabilizeLineageAcrossFrames(seed);
		}
		catch (Exception exception)
		{
			bodyFailure = exception;
			throw;
		}
		finally
		{
			ActiveScope.Value = previous;
			lock (ledger.Gate)
			{
				seed.Lineage.ReleaseContinuationLease();
			}
			if (IsActiveCombat(ledger))
			{
				try
				{
					SettleLineage(seed);
					if (stabilization.IsStable
						&& seed.Lineage.ActivityRevision
							== stabilization.ActivityRevision
						&& seed.Lineage.MemberCount
							== stabilization.MemberCount
						&& seed.Lineage.OutstandingContinuationLeaseCount == 0
						&& seed.Lineage.Members.All(member =>
							member.Evidence.CreatureRef is Creature creature
							&& ReadLayerState(
								seed.Ledger,
								creature).IsConverged))
					{
						seed.Lineage.TryIssueCompletionCertificate(
							stabilization.ActivityRevision,
							stabilization.MemberCount);
					}
				}
				catch (Exception finalException) when (bodyFailure != null)
				{
					Log.Warn(
						$"[{ModInfo.Id}] Final erasure convergence also " +
						$"failed after {bodyFailure.GetType().Name}: " +
						$"{finalException.GetBaseException().Message}");
				}
			}
		}
	}

	private static async Task<LineageStabilizationResult>
		StabilizeLineageAcrossFrames(
		LineageBinding seed)
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return default;
		}

		int stableFrames = 0;
		int memberCount = seed.Lineage.Members.Count;
		long activityRevision = seed.Lineage.ActivityRevision;
		for (int frame = 0;
			frame < MaximumStabilizationFrames;
			frame++)
		{
			try
			{
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
			catch (Exception) when (!IsActiveCombat(seed.Ledger))
			{
				return default;
			}
			if (!IsActiveCombat(seed.Ledger))
			{
				return default;
			}

			SettleLineage(seed);
			int currentMemberCount = seed.Lineage.Members.Count;
			long currentActivityRevision =
				seed.Lineage.ActivityRevision;
			bool converged = seed.Lineage.Members.All(member =>
				member.Evidence.CreatureRef is Creature creature
				&& ReadLayerState(seed.Ledger, creature).IsConverged);
			stableFrames =
				converged
				&& seed.Lineage.OutstandingContinuationLeaseCount == 1
				&& currentMemberCount == memberCount
				&& currentActivityRevision == activityRevision
				? stableFrames + 1
				: 0;
			memberCount = currentMemberCount;
			activityRevision = currentActivityRevision;
			if (stableFrames >= StableFramesToCloseContinuationLease)
			{
				return new LineageStabilizationResult(
					IsStable: true,
					activityRevision,
					memberCount);
			}
		}

		Log.Warn(
			$"[{ModInfo.Id}] Erasure continuation lease reached its bounded " +
			$"frame limit for operation {seed.Lineage.OperationSequence}; " +
			$"continuing with {seed.Lineage.Members.Count} exact members.");
		return default;
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

	private readonly record struct LineageStabilizationResult(
		bool IsStable,
		long ActivityRevision,
		int MemberCount);
}
