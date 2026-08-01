namespace UniversalDominionSword;

internal enum ErasureTerminalBarrierPhase
{
	Open,
	Armed,
	Committed,
	Completed
}

internal readonly record struct ErasureParticipantSnapshot(
	bool IsEnemy,
	bool IsSelectedTarget,
	bool IsAttachedToExpectedCombat,
	bool IsPresentInEnemyRoster,
	bool HasStableCombatPresence);

internal static class ErasureParticipationPolicy
{
	public static bool IsActiveAtSelection(
		in ErasureParticipantSnapshot snapshot)
	{
		return snapshot.IsEnemy
			&& snapshot.IsAttachedToExpectedCombat
			&& snapshot.IsPresentInEnemyRoster
			&& (snapshot.IsSelectedTarget
				|| snapshot.HasStableCombatPresence);
	}

	public static bool RejectActivation(
		ErasureTerminalBarrierPhase phase,
		bool isBaselineParticipant)
	{
		return phase != ErasureTerminalBarrierPhase.Open
			&& !isBaselineParticipant;
	}

	public static bool RejectContradictoryLoss(
		ErasureTerminalBarrierPhase phase,
		bool isCompletionRunning,
		bool isPlayerCreature)
	{
		return phase >= ErasureTerminalBarrierPhase.Committed
			&& isCompletionRunning
			&& isPlayerCreature;
	}
}
