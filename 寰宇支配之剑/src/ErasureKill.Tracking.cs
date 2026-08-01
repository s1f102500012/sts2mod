using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static readonly ConditionalWeakTable<GameAction, CombatLedger>
		PendingActionSettlements = new();

	private static bool TryTrackCandidate(
		ICombatState? combatState,
		Creature creature,
		ErasureMutationKind mutationKind,
		[NotNullWhen(true)]
		out LineageBinding? binding)
	{
		if (TryGetBinding(creature, out binding))
		{
			return true;
		}

		CombatLedger? ledger = null;
		if (combatState != null)
		{
			Ledgers.TryGetValue(combatState, out ledger);
		}

		CausalScope? scope = ActiveScope.Value;
		if (ledger == null
			&& scope != null
			&& (combatState == null
				|| ReferenceEquals(
					combatState,
					scope.Ledger.CombatState)))
		{
			ledger = scope.Ledger;
		}
		if (ledger == null)
		{
			binding = null;
			return false;
		}

		ErasureEvidence evidence = CaptureEvidence(creature);
		lock (ledger.Gate)
		{
			foreach (ErasureLineage lineage in ledger.Lineages)
			{
				ErasureAdmission strong = lineage.TryAdmitStrong(evidence);
				if (strong.Member != null)
				{
					binding = BindMember(
						ledger,
						lineage,
						strong.Member,
						creature);
					LogAdmission(binding, strong.Kind);
					return true;
				}
			}

			if (scope != null
				&& ReferenceEquals(scope.Ledger, ledger))
			{
				ErasureAdmission causal = scope.Lineage.ObserveCausal(
					evidence,
					scope.Token,
					mutationKind);
				if (causal.Member != null)
				{
					binding = BindMember(
						ledger,
						scope.Lineage,
						causal.Member,
						creature);
					LogAdmission(binding, causal.Kind);
					return true;
				}
				if (causal.Kind == ErasureAdmissionKind.LimitReached)
				{
					scope.Lineage.MarkActivity();
					binding = BindCausalOverflow(
						ledger,
						scope.Lineage,
						scope.Parent,
						creature);
					LogAdmission(binding, causal.Kind);
					return true;
				}
			}

				ErasureLineage[] terminalTransactions = ledger
					.ActiveTerminationLineages
					.Where(lineage =>
						lineage.WasTerminalCandidateAtStart)
				.ToArray();
			if (terminalTransactions.Length == 1)
			{
				ErasureLineage terminalLineage = terminalTransactions[0];
				ErasureAdmission terminal =
					terminalLineage.ObserveTerminalSuccessor(
						evidence,
						mutationKind);
				if (terminal.Member != null)
				{
					binding = BindMember(
						ledger,
						terminalLineage,
						terminal.Member,
						creature);
					LogAdmission(binding, terminal.Kind);
					return true;
				}
				if (terminal.Kind == ErasureAdmissionKind.LimitReached)
				{
					terminalLineage.MarkActivity();
					binding = BindCausalOverflow(
						ledger,
						terminalLineage,
						terminalLineage.Members
							.OrderByDescending(member =>
								member.AdmissionOrdinal)
							.First(),
						creature);
					LogAdmission(binding, terminal.Kind);
					return true;
				}
			}
		}

		binding = null;
		return false;
	}

	private static bool TryTrackCandidate(
		ICombatState? combatState,
		Creature creature,
		[NotNullWhen(true)]
		out LineageBinding? binding)
	{
		return TryTrackCandidate(
			combatState,
			creature,
			ErasureMutationKind.Observed,
			out binding);
	}

	private static LineageBinding BindMember(
		CombatLedger ledger,
		ErasureLineage lineage,
		ErasureLineageMember member,
		Creature creature)
	{
		if (Bindings.TryGetValue(creature, out LineageBinding? existing))
		{
			return existing;
		}

		LineageBinding binding = new(
			ledger,
			lineage,
			member,
			creature);
		try
		{
			Bindings.Add(creature, binding);
		}
		catch (ArgumentException)
		{
			if (Bindings.TryGetValue(
				creature,
				out LineageBinding? raced))
			{
				return raced;
			}
			throw;
		}
		return binding;
	}

	private static LineageBinding BindCausalOverflow(
		CombatLedger ledger,
		ErasureLineage lineage,
		ErasureLineageMember causalParent,
		Creature creature)
	{
		if (Bindings.TryGetValue(creature, out LineageBinding? existing))
		{
			return existing;
		}

		LineageBinding binding = new(
			ledger,
			lineage,
			causalParent,
			creature,
			IsCausalOverflow: true);
		try
		{
			Bindings.Add(creature, binding);
		}
		catch (ArgumentException)
		{
			if (Bindings.TryGetValue(
					creature,
					out LineageBinding? raced))
			{
				return raced;
			}
			throw;
		}
		return binding;
	}

	private static bool TryGetBinding(
		Creature creature,
		[NotNullWhen(true)]
		out LineageBinding? binding)
	{
		if (!Bindings.TryGetValue(creature, out binding))
		{
			return false;
		}

		ICombatState? attached = ReadAttachedCombat(creature);
		ICombatState? activeCombat = ReadManagerCombatState(
			CombatManager.Instance);
		if ((attached != null
				&& !ReferenceEquals(
					attached,
					binding.Ledger.CombatState))
			|| (attached == null
				&& !ReferenceEquals(
					activeCombat,
					binding.Ledger.CombatState)))
		{
			Bindings.Remove(creature);
			binding = null;
			return false;
		}

		return true;
	}

	private static LineageBinding? GetAnyBinding(
		CombatLedger ledger,
		ErasureLineage lineage)
	{
		foreach (ErasureLineageMember member in lineage.Members)
		{
			if (member.Evidence.CreatureRef is Creature creature)
			{
				return BindMember(ledger, lineage, member, creature);
			}
		}
		return null;
	}

	private static IReadOnlyList<ErasureLineage> SnapshotLineages(
		CombatLedger ledger)
	{
		lock (ledger.Gate)
		{
			return ledger.Lineages.ToArray();
		}
	}

	private static ErasureEvidence CaptureEvidence(Creature creature)
	{
		return new ErasureEvidence(
			creature,
			creature.CombatId,
			creature.Monster,
			creature.Monster?.GetType().FullName
				?? creature.Monster?.GetType().Name
				?? "<none>",
			creature.SlotName,
			creature.Side == CombatSide.Enemy,
			creature.IsPrimaryEnemy);
	}

	private static void LogAdmission(
		LineageBinding binding,
		ErasureAdmissionKind kind)
	{
		if (!binding.Ledger.LoggedAdmissions.Add(binding.Creature))
		{
			return;
		}

		Log.Info(
			$"[{ModInfo.Id}] Admitted direct erasure continuation " +
			$"{SafeModelId(binding.Creature)} by {kind}; " +
			$"combatId={binding.Creature.CombatId?.ToString() ?? "<none>"}; " +
			$"slot={binding.Creature.SlotName ?? "<none>"}; " +
			$"operation={binding.Lineage.OperationSequence}.");
	}

	private sealed class CombatLedger
	{
		public CombatLedger(ICombatState combatState)
		{
			CombatState = combatState;
			ManagerCombatSnapshot snapshot = ReadManagerSnapshot(
				CombatManager.Instance,
				invocationTurnState: null);
			if (ReferenceEquals(snapshot.CombatState, combatState))
			{
				CombatEpoch = snapshot.TurnState;
			}
		}

		public object Gate { get; } = new();

		public ICombatState CombatState { get; }

		public object? CombatEpoch { get; }

		public bool CompletionArmed { get; set; }

		public int PersistenceLeaseCount { get; set; }

		public int ActiveTerminationCount { get; set; }

		public HashSet<ErasureLineage> ActiveTerminationLineages { get; } =
			new(ReferenceEqualityComparer.Instance);

		public Task<bool>? CompletionFlight { get; set; }

		public CompletionDisposition CompletionDisposition { get; set; }

		public ErasureTerminalBarrierPhase TerminalBarrierPhase { get; set; }

		public bool TerminalBarrierArmed =>
			TerminalBarrierPhase != ErasureTerminalBarrierPhase.Open;

		public bool TerminalSealed =>
			TerminalBarrierPhase >= ErasureTerminalBarrierPhase.Committed;

		public HashSet<Creature> TerminalBaselineEnemies { get; } =
			new(ReferenceEqualityComparer.Instance);

		public bool LoggedPseudoSuccess { get; set; }

		public bool LoggedIndeterminateCompletion { get; set; }

		public bool LoggedDiscardedDeferredCallback { get; set; }

		public bool LoggedTerminalLossAttempt { get; set; }

		public bool LoggedSettlementProgressRepair { get; set; }

		public HashSet<ErasureCompletionDecision> LoggedCompletionDeferrals { get; } = [];

		public long NextOperationSequence { get; set; }

		public List<ErasureLineage> Lineages { get; } = [];

		public HashSet<ErasureLineage> Settling { get; } =
			new(ReferenceEqualityComparer.Instance);

		public HashSet<Creature> Converging { get; } =
			new(ReferenceEqualityComparer.Instance);

		public HashSet<Creature> LoggedAdmissions { get; } =
			new(ReferenceEqualityComparer.Instance);

		public HashSet<Creature> LoggedTerminalIngresses { get; } =
			new(ReferenceEqualityComparer.Instance);

		public Dictionary<ErasureLineage, Task> Restabilizations { get; } =
			new(ReferenceEqualityComparer.Instance);

		public Dictionary<Creature, HashSet<NCreature>> Nodes { get; } =
			new(ReferenceEqualityComparer.Instance);

		public HashSet<NCreature> VisualExitNodes { get; } =
			new(ReferenceEqualityComparer.Instance);
	}

	private sealed record LineageBinding(
		CombatLedger Ledger,
		ErasureLineage Lineage,
		ErasureLineageMember Member,
		Creature Creature,
		bool IsCausalOverflow = false);

	private sealed class CausalScope
	{
		public CausalScope(
			CombatLedger ledger,
			ErasureLineage lineage,
			ErasureLineageMember parent)
		{
			Ledger = ledger;
			Lineage = lineage;
			Parent = parent;
		}

		public CombatLedger Ledger { get; }

		public ErasureLineage Lineage { get; }

		public ErasureLineageMember Parent { get; }

		public ErasureContinuationToken Token =>
			Lineage.CreateContinuationToken(Parent);
	}

}
