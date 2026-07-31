using System.Reflection;
using MegaCrit.Sts2.Core.Combat;

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
