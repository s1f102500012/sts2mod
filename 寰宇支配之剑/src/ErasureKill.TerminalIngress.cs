using System.Collections;
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

	private static Task CreateTerminalIngressCancellation()
	{
		return Task.FromCanceled(new CancellationToken(canceled: true));
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
		bool isSealed;
		bool completionRunning;
		bool isBaselineEnemy;
		lock (ledger.Gate)
		{
			isSealed = ledger.TerminalSealed;
			completionRunning = ledger.CompletionDisposition
				== CompletionDisposition.Running;
			isBaselineEnemy =
				ledger.TerminalBaselineEnemies.Contains(creature);
		}

		ErasureTerminalIngressSnapshot snapshot = new(
			HasTrackedCombat: true,
			IsEnemy: creature.Side == CombatSide.Enemy,
			IsBaselineEnemy: isBaselineEnemy,
			IsTerminalSealed: isSealed,
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
			ledger.TerminalSealed = true;
		}
	}

	private static void CommitTerminalCombat(
		CombatLedger ledger,
		IEnumerable<Creature> baselineEnemies)
	{
		lock (ledger.Gate)
		{
			if (ledger.TerminalSealed)
			{
				return;
			}

			ledger.TerminalBaselineEnemies.Clear();
			foreach (Creature enemy in baselineEnemies)
			{
				ledger.TerminalBaselineEnemies.Add(enemy);
			}
			ledger.TerminalSealed = true;
		}
	}

	private static void SweepTerminalIngresses(CombatLedger ledger)
	{
		List<Creature> candidates = [];
		if (ledger.CombatState is CombatState concrete)
		{
			candidates.AddRange(
				GetRequiredList(EnemiesField, concrete)
					.Cast<Creature>());
		}

		NCombatRoom? room = NCombatRoom.Instance;
		if (room != null)
		{
			candidates.AddRange(
				EnumerateRoomCreatureNodes(room)
					.Where(node => node?.Entity != null)
					.Select(node => node.Entity));
		}

		foreach (Creature creature in candidates
			.Distinct(CreatureReferenceComparer)
			.ToArray())
		{
			if (!ShouldRejectTerminalIngress(ledger, creature))
			{
				continue;
			}

			QuarantineTerminalIngress(ledger, creature);
			RemoveTerminalIngressNodes(creature);
		}
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

	private static void RemoveTerminalIngressNodes(Creature creature)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}

		IList activeNodes = GetRequiredList(ActiveNodesField, room);
		IList removingNodes = GetRequiredList(RemovingNodesField, room);
		foreach (NCreature node in EnumerateRoomCreatureNodes(room)
			.Where(node => node?.Entity != null
				&& ReferenceEquals(node.Entity, creature))
			.ToArray())
		{
			RemoveExact(activeNodes, node);
			RemoveExact(removingNodes, node);
			if (GodotObject.IsInstanceValid(node))
			{
				node.QueueFree();
			}
		}
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
			$"[{ModInfo.Id}] Rejected enemy ingress into a completed " +
			$"combat: {SafeModelId(creature)}.");
	}
}
