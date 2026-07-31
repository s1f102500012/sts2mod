using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static void SettleLineage(LineageBinding seed)
	{
		CombatLedger ledger = seed.Ledger;
		if (!IsActiveCombat(ledger))
		{
			return;
		}

		if (!ledger.Settling.Add(seed.Lineage))
		{
			ConvergeMember(seed);
			return;
		}

		CausalScope? previous = ActiveScope.Value;
		CausalScope scope = new(ledger, seed.Lineage, seed.Member);
		ActiveScope.Value = scope;
		try
		{
			int stablePasses = 0;
			for (int pass = 0;
				pass < MaximumSettlementPasses && stablePasses < 2;
				pass++)
			{
				int membersBefore = seed.Lineage.Members.Count;
				ObserveCurrentCombat(seed);
				foreach (ErasureLineageMember member in seed.Lineage.Members)
				{
					if (member.Evidence.CreatureRef is not Creature creature)
					{
						continue;
					}

					LineageBinding binding = BindMember(
						ledger,
						seed.Lineage,
						member,
						creature);
					CausalScope priorScope = scope;
					scope = new CausalScope(
						ledger,
						seed.Lineage,
						member);
					ActiveScope.Value = scope;
					try
					{
						ConvergeMember(binding);
					}
					finally
					{
						scope = priorScope;
						ActiveScope.Value = scope;
					}
				}

				bool converged = seed.Lineage.Members.All(member =>
					member.Evidence.CreatureRef is Creature creature
					&& ReadLayerState(ledger, creature).IsConverged);
				bool unchanged =
					membersBefore == seed.Lineage.Members.Count;
				stablePasses = converged && unchanged
					? stablePasses + 1
					: 0;
			}
		}
		finally
		{
			ActiveScope.Value = previous;
			ledger.Settling.Remove(seed.Lineage);
		}

		foreach (ErasureLineageMember member in seed.Lineage.Members)
		{
			if (member.Evidence.CreatureRef is not Creature creature)
			{
				continue;
			}

			LineageBinding binding = BindMember(
				ledger,
				seed.Lineage,
				member,
				creature);
			ConvergeMember(binding);
			ErasureLayerState state = ReadLayerState(ledger, creature);
			if (!state.IsConverged)
			{
				Log.Warn(
					$"[{ModInfo.Id}] Exact lineage member did not fully " +
					$"converge after bounded settlement: " +
					$"{SafeModelId(creature)} combatId=" +
					$"{creature.CombatId?.ToString() ?? "<none>"}.");
			}
		}
	}

	private static void ObserveCurrentCombat(LineageBinding seed)
	{
		foreach (Creature creature in OrderObservedCreatures(
			seed.Ledger.CombatState.Allies
				.Concat(seed.Ledger.CombatState.Enemies)))
		{
			TryTrackCandidate(seed.Ledger.CombatState, creature, out _);
		}

		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}

		foreach (NCreature node in EnumerateRoomCreatureNodes(room)
			.Where(node => node?.Entity != null)
			.OrderBy(
				node => node.Entity.CombatId ?? uint.MaxValue)
			.ThenBy(
				node => node.Entity.SlotName ?? string.Empty,
				StringComparer.Ordinal)
			.ThenBy(
				node => SafeModelId(node.Entity),
				StringComparer.Ordinal)
			.ToArray())
		{
			CaptureNode(seed.Ledger, node.Entity, node);
			TryTrackCandidate(
				ReadAttachedCombat(node.Entity)
					?? seed.Ledger.CombatState,
				node.Entity,
				out _);
		}
	}

	private static Creature[] OrderObservedCreatures(
		IEnumerable<Creature> creatures)
	{
		return creatures
			.Distinct(CreatureReferenceComparer)
			.OrderBy(creature => creature.CombatId ?? uint.MaxValue)
			.ThenBy(
				creature => creature.SlotName ?? string.Empty,
				StringComparer.Ordinal)
			.ThenBy(SafeModelId, StringComparer.Ordinal)
			.ThenBy(
				creature => creature.Monster?.GetType().FullName
					?? string.Empty,
				StringComparer.Ordinal)
			.ToArray();
	}

	private static void ConvergeMember(LineageBinding binding)
	{
		CombatLedger ledger = binding.Ledger;
		Creature creature = binding.Creature;
		if (!IsActiveCombat(ledger))
		{
			return;
		}

		if (!ledger.Converging.Add(creature))
		{
			return;
		}

		try
		{
			if (!ReadLayerState(ledger, creature).IsConverged)
			{
				binding.Lineage.MarkActivity();
			}
			CaptureCurrentNodes(ledger, creature);
			ClearPowersSilently(creature);
			SetRawHpZero(creature);
			StopMonsterExecution(creature);

			HardRemoveNodes(ledger, creature);
			HardUnsubscribe(creature);
			HardRemoveFromCombatState(ledger, creature);
			ClearPowersSilently(creature);
			SetRawHpZero(creature);
		}
		finally
		{
			ledger.Converging.Remove(creature);
		}
	}

	private static void StopMonsterExecution(Creature creature)
	{
		MonsterModel? monster = creature.Monster;
		if (monster == null)
		{
			return;
		}

		MonsterMoveStateMachineField.SetValue(monster, null);
		MonsterIsPerformingMoveField.SetValue(monster, false);
	}

	private static void HardRemoveNodes(
		CombatLedger ledger,
		Creature creature)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}

		CaptureCurrentNodes(ledger, creature);
		if (!ledger.Nodes.TryGetValue(
			creature,
			out HashSet<NCreature>? captured))
		{
			return;
		}

		IList activeNodes = GetRequiredList(ActiveNodesField, room);
		IList removingNodes = GetRequiredList(RemovingNodesField, room);
		bool removedAny = false;
		bool focusWasRemoved = false;
		foreach (NCreature node in captured.ToArray())
		{
			if (!GodotObject.IsInstanceValid(node)
				|| !ReferenceEquals(node.Entity, creature))
			{
				captured.Remove(node);
				continue;
			}

			removedAny |= RemoveExact(activeNodes, node);
			removedAny |= RemoveExact(removingNodes, node);
			try
			{
				focusWasRemoved |= ReferenceEquals(
					room.GetViewport().GuiGetFocusOwner(),
					node.Hitbox);
			}
			catch
			{
			}
			try
			{
				node.ToggleIsInteractable(on: false);
			}
			catch
			{
			}
			try
			{
				node.Visible = false;
				node.QueueFreeSafely();
			}
			catch
			{
			}
		}
		if (focusWasRemoved)
		{
			try
			{
				room.CreatureNodes
					.FirstOrDefault(node => node.IsInteractable)
					?.Hitbox.GrabFocus();
			}
			catch
			{
			}
		}
		if (removedAny)
		{
			try
			{
				UpdateCreatureNavigationMethod.Invoke(room, null);
			}
			catch (Exception exception)
			{
				Log.Warn(
					$"[{ModInfo.Id}] Creature navigation refresh failed " +
					$"after erasure convergence: " +
					$"{exception.GetBaseException().Message}");
			}
		}
	}

	private static void HardUnsubscribe(Creature creature)
	{
		try
		{
			CombatManager.Instance.StateTracker.Unsubscribe(creature);
		}
		catch (Exception exception)
		{
			Log.Warn(
				$"[{ModInfo.Id}] StateTracker unsubscribe failed for " +
				$"{SafeModelId(creature)}: " +
				$"{exception.GetBaseException().Message}");
		}
	}

	private static void HardRemoveFromCombatState(
		CombatLedger ledger,
		Creature creature)
	{
		if (ledger.CombatState is not CombatState concrete)
		{
			return;
		}

		bool removed =
			RemoveExact(GetRequiredList(EnemiesField, concrete), creature)
			| RemoveExact(GetRequiredList(AlliesField, concrete), creature)
			| RemoveExact(
				GetRequiredList(EscapedCreaturesField, concrete),
				creature);
		if (ReferenceEquals(ReadAttachedCombat(creature), ledger.CombatState))
		{
			CombatStateBackingField.SetValue(creature, null);
		}
		if (removed
			&& ReferenceEquals(
				ReadManagerCombatState(CombatManager.Instance),
				ledger.CombatState)
			&& ManagerCreaturesChangedField.GetValue(CombatManager.Instance)
				is Action<CombatState> managerChanged)
		{
			InvokeHandlers(
				managerChanged,
				concrete,
				"CombatManager.CreaturesChanged");
		}
		if (removed
			&& CombatStateChangedField.GetValue(concrete)
				is Action<ICombatState> changed)
		{
			InvokeHandlers(
				changed,
				concrete,
				"CombatState.CreaturesChanged");
		}
	}

	private static void InvokeHandlers<T>(
		Action<T> handlers,
		T argument,
		string eventName)
	{
		foreach (Delegate handler in handlers.GetInvocationList())
		{
			try
			{
				((Action<T>)handler)(argument);
			}
			catch (Exception exception)
			{
				Log.Warn(
					$"[{ModInfo.Id}] {eventName} listener failed after " +
					$"erasure convergence: " +
					$"{exception.GetBaseException().Message}");
			}
		}
	}

	private static ErasureLayerState ReadLayerState(
		CombatLedger ledger,
		Creature creature)
	{
		bool activeNode = false;
		bool removingNode = false;
		bool capturedNodeAlive = false;
		NCombatRoom? room = NCombatRoom.Instance;
		if (room != null)
		{
			activeNode = room.CreatureNodes.Any(
				node => ReferenceEquals(node.Entity, creature));
			removingNode = room.RemovingCreatureNodes.Any(
				node => ReferenceEquals(node.Entity, creature));
		}
		if (ledger.Nodes.TryGetValue(
			creature,
			out HashSet<NCreature>? captured))
		{
			capturedNodeAlive = captured.Any(node =>
				IsExactLiveNode(node, creature));
		}

		return new ErasureLayerState(
			HpIsZero: ReadRawHp(creature) <= 0,
			IsAbsentFromCombat: !ContainsExact(
				ledger.CombatState,
				creature),
			IsUnattached: !ReferenceEquals(
				ReadAttachedCombat(creature),
				ledger.CombatState),
			HasNoActiveNode: !activeNode,
			HasNoRemovingNode: !removingNode,
			HasNoCapturedLiveNode: !capturedNodeAlive);
	}

	private static bool IsNodeStillAlive(NCreature node)
	{
		try
		{
			return GodotObject.IsInstanceValid(node)
				&& !node.IsQueuedForDeletion();
		}
		catch
		{
			return false;
		}
	}

	private static bool IsExactLiveNode(
		NCreature node,
		Creature creature)
	{
		try
		{
			return IsNodeStillAlive(node)
				&& ReferenceEquals(node.Entity, creature);
		}
		catch
		{
			return false;
		}
	}

	private static void CaptureCurrentNodes(
		CombatLedger ledger,
		Creature creature)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}

		foreach (NCreature node in EnumerateRoomCreatureNodes(room)
			.Where(node => IsExactLiveNode(node, creature)))
		{
			CaptureNode(ledger, creature, node);
		}
	}

	private static IReadOnlyList<NCreature> EnumerateRoomCreatureNodes(
		NCombatRoom room)
	{
		HashSet<NCreature> nodes = new(
			ReferenceEqualityComparer.Instance);
		foreach (NCreature node in room.CreatureNodes
			.Concat(room.RemovingCreatureNodes)
			.Concat(room.GetChildrenRecursive<NCreature>()))
		{
			if (GodotObject.IsInstanceValid(node))
			{
				nodes.Add(node);
			}
		}
		return nodes.ToArray();
	}

	private static void CaptureNode(
		CombatLedger ledger,
		Creature creature,
		NCreature node)
	{
		if (!ledger.Nodes.TryGetValue(
			creature,
			out HashSet<NCreature>? nodes))
		{
			nodes = new HashSet<NCreature>(
				ReferenceEqualityComparer.Instance);
			ledger.Nodes.Add(creature, nodes);
		}
		nodes.Add(node);
	}

	private static void ClearPowersSilently(Creature creature)
	{
		if (PowersField.GetValue(creature) is IList powers)
		{
			powers.Clear();
			return;
		}

		throw new MissingFieldException(
			$"Could not access the power list required by {nameof(ErasureKill)}.");
	}

	private static void SetRawHpZero(Creature creature)
	{
		CurrentHpField.SetValue(creature, 0);
	}

	private static int ReadRawHp(Creature creature)
	{
		return CurrentHpField.GetValue(creature) is int hp
			? hp
			: creature.CurrentHp;
	}

	private static bool ContainsExact(
		ICombatState combatState,
		Creature creature)
	{
		if (combatState is not CombatState concrete)
		{
			return combatState.ContainsCreature(creature);
		}

		return ContainsExact(
				GetRequiredList(EnemiesField, concrete),
				creature)
			|| ContainsExact(
				GetRequiredList(AlliesField, concrete),
				creature)
			|| ContainsExact(
				GetRequiredList(EscapedCreaturesField, concrete),
				creature);
	}

	private static bool ContainsExact(IList list, object value)
	{
		foreach (object? item in list)
		{
			if (ReferenceEquals(item, value))
			{
				return true;
			}
		}
		return false;
	}

	private static bool RemoveExact(IList list, object value)
	{
		bool removed = false;
		for (int index = list.Count - 1; index >= 0; index--)
		{
			if (!ReferenceEquals(list[index], value))
			{
				continue;
			}

			list.RemoveAt(index);
			removed = true;
		}
		return removed;
	}

	private static IList GetRequiredList(
		FieldInfo field,
		object instance)
	{
		return field.GetValue(instance) as IList
			?? throw new MissingFieldException(
				$"Could not access list field {field.Name}.");
	}
}
