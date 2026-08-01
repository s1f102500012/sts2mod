using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static readonly ConditionalWeakTable<Creature, CombatLedger>
		TerminalIngresses = new();

	private static bool TryQuarantineTerminalIngress(
		ICombatState? combatState,
		Creature creature)
	{
		if (!TryResolveTerminalLedger(combatState, creature, out CombatLedger? ledger)
			|| !ShouldRejectTerminalIngress(ledger, creature))
		{
			return false;
		}

		QuarantineTerminalIngress(ledger, creature);
		return true;
	}

	private static bool TryQuarantineTerminalNode(
		Creature creature,
		NCreature node)
	{
		if (!TryQuarantineTerminalIngress(
				ReadAttachedCombat(creature),
				creature))
		{
			return false;
		}

		NCombatRoom? room = NCombatRoom.Instance;
		if (room != null)
		{
			RemoveExact(GetRequiredList(ActiveNodesField, room), node);
			RemoveExact(GetRequiredList(RemovingNodesField, room), node);
		}
		if (GodotObject.IsInstanceValid(node))
		{
			node.QueueFree();
		}
		return true;
	}

	private static bool IsErasedOrTerminalIngress(Creature creature)
	{
		return TryGetBinding(creature, out _)
			|| TerminalIngresses.TryGetValue(creature, out _);
	}

	private static Task CreateTerminalIngressCompletion()
	{
		return ErasureTerminalIngressPolicy.CreateRejectedIngressTask();
	}

	private static bool TryResolveTerminalLedger(
		ICombatState? combatState,
		Creature creature,
		[NotNullWhen(true)] out CombatLedger? ledger)
	{
		if (TerminalIngresses.TryGetValue(creature, out ledger))
		{
			return true;
		}

		if (combatState != null
			&& Ledgers.TryGetValue(combatState, out ledger))
		{
			return true;
		}

		ICombatState? attached = ReadAttachedCombat(creature);
		if (attached != null
			&& Ledgers.TryGetValue(attached, out ledger))
		{
			return true;
		}

		if (Bindings.TryGetValue(creature, out LineageBinding? binding))
		{
			ledger = binding.Ledger;
			return true;
		}

		ledger = null;
		return false;
	}

	private static bool ShouldRejectTerminalIngress(
		CombatLedger ledger,
		Creature creature)
	{
		ManagerCombatSnapshot managerState = ReadManagerSnapshot(
			CombatManager.Instance,
			invocationTurnState: null);
		ErasureTerminalBarrierPhase barrierPhase;
		bool completionRunning;
		bool isBaselineEnemy;
		lock (ledger.Gate)
		{
			barrierPhase = ledger.TerminalBarrierPhase;
			completionRunning = ledger.CompletionDisposition
				== CompletionDisposition.Running;
			isBaselineEnemy =
				ledger.TerminalBaselineEnemies.Contains(creature);
		}

		ErasureTerminalIngressSnapshot snapshot = new(
			HasTrackedCombat: true,
			IsEnemy: creature.Side == CombatSide.Enemy,
			IsBaselineEnemy: isBaselineEnemy,
			BarrierPhase: barrierPhase,
			IsCompletionFlightRunning: completionRunning,
			IsExpectedCombat:
				managerState.IsCurrentInvocation
					&& ReferenceEquals(
						managerState.CombatState,
						ledger.CombatState)
					&& (ledger.CombatEpoch == null
						|| ReferenceEquals(
							managerState.TurnState,
							ledger.CombatEpoch)),
			IsInProgress: managerState.IsInProgress);
		return ErasureTerminalIngressPolicy.Evaluate(snapshot)
			== ErasureTerminalIngressDecision.RejectTerminalIngress;
	}

	private static void SealTerminalCombat(CombatLedger ledger)
	{
		lock (ledger.Gate)
		{
			if (ledger.TerminalBarrierPhase
				< ErasureTerminalBarrierPhase.Completed)
			{
				ledger.TerminalBarrierPhase =
					ErasureTerminalBarrierPhase.Completed;
			}
		}
	}

	private static void CommitTerminalCombat(CombatLedger ledger)
	{
		lock (ledger.Gate)
		{
			if (ledger.TerminalBarrierPhase
				>= ErasureTerminalBarrierPhase.Committed)
			{
				return;
			}
			if (ledger.TerminalBarrierPhase
				!= ErasureTerminalBarrierPhase.Armed)
			{
				throw new InvalidOperationException(
					"Terminal combat settlement requires an armed participation barrier.");
			}
			ledger.TerminalBarrierPhase =
				ErasureTerminalBarrierPhase.Committed;
		}
	}

	private static bool TryArmTerminalCombat(
		CombatLedger ledger,
		ICombatState combatState,
		Creature selectedTarget)
	{
		Creature[] activeParticipants = ErasureRosterPolicy
			.SnapshotNonNull(combatState.Enemies)
			.Distinct(CreatureReferenceComparer)
			.Where(enemy => IsActiveEnemyAtSelection(
				combatState,
				selectedTarget,
				enemy))
			.ToArray();
		if (!activeParticipants.Any(enemy =>
				ReferenceEquals(enemy, selectedTarget))
			|| activeParticipants.Any(enemy =>
				!ReferenceEquals(enemy, selectedTarget)
				&& enemy.IsPrimaryEnemy))
		{
			return false;
		}

		lock (ledger.Gate)
		{
			if (ledger.TerminalBarrierPhase
				!= ErasureTerminalBarrierPhase.Open)
			{
				return true;
			}

			ledger.TerminalBaselineEnemies.Clear();
			foreach (Creature participant in activeParticipants)
			{
				ledger.TerminalBaselineEnemies.Add(participant);
			}
			ledger.TerminalBarrierPhase =
				ErasureTerminalBarrierPhase.Armed;
			return true;
		}
	}

	private static bool IsActiveEnemyAtSelection(
		ICombatState combatState,
		Creature selectedTarget,
		Creature candidate)
	{
		bool isPresentInEnemyRoster = combatState is CombatState concrete
			? ContainsExact(GetRequiredList(EnemiesField, concrete), candidate)
			: ErasureRosterPolicy.SnapshotNonNull(combatState.Enemies)
				.Any(enemy => ReferenceEquals(enemy, candidate));
		bool hasStableCombatPresence = !string.IsNullOrEmpty(candidate.SlotName);
		NCombatRoom? room = NCombatRoom.Instance;
		if (!hasStableCombatPresence && room != null)
		{
			hasStableCombatPresence = room.CreatureNodes.Any(node =>
				GodotObject.IsInstanceValid(node)
				&& !node.IsQueuedForDeletion()
				&& ReferenceEquals(node.Entity, candidate));
		}

		return ErasureParticipationPolicy.IsActiveAtSelection(
			new ErasureParticipantSnapshot(
				IsEnemy: candidate.Side == CombatSide.Enemy,
				IsSelectedTarget: ReferenceEquals(candidate, selectedTarget),
				IsAttachedToExpectedCombat: ReferenceEquals(
					ReadAttachedCombat(candidate),
					combatState),
				IsPresentInEnemyRoster: isPresentInEnemyRoster,
				HasStableCombatPresence: hasStableCombatPresence));
	}

	private static void QuarantineTerminalIngress(
		CombatLedger ledger,
		Creature creature)
	{
		RememberTerminalIngress(creature, ledger);
		SetRawHpZero(creature);
		StopMonsterExecution(creature);
		DetachTerminalIngress(ledger, creature);
		LogTerminalIngressOnce(ledger, creature);
	}

	private static void RememberTerminalIngress(
		Creature creature,
		CombatLedger ledger)
	{
		TerminalIngresses.Remove(creature);
		TerminalIngresses.Add(creature, ledger);
	}

	private static void DetachTerminalIngress(
		CombatLedger ledger,
		Creature creature)
	{
		if (ledger.CombatState is CombatState concrete)
		{
			RemoveExact(GetRequiredList(EnemiesField, concrete), creature);
		}

		if (ReferenceEquals(ReadAttachedCombat(creature), ledger.CombatState))
		{
			CombatStateBackingField.SetValue(creature, null);
		}
	}

	private static void LogTerminalIngressOnce(
		CombatLedger ledger,
		Creature creature)
	{
		lock (ledger.Gate)
		{
			if (!ledger.LoggedTerminalIngresses.Add(creature))
			{
				return;
			}
		}

		Log.Info(
			$"[{ModInfo.Id}] Rejected enemy ingress during terminal combat " +
			$"settlement: {SafeModelId(creature)}.");
	}
}
