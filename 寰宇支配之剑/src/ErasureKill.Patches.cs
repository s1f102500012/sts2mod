using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	[ErasureBoundary(
		ErasurePatchContract.ThirdPartyInteroperability,
		ErasurePatchContract.FailClosedCompatibility)]
	public static void Install(Harmony harmony)
	{
		PatchRequired(
			harmony,
			GetKillStateMachineMoveNext(),
			transpilerName: nameof(ErasureDeathPipelineTranspiler));
		PatchCanonicalDeathEntry(harmony);
		PatchCanonicalSettlementEntry(harmony);
		PatchRequired(
			harmony,
			GetCombatProgressSetter(),
			postfixName: nameof(CanonicalSettlementProgressPostfix),
			finalizerName: nameof(CanonicalSettlementProgressFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CreatureCmd),
				nameof(CreatureCmd.Kill),
				[typeof(Creature), typeof(bool)]),
			prefixName: nameof(TerminalPlayerKillPrefix));
		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CreatureCmd),
				nameof(CreatureCmd.Kill),
				[typeof(IReadOnlyCollection<Creature>), typeof(bool)]),
			prefixName: nameof(TerminalPlayerCollectionKillPrefix));

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
				typeof(CombatState),
				nameof(CombatState.CreateCreature),
				[
					typeof(MonsterModel),
					typeof(CombatSide),
					typeof(string)
				]),
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
			finalizerName: nameof(CombatStateAttachFinalizer));

		PatchRequired(
			harmony,
			AccessTools.PropertySetter(
				typeof(Creature),
				nameof(Creature.CombatState)),
			postfixName: nameof(CombatStateSetterPostfix),
			finalizerName: nameof(CombatStateSetterFinalizer));

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
			finalizerName: nameof(AddLayerFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatState),
				nameof(CombatState.CreatureEscaped),
				[typeof(Creature)]),
			prefixName: nameof(CombatStateEscapePrefix),
			postfixName: nameof(CombatStateEscapePostfix),
			finalizerName: nameof(CombatStateEscapeFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.AddCreature),
				[typeof(Creature)]),
			prefixName: nameof(AddLayerPrefix),
			postfixName: nameof(AddLayerPostfix),
			finalizerName: nameof(AddLayerFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(NCombatRoom),
				nameof(NCombatRoom.AddCreature),
				[typeof(Creature)]),
			prefixName: nameof(AddLayerPrefix),
			postfixName: nameof(AddLayerPostfix),
			finalizerName: nameof(AddLayerFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(CombatManager),
				nameof(CombatManager.AfterCreatureAdded),
				[typeof(Creature)]),
			prefixName: nameof(AfterAddedPrefix),
			postfixName: nameof(AfterAddedPostfix),
			finalizerName: nameof(AfterAddedFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Hook),
				nameof(Hook.AfterCreatureAddedToCombat),
				[typeof(ICombatState), typeof(Creature)]),
			prefixName: nameof(HookAfterAddedPrefix),
			postfixName: nameof(AfterAddedPostfix),
			finalizerName: nameof(AfterAddedFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Creature),
				nameof(Creature.HealInternal),
				[typeof(decimal)]),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer));

		PatchRequired(
			harmony,
			AccessTools.Method(
				typeof(Creature),
				nameof(Creature.SetCurrentHpInternal),
				[typeof(decimal)]),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer));

		PatchRequired(
			harmony,
			AccessTools.PropertySetter(
				typeof(Creature),
				nameof(Creature.CurrentHp)),
			prefixName: nameof(BlockHpMutationPrefix),
			postfixName: nameof(BlockHpMutationPostfix),
			finalizerName: nameof(BlockHpMutationFinalizer));

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
			finalizerName: nameof(PerformMoveFinalizer));

		foreach (MethodInfo checkWinMethod in GetCheckWinMethods())
		{
			PatchRequired(
				harmony,
				checkWinMethod,
				prefixName: nameof(CheckWinCapturePrefix),
				finalizerName: nameof(CheckWinConditionFinalizer));
			PatchRequired(
				harmony,
				checkWinMethod,
				prefixName: nameof(CheckWinGuardPrefix));
		}

		PatchRequired(
			harmony,
			AccessTools.DeclaredMethod(
				typeof(ActionExecutor),
				"AfterActionFinished",
				[typeof(GameAction)]),
			postfixName: nameof(ActionFinishedPostfix));

	}

	private static void ActionFinishedPostfix(GameAction action)
	{
		if (action.State != GameActionState.Finished
			|| !PendingActionSettlements.TryGetValue(
				action,
				out CombatLedger? ledger))
		{
			return;
		}

		PendingActionSettlements.Remove(action);
		Log.Info(
			$"[{ModInfo.Id}] Reached the game-action boundary; " +
			"starting normal combat completion.");
		_ = RequestImmediateCombatCompletion(ledger);
	}

	private static bool DeferredCallablePrefix(
		Callable __instance,
		Variant[] args)
	{
		CausalScope? scope = ActiveScope.Value;
		if (scope == null || IsSchedulingCausalCallback.Value)
		{
			return true;
		}

		Callable original = __instance;
		Variant[] capturedArgs = args.Length == 0
			? []
			: args.ToArray();
		Callable continuation = Callable.From(() =>
			InvokeCausalCallback(original, capturedArgs, scope));
		IsSchedulingCausalCallback.Value = true;
		try
		{
			continuation.CallDeferred();
		}
		finally
		{
			IsSchedulingCausalCallback.Value = false;
		}
		return false;
	}

	private static void InvokeCausalCallback(
		Callable callable,
		Variant[] args,
		CausalScope scope)
	{
		if (!ShouldExecuteCausalCallback(scope))
		{
			return;
		}

		CausalScope? previous = ActiveScope.Value;
		bool wasScheduling = IsSchedulingCausalCallback.Value;
		ActiveScope.Value = scope;
		IsSchedulingCausalCallback.Value = false;
		try
		{
			try
			{
				callable.Call(args);
			}
			catch (InvalidOperationException exception)
				when (IsUnsupportedTaskReturnConversion(exception))
			{
			}
		}
		finally
		{
			IsSchedulingCausalCallback.Value = wasScheduling;
			ActiveScope.Value = previous;
		}
	}

	private static bool ShouldExecuteCausalCallback(CausalScope scope)
	{
		CombatLedger ledger = scope.Ledger;
		ManagerCombatSnapshot managerState = ReadManagerSnapshot(
			CombatManager.Instance,
			invocationTurnState: null);
		bool terminalSealed;
		bool terminalBarrierArmed;
		bool completionRunning;
		bool lineageCertified;
		lock (ledger.Gate)
		{
			terminalSealed = ledger.TerminalSealed;
			terminalBarrierArmed = ledger.TerminalBarrierArmed;
			completionRunning = ledger.CompletionDisposition
				== CompletionDisposition.Running;
			lineageCertified = scope.Lineage.TryGetCompletionCertificate(
				out _);
		}

		ErasureDeferredCallbackSnapshot snapshot = new(
			HasTrackedScope: true,
			IsExpectedCombat:
				managerState.IsCurrentInvocation
					&& ReferenceEquals(
						managerState.CombatState,
						ledger.CombatState)
					&& (ledger.CombatEpoch == null
						|| ReferenceEquals(
							managerState.TurnState,
							ledger.CombatEpoch)),
			IsInProgress: managerState.IsInProgress,
			IsTerminalBarrierArmed: terminalBarrierArmed,
			IsTerminalSealed: terminalSealed,
			IsCompletionFlightRunning: completionRunning,
			IsLineageCertified: lineageCertified);
		ErasureDeferredCallbackDecision decision =
			ErasureDeferredCallbackPolicy.Evaluate(snapshot);
		if (ErasureDeferredCallbackPolicy.ShouldExecute(snapshot))
		{
			return true;
		}

		lock (ledger.Gate)
		{
			if (ledger.LoggedDiscardedDeferredCallback)
			{
				return false;
			}
			ledger.LoggedDiscardedDeferredCallback = true;
		}
		Log.Info(
			$"[{ModInfo.Id}] Discarded a deferred callback from completed " +
			$"erasure operation {scope.Lineage.OperationSequence}; " +
			$"reason={decision}.");
		return false;
	}

	private static bool IsUnsupportedTaskReturnConversion(
		InvalidOperationException exception)
	{
		return exception.Message.Contains(
			"not supported for conversion to/from Variant",
			StringComparison.Ordinal)
			&& exception.Message.Contains(
				"System.Threading.Tasks.Task",
				StringComparison.Ordinal);
	}

	private static void CreateCreaturePostfix(
		CombatState __instance,
		Creature? __result)
	{
		if (__result == null)
		{
			return;
		}
		if (TryQuarantineTerminalIngress(__instance, __result))
		{
			return;
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

	private static void NCreatureCreatePostfix(
		Creature entity,
		NCreature? __result)
	{
		if (__result == null)
		{
			return;
		}
		if (TryQuarantineTerminalNode(entity, __result))
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
		if (TryQuarantineTerminalIngress(__instance, creature))
		{
			return;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(__instance),
			__instance))
		{
			return;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return false;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return null;
		}
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
		if (TryQuarantineTerminalIngress(__instance, creature))
		{
			return false;
		}
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
		if (TryQuarantineTerminalIngress(__instance, creature))
		{
			return false;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			return false;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			return;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return false;
		}
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
		if (TryQuarantineTerminalIngress(combatState, creature))
		{
			__result = CreateTerminalIngressCompletion();
			return false;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(creature),
			creature))
		{
			__result = CreateTerminalIngressCompletion();
			return null;
		}
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
		if (TryQuarantineTerminalIngress(
			ReadAttachedCombat(__instance),
			__instance))
		{
			return false;
		}
		if (!IsErasedOrTerminalIngress(__instance))
		{
			return true;
		}

		SetRawHpZero(__instance);
		return false;
	}

	private static void BlockHpMutationPostfix(Creature __instance)
	{
		TryQuarantineTerminalIngress(
			ReadAttachedCombat(__instance),
			__instance);
		if (IsErasedOrTerminalIngress(__instance))
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
		TryQuarantineTerminalIngress(
			ReadAttachedCombat(__instance),
			__instance);
		if (IsErasedOrTerminalIngress(__instance))
		{
			__result = false;
		}
	}

	private static void IsDeadPostfix(
		Creature __instance,
		ref bool __result)
	{
		TryQuarantineTerminalIngress(
			ReadAttachedCombat(__instance),
			__instance);
		if (IsErasedOrTerminalIngress(__instance))
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

		if (!IsErasedOrTerminalIngress(creature))
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

	private static bool TerminalPlayerKillPrefix(
		Creature creature,
		ref Task __result)
	{
		if (!ShouldRejectTerminalPlayerKill(creature, out CombatLedger ledger))
		{
			return true;
		}

		LogRejectedTerminalLossOnce(ledger);
		__result = Task.CompletedTask;
		return false;
	}

	private static bool TerminalPlayerCollectionKillPrefix(
		IReadOnlyCollection<Creature> creatures,
		ref Task __result)
	{
		CombatLedger? ledger = null;
		if (creatures.Count == 0)
		{
			return true;
		}
		foreach (Creature creature in creatures)
		{
			if (!ShouldRejectTerminalPlayerKill(
					creature,
					out CombatLedger candidate)
				|| (ledger != null && !ReferenceEquals(ledger, candidate)))
			{
				return true;
			}
			ledger = candidate;
		}

		LogRejectedTerminalLossOnce(ledger!);
		__result = Task.CompletedTask;
		return false;
	}

	private static bool ShouldRejectTerminalPlayerKill(
		Creature creature,
		out CombatLedger ledger)
	{
		ICombatState? combatState = ReadAttachedCombat(creature);
		ledger = null!;
		if (combatState == null
			|| !Ledgers.TryGetValue(
				combatState,
				out CombatLedger? candidate)
			|| candidate == null
			|| creature.Player == null)
		{
			return false;
		}
		ledger = candidate;

		lock (ledger.Gate)
		{
			return ErasureParticipationPolicy.RejectContradictoryLoss(
				ledger.TerminalBarrierPhase,
				ledger.CompletionDisposition
					== CompletionDisposition.Running,
				isPlayerCreature: true);
		}
	}

	private static void LogRejectedTerminalLossOnce(CombatLedger ledger)
	{
		lock (ledger.Gate)
		{
			if (ledger.LoggedTerminalLossAttempt)
			{
				return;
			}
			ledger.LoggedTerminalLossAttempt = true;
		}
		Log.Info(
			$"[{ModInfo.Id}] Ignored a contradictory player-death request " +
			"during committed combat victory settlement.");
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
			if (ledger.Lineages.Count == 0
				|| ledger.ActiveTerminationCount == 0)
			{
				return true;
			}
		}
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
		string? transpilerName = null)
	{
		if (original == null)
		{
			throw new MissingMethodException(
				$"A required STS2 method for {nameof(ErasureKill)} was not found.");
		}

		harmony.Patch(
			original,
			prefix: CreateHarmonyMethod(prefixName),
			postfix: CreateHarmonyMethod(postfixName),
			transpiler: CreateHarmonyMethod(transpilerName),
			finalizer: CreateHarmonyMethod(finalizerName));
	}

	private static HarmonyMethod? CreateHarmonyMethod(string? methodName)
	{
		if (methodName == null)
		{
			return null;
		}

		return new HarmonyMethod(typeof(ErasureKill), methodName);
	}

	private static void PatchCanonicalDeathEntry(Harmony harmony)
	{
		PatchOriginalPrimitive(
			harmony,
			GetKillWithoutCheckingWinCondition(),
			nameof(InvokeOriginalKillWithoutCheckingWinCondition));
		PatchOriginalPrimitive(
			harmony,
			RequireMethod(
				typeof(NCombatRoom),
				nameof(NCombatRoom.RemoveCreatureNode),
				[typeof(NCreature)]),
			nameof(InvokeOriginalRemoveCreatureNode));
		PatchOriginalPrimitive(
			harmony,
			RequireMethod(
				typeof(CombatManager),
				nameof(CombatManager.RemoveCreature),
				[typeof(Creature)]),
			nameof(InvokeOriginalCombatManagerRemoveCreature));
		PatchOriginalPrimitive(
			harmony,
			RequireMethod(
				typeof(CombatState),
				nameof(CombatState.RemoveCreature),
				[typeof(Creature), typeof(bool)]),
			nameof(InvokeOriginalCombatStateRemoveCreature));
	}

	private static void PatchOriginalPrimitive(
		Harmony harmony,
		MethodInfo original,
		string standinName)
	{
		MethodInfo? patched = harmony.CreateReversePatcher(
				original,
				new HarmonyMethod(typeof(ErasureKill), standinName))
			.Patch(HarmonyReversePatchType.Original);
		if (patched != null)
		{
			return;
		}

		throw new MissingMethodException(
			$"The canonical primitive {original.DeclaringType?.Name}." +
			$"{original.Name} could not be initialized.");
	}
}
