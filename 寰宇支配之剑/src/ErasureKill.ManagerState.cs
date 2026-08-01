using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
#if STS2_107_1
	private static readonly FieldInfo LegacyManagerStateField = RequireField(
		typeof(CombatManager),
		"_state");

	private static readonly FieldInfo LegacyManagerInProgressField =
		RequireField(
			typeof(CombatManager),
			"<IsInProgress>k__BackingField");

	private static readonly FieldInfo LegacyManagerStartingField =
		RequireField(
			typeof(CombatManager),
			"<IsStarting>k__BackingField");

	private static readonly FieldInfo LegacyManagerPendingLossField =
		RequireField(
			typeof(CombatManager),
			"_pendingLoss");
#elif STS2_110_0
	private static readonly FieldInfo ManagerTurnStateField = RequireField(
		typeof(CombatManager),
		"_turnState");

	private static readonly FieldInfo TurnStateCombatStateField = RequireField(
		ManagerTurnStateField.FieldType,
		"<State>k__BackingField");

	private static readonly FieldInfo TurnStateInProgressField = RequireField(
		ManagerTurnStateField.FieldType,
		"<IsInProgress>k__BackingField");

	private static readonly FieldInfo TurnStateStartingField = RequireField(
		ManagerTurnStateField.FieldType,
		"<IsStarting>k__BackingField");

	private static readonly FieldInfo TurnStatePendingLossField = RequireField(
		ManagerTurnStateField.FieldType,
		"<PendingLoss>k__BackingField");
#else
#error Unsupported Slay the Spire 2 compatibility target.
#endif

	private static ICombatState? ReadManagerCombatState(
		CombatManager manager)
	{
#if STS2_107_1
		return LegacyManagerStateField.GetValue(manager) as ICombatState;
#elif STS2_110_0
		object? turnState = ManagerTurnStateField.GetValue(manager);
		return turnState == null
			? null
			: TurnStateCombatStateField.GetValue(turnState) as ICombatState;
#endif
	}

	private static MethodInfo GetCombatProgressSetter()
	{
#if STS2_107_1
		return AccessTools.PropertySetter(
				typeof(CombatManager),
				nameof(CombatManager.IsInProgress))
			?? throw new MissingMethodException(
				typeof(CombatManager).FullName,
				"set_IsInProgress");
#elif STS2_110_0
		return AccessTools.PropertySetter(
				ManagerTurnStateField.FieldType,
				"IsInProgress")
			?? throw new MissingMethodException(
				ManagerTurnStateField.FieldType.FullName,
				"set_IsInProgress");
#endif
	}

	private static void CanonicalSettlementProgressPostfix(
		bool __0)
	{
		if (!__0
			&& ActiveCompletionEvaluation.Value is CombatLedger ledger
			&& TryGetSettlementProgressOwner(ledger, out object? owner))
		{
			SetSettlementProgressRaw(owner, inProgress: false);
		}
	}

	private static Exception? CanonicalSettlementProgressFinalizer(
		bool __0,
		Exception? __exception)
	{
		if (__0
			|| ActiveCompletionEvaluation.Value is not CombatLedger ledger
			|| !TryGetSettlementProgressOwner(ledger, out object? owner))
		{
			return __exception;
		}

		SetSettlementProgressRaw(owner, inProgress: false);
		if (__exception != null)
		{
			lock (ledger.Gate)
			{
				if (!ledger.LoggedSettlementProgressRepair)
				{
					ledger.LoggedSettlementProgressRepair = true;
					Log.Warn(
						$"[{ModInfo.Id}] Canonical settlement progress was " +
						"committed after an intercepted state transition.");
				}
			}
		}
		return null;
	}

	private static bool TryGetSettlementProgressOwner(
		CombatLedger ledger,
		[NotNullWhen(true)] out object? owner)
	{
#if STS2_107_1
		CombatManager manager = CombatManager.Instance;
		owner = manager;
		return ReferenceEquals(
			LegacyManagerStateField.GetValue(manager),
			ledger.CombatState);
#elif STS2_110_0
		object? currentTurnState = ManagerTurnStateField.GetValue(
			CombatManager.Instance);
		owner = currentTurnState;
		return currentTurnState != null
			&& ReferenceEquals(currentTurnState, ledger.CombatEpoch)
			&& ReferenceEquals(
				TurnStateCombatStateField.GetValue(currentTurnState),
				ledger.CombatState);
#endif
	}

	private static void SetSettlementProgressRaw(
		object instance,
		bool inProgress)
	{
#if STS2_107_1
		LegacyManagerInProgressField.SetValue(instance, inProgress);
#elif STS2_110_0
		TurnStateInProgressField.SetValue(instance, inProgress);
#endif
	}

	private static ManagerCombatSnapshot ReadManagerSnapshot(
		CombatManager manager,
		object? invocationTurnState)
	{
#if STS2_107_1
		if (invocationTurnState != null)
		{
			return default;
		}

		return new ManagerCombatSnapshot(
			LegacyManagerStateField.GetValue(manager) as ICombatState,
			TurnState: null,
			IsCurrentInvocation: true,
			IsInProgress:
				LegacyManagerInProgressField.GetValue(manager) is true,
			IsStarting:
				LegacyManagerStartingField.GetValue(manager) is true,
			HasPendingLoss:
				LegacyManagerPendingLossField.GetValue(manager) != null);
#elif STS2_110_0
		object? currentTurnState = ManagerTurnStateField.GetValue(manager);
		object? selectedTurnState =
			invocationTurnState ?? currentTurnState;
		if (selectedTurnState == null)
		{
			return default;
		}

		return new ManagerCombatSnapshot(
			TurnStateCombatStateField.GetValue(selectedTurnState)
				as ICombatState,
			selectedTurnState,
			ReferenceEquals(selectedTurnState, currentTurnState),
			TurnStateInProgressField.GetValue(selectedTurnState) is true,
			TurnStateStartingField.GetValue(selectedTurnState) is true,
			TurnStatePendingLossField.GetValue(selectedTurnState) != null);
#endif
	}

	private readonly record struct ManagerCombatSnapshot(
		ICombatState? CombatState,
		object? TurnState,
		bool IsCurrentInvocation,
		bool IsInProgress,
		bool IsStarting,
		bool HasPendingLoss);
}
