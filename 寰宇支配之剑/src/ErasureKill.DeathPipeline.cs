using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	private static MethodInfo GetKillWithoutCheckingWinCondition()
	{
		return AccessTools.DeclaredMethod(
				typeof(CreatureCmd),
				"KillWithoutCheckingWinCondition",
				[typeof(Creature), typeof(bool), typeof(int)])
			?? throw new MissingMethodException(
				typeof(CreatureCmd).FullName,
				"KillWithoutCheckingWinCondition");
	}

	private static MethodInfo GetKillStateMachineMoveNext()
	{
		MethodInfo entry = GetKillWithoutCheckingWinCondition();
		Type stateMachine = entry
			.GetCustomAttribute<AsyncStateMachineAttribute>()
			?.StateMachineType
			?? throw new MissingMemberException(
				"CreatureCmd kill state machine metadata is missing.");
		return AccessTools.DeclaredMethod(stateMachine, "MoveNext")
			?? throw new MissingMethodException(
				stateMachine.FullName,
				"MoveNext");
	}

	private static Task InvokeOriginalKillWithoutCheckingWinCondition(
		Creature creature,
		bool force,
		int recursion)
	{
		throw new NotSupportedException(
			"The canonical creature death entry was not initialized.");
	}

	private static void InvokeOriginalRemoveCreatureNode(
		NCombatRoom room,
		NCreature node)
	{
		throw new NotSupportedException(
			"The canonical creature node removal was not initialized.");
	}

	private static void InvokeOriginalCombatManagerRemoveCreature(
		CombatManager manager,
		Creature creature)
	{
		throw new NotSupportedException(
			"The canonical combat manager removal was not initialized.");
	}

	private static void InvokeOriginalCombatStateRemoveCreature(
		CombatState combatState,
		Creature creature,
		bool unattach)
	{
		throw new NotSupportedException(
			"The canonical combat state removal was not initialized.");
	}

	private static IEnumerable<CodeInstruction>
		ErasureDeathPipelineTranspiler(
			IEnumerable<CodeInstruction> instructions)
	{
		Dictionary<MethodInfo, (MethodInfo Wrapper, int Expected)> replacements =
			new()
			{
				[RequireMethod(typeof(Hook), nameof(Hook.BeforeDeath))] =
					(RequireLocalMethod(nameof(BeforeDeathForErasure)), 1),
				[RequireMethod(typeof(Hook), nameof(Hook.ShouldDie))] =
					(RequireLocalMethod(nameof(ShouldDieForErasure)), 1),
				[RequireMethod(
					typeof(Creature),
					nameof(Creature.InvokeDiedEvent),
					Type.EmptyTypes)] =
					(RequireLocalMethod(nameof(InvokeDiedEventForErasure)), 1),
				[RequireMethod(
					typeof(Hook),
					nameof(Hook.ShouldCreatureBeRemovedFromCombatAfterDeath))] =
					(RequireLocalMethod(nameof(ShouldRemoveForErasure)), 1),
				[RequireMethod(typeof(Hook), nameof(Hook.AfterDeath))] =
					(RequireLocalMethod(nameof(AfterDeathForErasure)), 2),
				[RequireMethod(
					typeof(Creature),
					nameof(Creature.RemoveAllPowersAfterDeath),
					Type.EmptyTypes)] =
					(RequireLocalMethod(nameof(RemovePowersForErasure)), 1),
				[RequireMethod(
					typeof(Creature),
					$"get_{nameof(Creature.IsPrimaryEnemy)}",
					Type.EmptyTypes)] =
					(RequireLocalMethod(nameof(IsPrimaryEnemyForErasure)), 1),
				[RequireMethod(
					typeof(NCombatRoom),
					nameof(NCombatRoom.RemoveCreatureNode),
					[typeof(NCreature)])] =
					(RequireLocalMethod(nameof(RemoveCreatureNodeForErasure)), 1),
				[RequireMethod(
					typeof(CombatManager),
					nameof(CombatManager.RemoveCreature),
					[typeof(Creature)])] =
					(RequireLocalMethod(
						nameof(CombatManagerRemoveCreatureForErasure)), 1),
				[RequireMethod(
					typeof(ICombatState),
					nameof(ICombatState.RemoveCreature),
					[typeof(Creature), typeof(bool)])] =
					(RequireLocalMethod(
						nameof(CombatStateRemoveCreatureForErasure)), 1)
			};
		Dictionary<MethodInfo, int> observed = replacements.Keys
			.ToDictionary(method => method, _ => 0);
		List<CodeInstruction> rewritten = instructions.ToList();
		foreach (CodeInstruction instruction in rewritten)
		{
			if (instruction.operand is not MethodInfo called
				|| !replacements.TryGetValue(
					called,
					out (MethodInfo Wrapper, int Expected) replacement))
			{
				continue;
			}

			instruction.opcode = OpCodes.Call;
			instruction.operand = replacement.Wrapper;
			observed[called]++;
		}

		foreach ((MethodInfo original, (MethodInfo _, int expected))
			in replacements)
		{
			if (observed[original] != expected)
			{
				throw new InvalidProgramException(
					$"Unexpected {original.DeclaringType?.Name}." +
					$"{original.Name} call count in the creature death " +
					$"state machine: expected {expected}, " +
					$"observed {observed[original]}.");
			}
		}

		return rewritten;
	}

	private static MethodInfo RequireMethod(
		Type type,
		string name,
		Type[]? parameters = null)
	{
		return (parameters == null
				? AccessTools.Method(type, name)
				: AccessTools.Method(type, name, parameters))
			?? throw new MissingMethodException(type.FullName, name);
	}

	private static MethodInfo RequireLocalMethod(string name)
	{
		return AccessTools.DeclaredMethod(typeof(ErasureKill), name)
			?? throw new MissingMethodException(typeof(ErasureKill).FullName, name);
	}

	private static Task BeforeDeathForErasure(
		IRunState runState,
		ICombatState combatState,
		Creature creature)
	{
		return TryGetBinding(creature, out _)
			? Task.CompletedTask
			: Hook.BeforeDeath(runState, combatState, creature);
	}

	private static bool ShouldDieForErasure(
		IRunState runState,
		ICombatState combatState,
		Creature creature,
		ref AbstractModel? preventer)
	{
		if (TryGetBinding(creature, out _))
		{
			preventer = null;
			return true;
		}

		return Hook.ShouldDie(
			runState,
			combatState,
			creature,
			out preventer);
	}

	private static void InvokeDiedEventForErasure(Creature creature)
	{
		if (!TryGetBinding(creature, out _))
		{
			creature.InvokeDiedEvent();
		}
	}

	private static bool ShouldRemoveForErasure(
		ICombatState combatState,
		Creature creature)
	{
		return TryGetBinding(creature, out _)
			|| Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(
				combatState,
				creature);
	}

	private static Task AfterDeathForErasure(
		IRunState runState,
		ICombatState combatState,
		Creature creature,
		bool wasRemovalPrevented,
		float deathAnimLength)
	{
		return TryGetBinding(creature, out _)
			? Task.CompletedTask
			: Hook.AfterDeath(
				runState,
				combatState,
				creature,
				wasRemovalPrevented,
				deathAnimLength);
	}

	private static IEnumerable<PowerModel> RemovePowersForErasure(
		Creature creature)
	{
		return TryGetBinding(creature, out _)
			? Array.Empty<PowerModel>()
			: creature.RemoveAllPowersAfterDeath();
	}

	private static bool IsPrimaryEnemyForErasure(Creature creature)
	{
		return !TryGetBinding(creature, out _)
			&& creature.IsPrimaryEnemy;
	}

	private static void RemoveCreatureNodeForErasure(
		NCombatRoom room,
		NCreature node)
	{
		if (TryGetBinding(node.Entity, out _))
		{
			InvokeOriginalRemoveCreatureNode(room, node);
			return;
		}

		room.RemoveCreatureNode(node);
	}

	private static void CombatManagerRemoveCreatureForErasure(
		CombatManager manager,
		Creature target)
	{
		if (TryGetBinding(target, out _))
		{
			InvokeOriginalCombatManagerRemoveCreature(manager, target);
			return;
		}

		manager.RemoveCreature(target);
	}

	private static void CombatStateRemoveCreatureForErasure(
		ICombatState combatState,
		Creature target,
		bool unattach)
	{
		if (!TryGetBinding(target, out _))
		{
			combatState.RemoveCreature(target, unattach);
			return;
		}

		if (combatState is not CombatState concrete)
		{
			throw new InvalidOperationException(
				"Canonical erasure requires the live combat state implementation.");
		}

		InvokeOriginalCombatStateRemoveCreature(
			concrete,
			target,
			unattach);
	}
}
