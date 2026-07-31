using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	public static void Install(Harmony harmony)
	{
		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Callable),
				nameof(Callable.CallDeferred),
				[typeof(Variant[])]),
			prefixName: nameof(DeferredCallablePrefix));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(EncounterModel),
				nameof(EncounterModel.GetNextSlot),
				[typeof(ICombatState)]),
			postfixName: nameof(GetNextSlotPostfix));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatState),
				nameof(CombatState.CreateCreature),
				[
					typeof(MonsterModel),
					typeof(CombatSide),
					typeof(string)
				]),
			prefixName: nameof(CreateCreaturePrefix),
			postfixName: nameof(CreateCreaturePostfix));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(NCreature),
				nameof(NCreature.Create),
				[typeof(Creature)]),
			postfixName: nameof(NCreatureCreatePostfix));

		PatchRequired(
			harmony,
			AccessTools.DeclaredMethod(
				typeof(CombatState),
				"AttachCreature",
				[typeof(Creature)]),
			postfixName: nameof(CombatStateAttachPostfix),
			finalizerName: nameof(CombatStateAttachFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.PropertySetter(
				typeof(Creature),
				nameof(Creature.CombatState)),
			postfixName: nameof(CombatStateSetterPostfix),
			finalizerName: nameof(CombatStateSetterFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CreatureCmd),
				nameof(CreatureCmd.Add),
				[typeof(Creature)]),
			prefixName: nameof(AddCommandPrefix),
			postfixName: nameof(AddCommandPostfix),
			finalizerName: nameof(AddCommandFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatState),
				nameof(CombatState.AddCreature),
				[typeof(Creature)]),
			prefixName: nameof(CombatStateAddPrefix),
			postfixName: nameof(AddLayerPostfix),
			finalizerName: nameof(AddLayerFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatState),
				nameof(CombatState.CreatureEscaped),
				[typeof(Creature)]),
			prefixName: nameof(CombatStateEscapePrefix),
			postfixName: nameof(CombatStateEscapePostfix),
			finalizerName: nameof(CombatStateEscapeFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.AddCreature),
				[typeof(Creature)]),
			prefixName: nameof(AddLayerPrefix),
			postfixName: nameof(AddLayerPostfix),
			finalizerName: nameof(AddLayerFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(NCombatRoom),
				nameof(NCombatRoom.AddCreature),
				[typeof(Creature)]),
			prefixName: nameof(AddLayerPrefix),
			postfixName: nameof(AddLayerPostfix),
			finalizerName: nameof(AddLayerFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.AfterCreatureAdded),
				[typeof(Creature)]),
			prefixName: nameof(AfterAddedPrefix),
			postfixName: nameof(AfterAddedPostfix),
			finalizerName: nameof(AfterAddedFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Hook),
				nameof(Hook.BeforeDeath)),
			prefixName: nameof(DirectContinuationTaskPrefix),
			finalizerName: nameof(DirectContinuationTaskFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Hook),
				nameof(Hook.AfterDeath)),
			prefixName: nameof(DirectContinuationTaskPrefix),
			finalizerName: nameof(DirectContinuationTaskFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Creature),
				nameof(Creature.InvokeDiedEvent),
				Type.EmptyTypes),
			prefixName: nameof(DirectContinuationEventPrefix),
			finalizerName: nameof(DirectContinuationEventFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Hook),
				nameof(Hook.AfterCreatureAddedToCombat),
				[typeof(ICombatState), typeof(Creature)]),
			prefixName: nameof(HookAfterAddedPrefix),
			postfixName: nameof(AfterAddedPostfix),
			finalizerName: nameof(AfterAddedFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Creature),
				nameof(Creature.HealInternal),
				[typeof(decimal)]),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Creature),
				nameof(Creature.SetCurrentHpInternal),
				[typeof(decimal)]),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.PropertySetter(
				typeof(Creature),
				nameof(Creature.CurrentHp)),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer),
			finalizerPriority: Priority.Last);

		PatchRequired(
			harmony,
			AccessTools.PropertyGetter(
				typeof(Creature),
				nameof(Creature.IsAlive)),
			postfixName: nameof(IsAlivePostfix));

		PatchRequired(
			harmony,
			AccessTools.PropertyGetter(
				typeof(Creature),
				nameof(Creature.IsDead)),
			postfixName: nameof(IsDeadPostfix));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(MonsterModel),
				nameof(MonsterModel.PerformMove)),
			prefixName: nameof(PerformMovePrefix),
			postfixName: nameof(PerformMovePostfix),
			finalizerName: nameof(PerformMoveFinalizer),
			finalizerPriority: Priority.Last);

		foreach (MethodInfo checkWinMethod in GetCheckWinMethods())
		{
			PatchRequired(
				harmony,
				checkWinMethod,
				prefixName: nameof(CheckWinCapturePrefix),
				finalizerName: nameof(CheckWinConditionFinalizer),
				prefixPriority: Priority.Last,
				finalizerPriority: Priority.Last);
			PatchRequired(
				harmony,
				checkWinMethod,
				prefixName: nameof(CheckWinGuardPrefix));
		}

		foreach (MethodInfo endCombatMethod in GetEndCombatMethods())
		{
			PatchRequired(
				harmony,
				endCombatMethod,
				finalizerName: nameof(EndCombatFinalizer),
				finalizerPriority: Priority.Last);
		}
	}

	private static bool DeferredCallablePrefix(
		Callable __instance,
		Variant[] args)
	{
		CausalScope? scope = ActiveScope.Value;
		if (scope == null || IsSchedulingDeferredContinuation.Value)
		{
			return true;
		}

		Variant[] capturedArgs = args.Length == 0
			? []
			: args.ToArray();
		Callable captured = __instance;
		Callable continuation = Callable.From(() =>
			InvokeDeferredContinuation(captured, capturedArgs, scope));

		lock (scope.Ledger.Gate)
		{
			scope.Lineage.AcquireContinuationLease();
		}

		IsSchedulingDeferredContinuation.Value = true;
		try
		{
			continuation.CallDeferred();
		}
		catch
		{
			ReleaseContinuationLease(scope);
			throw;
		}
		finally
		{
			IsSchedulingDeferredContinuation.Value = false;
		}
		return false;
	}

	private static void InvokeDeferredContinuation(
		Callable callable,
		Variant[] args,
		CausalScope scope)
	{
		if (ShouldSuppressDeferredContinuation(scope))
		{
			ReleaseContinuationLease(scope);
			if (scope.Parent.Evidence.CreatureRef is Creature parent)
			{
				LineageBinding binding = BindMember(
					scope.Ledger,
					scope.Lineage,
					scope.Parent,
					parent);
				ScheduleRestabilization(binding);
			}
			return;
		}

		CausalScope? previous = ActiveScope.Value;
		ActiveScope.Value = scope;
		try
		{
			callable.Call(args);
		}
		finally
		{
			ActiveScope.Value = previous;
			ReleaseContinuationLease(scope);
		}
	}

	private static bool ShouldSuppressDeferredContinuation(
		CausalScope scope)
	{
		ManagerCombatSnapshot managerState = ReadManagerSnapshot(
			CombatManager.Instance,
			invocationTurnState: null);
		bool completionArmed;
		ErasureLineageMember[] members;
		lock (scope.Ledger.Gate)
		{
			completionArmed = scope.Ledger.CompletionArmed;
			members = scope.Lineage.Members.ToArray();
		}

		bool lineageConverged = members.All(member =>
			member.Evidence.CreatureRef is Creature creature
			&& ReadLayerState(scope.Ledger, creature).IsConverged);
		bool hasLivingPlayer = scope.Ledger.CombatState.Players.Any(
			player => ReadRawHp(player.Creature) > 0
				|| player.Creature.IsAlive);
		DeferredContinuationSnapshot snapshot = new(
			IsExpectedCombat:
				managerState.IsCurrentInvocation
				&& ReferenceEquals(
					managerState.CombatState,
					scope.Ledger.CombatState)
				&& (scope.Ledger.CombatEpoch == null
					|| ReferenceEquals(
						managerState.TurnState,
						scope.Ledger.CombatEpoch)),
			IsInProgress: managerState.IsInProgress,
			IsStarting: managerState.IsStarting,
			HasPendingLoss: managerState.HasPendingLoss,
			HasLivingPlayer: hasLivingPlayer,
			IsCompletionArmed: completionArmed,
			IsLineageConverged: lineageConverged);
		return ErasureCompletionPolicy
			.ShouldSuppressDeferredContinuation(snapshot);
	}

	private static void GetNextSlotPostfix(
		ICombatState combatState,
		string? __result)
	{
		ActiveSlotAllocation.Value = string.IsNullOrEmpty(__result)
			? null
			: new SlotAllocationTicket(combatState, __result);
	}

	private static void CreateCreaturePrefix(
		CombatState __instance,
		object[] __args,
		out SlotAllocationTicket? __state)
	{
		SlotAllocationTicket? ticket = ActiveSlotAllocation.Value;
		ActiveSlotAllocation.Value = null;
		string? requestedSlot = __args.Length >= 3
			? __args[2] as string
			: null;
		__state = ticket != null
			&& ReferenceEquals(ticket.CombatState, __instance)
			&& string.Equals(
				ticket.SlotName,
				requestedSlot,
				StringComparison.Ordinal)
			? ticket
			: null;
	}

	private static void CreateCreaturePostfix(
		CombatState __instance,
		SlotAllocationTicket? __state,
		Creature? __result)
	{
		if (__result == null)
		{
			return;
		}

		if (__state != null)
		{
			GenericSlotOrigins.GetValue(
				__result,
				_ => GenericSlotOrigin.Instance);
		}

		if (TryTrackCandidate(
			__instance,
			__result,
			ErasureMutationKind.Created,
			out LineageBinding? binding))
		{
			ScheduleRestabilization(binding);
		}
	}

	private static void DirectContinuationTaskPrefix(
		ICombatState combatState,
		Creature creature,
		out DirectCausalInvocation __state)
	{
		__state = BeginDirectCausalInvocation(combatState, creature);
	}

	private static Exception? DirectContinuationTaskFinalizer(
		ref Task? __result,
		DirectCausalInvocation __state,
		Exception? __exception)
	{
		if (!__state.WasEntered)
		{
			return __exception;
		}

		ActiveScope.Value = __state.Previous;
		if (__state.Scope == null)
		{
			return __exception;
		}

		if (__result == null)
		{
			ReleaseContinuationLease(__state.Scope);
		}
		else
		{
			__result = CloseDirectCausalScopeAfter(
				__result,
				__state.Scope);
		}
		return __exception;
	}

	private static void DirectContinuationEventPrefix(
		Creature __instance,
		out DirectCausalInvocation __state)
	{
		__state = BeginDirectCausalInvocation(
			ReadAttachedCombat(__instance),
			__instance);
	}

	private static Exception? DirectContinuationEventFinalizer(
		DirectCausalInvocation __state,
		Exception? __exception)
	{
		if (!__state.WasEntered)
		{
			return __exception;
		}

		if (__state.Scope != null)
		{
			ReleaseContinuationLease(__state.Scope);
		}
		ActiveScope.Value = __state.Previous;
		return __exception;
	}

	private static DirectCausalInvocation BeginDirectCausalInvocation(
		ICombatState? combatState,
		Creature source)
	{
		CausalScope? previous = ActiveScope.Value;
		CausalScope? scope = null;
		if (TryGetBinding(source, out LineageBinding? binding)
			&& (combatState == null
				|| ReferenceEquals(
					combatState,
					binding.Ledger.CombatState)))
		{
			lock (binding.Ledger.Gate)
			{
				binding.Lineage.AcquireContinuationLease();
			}
			scope = new CausalScope(
				binding.Ledger,
				binding.Lineage,
				binding.Member);
		}

		ActiveScope.Value = scope;
		return new DirectCausalInvocation(
			WasEntered: true,
			previous,
			scope);
	}

	private static async Task CloseDirectCausalScopeAfter(
		Task original,
		CausalScope scope)
	{
		try
		{
			await original;
		}
		finally
		{
			ReleaseContinuationLease(scope);
		}
	}

	private static void ReleaseContinuationLease(CausalScope scope)
	{
		lock (scope.Ledger.Gate)
		{
			scope.Lineage.ReleaseContinuationLease();
		}
	}

	private static void NCreatureCreatePostfix(
		Creature entity,
		NCreature? __result)
	{
		if (__result == null)
		{
			return;
		}

		if (!TryTrackCandidate(
			ReadAttachedCombat(entity),
			entity,
			ErasureMutationKind.NodeCreated,
			out LineageBinding? binding))
		{
			return;
		}

		CaptureNode(binding.Ledger, entity, __result);
		SettleLineage(binding);
		ScheduleRestabilization(binding);
	}

	private static void CombatStateAttachPostfix(
		CombatState __instance,
		Creature creature)
	{
		if (TryTrackCandidate(
			__instance,
			creature,
			ErasureMutationKind.Attached,
			out LineageBinding? binding))
		{
			SettleLineage(binding);
			ScheduleRestabilization(binding);
		}
	}

	private static Exception? CombatStateAttachFinalizer(
		CombatState __instance,
		Creature creature,
		Exception? __exception)
	{
		CombatStateAttachPostfix(__instance, creature);
		return __exception;
	}

	private static void CombatStateSetterPostfix(Creature __instance)
	{
		if (TryGetBinding(__instance, out LineageBinding? binding))
		{
			ConvergeMember(binding);
			ScheduleRestabilization(binding);
		}
	}

	private static Exception? CombatStateSetterFinalizer(
		Creature __instance,
		Exception? __exception)
	{
		CombatStateSetterPostfix(__instance);
		return __exception;
	}

	private static bool AddCommandPrefix(
		Creature creature,
		ref Task __result)
	{
		if (!TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		__result = Task.CompletedTask;
		return false;
	}

	private static void AddCommandPostfix(
		Creature creature,
		ref Task __result)
	{
		if (!TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return;
		}

		__result = FinishPatchedTaskAndSettle(
			__result ?? Task.CompletedTask,
			binding);
	}

	private static Exception? AddCommandFinalizer(
		Creature creature,
		ref Task __result,
		Exception? __exception)
	{
		if (!TryGetBinding(creature, out LineageBinding? binding))
		{
			return __exception;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		__result ??= Task.CompletedTask;
		if (__exception != null)
		{
			Log.Warn(
				$"[{ModInfo.Id}] Suppressed a failed re-add of erased " +
				$"creature {SafeModelId(creature)}: " +
				$"{__exception.GetBaseException().Message}");
		}
		return null;
	}

	private static bool CombatStateAddPrefix(
		CombatState __instance,
		Creature creature)
	{
		if (!TryTrackCandidate(
			__instance,
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		return false;
	}

	private static bool CombatStateEscapePrefix(
		CombatState __instance,
		Creature creature)
	{
		if (!TryTrackCandidate(
			__instance,
			creature,
			ErasureMutationKind.Reentered,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		return false;
	}

	private static void CombatStateEscapePostfix(
		CombatState __instance,
		Creature creature)
	{
		if (TryTrackCandidate(
			__instance,
			creature,
			ErasureMutationKind.Reentered,
			out LineageBinding? binding))
		{
			SettleLineage(binding);
			ScheduleRestabilization(binding);
		}
	}

	private static Exception? CombatStateEscapeFinalizer(
		CombatState __instance,
		Creature creature,
		Exception? __exception)
	{
		CombatStateEscapePostfix(__instance, creature);
		return __exception;
	}

	private static bool AddLayerPrefix(Creature creature)
	{
		if (!TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		return false;
	}

	private static void AddLayerPostfix(Creature creature)
	{
		if (TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			SettleLineage(binding);
			ScheduleRestabilization(binding);
		}
	}

	private static Exception? AddLayerFinalizer(
		Creature creature,
		Exception? __exception)
	{
		AddLayerPostfix(creature);
		return __exception;
	}

	private static bool AfterAddedPrefix(
		Creature creature,
		ref Task __result)
	{
		if (!TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		__result = Task.CompletedTask;
		return false;
	}

	private static bool HookAfterAddedPrefix(
		ICombatState combatState,
		Creature creature,
		ref Task __result)
	{
		if (!TryTrackCandidate(
			combatState,
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return true;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		__result = Task.CompletedTask;
		return false;
	}

	private static void AfterAddedPostfix(
		Creature creature,
		ref Task __result)
	{
		if (!TryTrackCandidate(
			ReadAttachedCombat(creature),
			creature,
			ErasureMutationKind.Added,
			out LineageBinding? binding))
		{
			return;
		}

		__result = FinishPatchedTaskAndSettle(
			__result ?? Task.CompletedTask,
			binding);
	}

	private static Exception? AfterAddedFinalizer(
		Creature creature,
		ref Task __result,
		Exception? __exception)
	{
		if (!TryGetBinding(creature, out LineageBinding? binding))
		{
			return __exception;
		}

		SettleLineage(binding);
		ScheduleRestabilization(binding);
		__result ??= Task.CompletedTask;
		if (__exception != null)
		{
			Log.Warn(
				$"[{ModInfo.Id}] Suppressed a failed continuation setup " +
				$"for erased creature {SafeModelId(creature)}: " +
				$"{__exception.GetBaseException().Message}");
		}
		return null;
	}

	private static bool BlockHpMutationPrefix(Creature __instance)
	{
		if (!TryGetBinding(__instance, out _))
		{
			return true;
		}

		SetRawHpZero(__instance);
		return false;
	}

	private static void BlockHpMutationPostfix(Creature __instance)
	{
		if (TryGetBinding(__instance, out _))
		{
			SetRawHpZero(__instance);
		}
	}

	private static Exception? BlockHpMutationFinalizer(
		Creature __instance,
		Exception? __exception)
	{
		BlockHpMutationPostfix(__instance);
		return __exception;
	}

	private static void IsAlivePostfix(
		Creature __instance,
		ref bool __result)
	{
		if (TryGetBinding(__instance, out _))
		{
			__result = false;
		}
	}

	private static void IsDeadPostfix(
		Creature __instance,
		ref bool __result)
	{
		if (TryGetBinding(__instance, out _))
		{
			__result = true;
		}
	}

	private static bool PerformMovePrefix(
		MonsterModel __instance,
		ref Task __result)
	{
		Creature creature;
		try
		{
			creature = __instance.Creature;
		}
		catch
		{
			return true;
		}

		if (!TryGetBinding(creature, out _))
		{
			return true;
		}

		StopMonsterExecution(creature);
		__result = Task.CompletedTask;
		return false;
	}

	private static void PerformMovePostfix(
		MonsterModel __instance,
		ref Task __result)
	{
		if (!TryGetMonsterBinding(
			__instance,
			out LineageBinding? binding)
			|| binding == null)
		{
			return;
		}

		__result = FinishPatchedTaskAndSettle(
			__result ?? Task.CompletedTask,
			binding);
	}

	private static Exception? PerformMoveFinalizer(
		MonsterModel __instance,
		ref Task __result,
		Exception? __exception)
	{
		if (TryGetMonsterBinding(
			__instance,
			out LineageBinding? binding)
			&& binding != null)
		{
			StopMonsterExecution(binding.Creature);
			SettleLineage(binding);
			ScheduleRestabilization(binding);
			__result ??= Task.CompletedTask;
		}
		return __exception;
	}

	private static bool TryGetMonsterBinding(
		MonsterModel monster,
		out LineageBinding? binding)
	{
		try
		{
			return TryGetBinding(monster.Creature, out binding);
		}
		catch
		{
			binding = null;
			return false;
		}
	}

	private static void CheckWinCapturePrefix(
		CombatManager __instance,
		out CheckWinInvocation __state)
	{
		__state = CaptureCheckWinInvocation(
			__instance,
			invocationTurnState: null);
	}

	private static bool CheckWinGuardPrefix(
		CombatManager __instance,
		object[] __args,
		ref Task<bool> __result)
	{
		object? invocationTurnState = __args.Length == 1
			? __args[0]
			: null;
		CheckWinInvocation invocation = CaptureCheckWinInvocation(
			__instance,
			invocationTurnState);
		CombatLedger? ledger = invocation.Ledger;
		if (!invocation.WasCurrentAtEntry || ledger == null)
		{
			return true;
		}
		lock (ledger.Gate)
		{
			if (ledger.Lineages.Count == 0)
			{
				return true;
			}
		}

		SettleLedger(ledger);
		ScheduleUncertifiedLineages(ledger);
		ManagerCombatSnapshot snapshot = ReadManagerSnapshot(
			__instance,
			invocationTurnState);
		if (snapshot.HasPendingLoss)
		{
			return true;
		}

		__result = Task.FromResult(false);
		return false;
	}

	private static CheckWinInvocation CaptureCheckWinInvocation(
		CombatManager manager,
		object? invocationTurnState)
	{
		ManagerCombatSnapshot snapshot = ReadManagerSnapshot(
			manager,
			invocationTurnState);
		CombatLedger? ledger = null;
		if (snapshot.IsCurrentInvocation
			&& snapshot.CombatState != null)
		{
			Ledgers.TryGetValue(snapshot.CombatState, out ledger);
		}
		return new CheckWinInvocation(
			snapshot.TurnState,
			snapshot.CombatState,
			ledger,
			snapshot.IsCurrentInvocation);
	}

	private static Exception? CheckWinConditionFinalizer(
		CombatManager __instance,
		object[] __args,
		CheckWinInvocation? __state,
		bool __runOriginal,
		ref Task<bool> __result,
		Exception? __exception)
	{
		if (__exception == null)
		{
			CheckWinInvocation? invocation =
				ResolveCheckWinInvocation(
					__instance,
					__args,
					__state);
			__result = FinishCheckWinCondition(
				__instance,
				invocation,
				__runOriginal,
				__result ?? Task.FromResult(false));
		}
		return __exception;
	}

	private static CheckWinInvocation? ResolveCheckWinInvocation(
		CombatManager manager,
		object[] arguments,
		CheckWinInvocation? captured)
	{
		object? invocationTurnState = arguments.Length == 1
			? arguments[0]
			: null;
		if (captured != null)
		{
			if (invocationTurnState != null
				&& !ReferenceEquals(
					invocationTurnState,
					captured.TurnState))
			{
				return null;
			}
			return captured;
		}

		CheckWinInvocation rebuilt = CaptureCheckWinInvocation(
			manager,
			invocationTurnState);
		return rebuilt.WasCurrentAtEntry ? rebuilt : null;
	}

	private static async Task FinishPatchedTaskAndSettle(
		Task patchedTask,
		LineageBinding binding)
	{
		try
		{
			await patchedTask;
		}
		finally
		{
			SettleLineage(binding);
			ScheduleRestabilization(binding);
		}
	}

	private static Exception? EndCombatFinalizer(
		MethodBase __originalMethod,
		bool __runOriginal,
		ref Task __result,
		Exception? __exception)
	{
		EndCombatAttempt? attempt = ActiveEndCombatAttempt.Value;
		if (attempt != null)
		{
			if (IsSettlementEndMethod(__originalMethod))
			{
				attempt.LeafObserved = true;
				attempt.LeafOriginalRan |= __runOriginal;
			}
			if (!__runOriginal && __exception == null)
			{
				__result ??= Task.CompletedTask;
			}
		}
		return __exception;
	}

	private static IReadOnlyList<MethodInfo> GetCheckWinMethods()
	{
		MethodInfo noArgument = AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.CheckWinCondition),
				Type.EmptyTypes)
			?? throw new MissingMethodException(
				typeof(CombatManager).FullName,
				$"{nameof(CombatManager.CheckWinCondition)}()");
		List<MethodInfo> methods = [noArgument];

#if STS2_110_0
		MethodInfo[] turnStateOverloads = AccessTools
			.GetDeclaredMethods(typeof(CombatManager))
			.Where(method =>
				method.Name == nameof(CombatManager.CheckWinCondition)
				&& method.ReturnType == typeof(Task<bool>)
				&& method.GetParameters() is [ParameterInfo parameter]
				&& parameter.ParameterType.FullName
					== "MegaCrit.Sts2.Core.Combat.CombatTurnState")
			.ToArray();
		if (turnStateOverloads.Length != 1)
		{
			throw new MissingMethodException(
				"Expected exactly one CombatTurnState win-check overload.");
		}
		methods.Add(turnStateOverloads[0]);
#endif

		return methods;
	}

	private static IReadOnlyList<MethodInfo> GetEndCombatMethods()
	{
		MethodInfo noArgument = AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.EndCombatInternal),
				Type.EmptyTypes)
			?? throw new MissingMethodException(
				typeof(CombatManager).FullName,
				$"{nameof(CombatManager.EndCombatInternal)}()");
		List<MethodInfo> methods = [noArgument];

#if STS2_110_0
		MethodInfo[] turnStateOverloads = AccessTools
			.GetDeclaredMethods(typeof(CombatManager))
			.Where(method =>
				method.Name == nameof(CombatManager.EndCombatInternal)
				&& method.ReturnType == typeof(Task)
				&& method.GetParameters() is [ParameterInfo parameter]
				&& parameter.ParameterType.FullName
					== "MegaCrit.Sts2.Core.Combat.CombatTurnState")
			.ToArray();
		if (turnStateOverloads.Length != 1)
		{
			throw new MissingMethodException(
				"Expected exactly one CombatTurnState settlement overload.");
		}
		methods.Add(turnStateOverloads[0]);
#endif

		return methods;
	}

	private static bool IsSettlementEndMethod(MethodBase method)
	{
#if STS2_107_1
		return method.GetParameters().Length == 0;
#elif STS2_110_0
		return method.GetParameters() is [ParameterInfo parameter]
			&& parameter.ParameterType.FullName
				== "MegaCrit.Sts2.Core.Combat.CombatTurnState";
#endif
	}

	private static void PatchRequired(
		Harmony harmony,
		MethodInfo? original,
		string? prefixName = null,
		string? postfixName = null,
		string? finalizerName = null,
		int prefixPriority = ErasurePatchPriority,
		int postfixPriority = ErasurePatchPriority,
		int finalizerPriority = ErasurePatchPriority)
	{
		if (original == null)
		{
			throw new MissingMethodException(
				$"A required STS2 method for {nameof(ErasureKill)} was not found.");
		}

		harmony.Patch(
			original,
			prefix: CreateHarmonyMethod(prefixName, prefixPriority),
			postfix: CreateHarmonyMethod(postfixName, postfixPriority),
			finalizer: CreateHarmonyMethod(
				finalizerName,
				finalizerPriority));
	}

	private static HarmonyMethod? CreateHarmonyMethod(
		string? methodName,
		int priority)
	{
		if (methodName == null)
		{
			return null;
		}

		return new HarmonyMethod(typeof(ErasureKill), methodName)
		{
			priority = priority
		};
	}
}
