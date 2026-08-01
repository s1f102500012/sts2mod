namespace UniversalDominionSword;

internal enum ErasureTerminalIngressDecision
{
	Allow,
	NoTrackedCombat,
	FriendlyCreature,
	CompletionNotCommitted,
	RejectTerminalIngress
}

internal readonly record struct ErasureTerminalIngressSnapshot(
	bool HasTrackedCombat,
	bool IsEnemy,
	bool IsBaselineEnemy,
	ErasureTerminalBarrierPhase BarrierPhase,
	bool IsCompletionFlightRunning,
	bool IsExpectedCombat,
	bool IsInProgress);

internal static class ErasureTerminalIngressPolicy
{
	public static Task CreateRejectedIngressTask()
	{
		return Task.CompletedTask;
	}

	public static ErasureTerminalIngressDecision Evaluate(
		in ErasureTerminalIngressSnapshot snapshot)
	{
		if (!snapshot.HasTrackedCombat)
		{
			return ErasureTerminalIngressDecision.NoTrackedCombat;
		}

		if (!snapshot.IsEnemy)
		{
			return ErasureTerminalIngressDecision.FriendlyCreature;
		}

		if (ErasureParticipationPolicy.RejectActivation(
			snapshot.BarrierPhase,
			snapshot.IsBaselineEnemy))
		{
			return ErasureTerminalIngressDecision.RejectTerminalIngress;
		}
		if (snapshot.BarrierPhase != ErasureTerminalBarrierPhase.Open)
		{
			return ErasureTerminalIngressDecision.Allow;
		}

		if (!snapshot.IsCompletionFlightRunning)
		{
			return ErasureTerminalIngressDecision.CompletionNotCommitted;
		}

		return snapshot.IsExpectedCombat && snapshot.IsInProgress
			? ErasureTerminalIngressDecision.CompletionNotCommitted
			: ErasureTerminalIngressDecision.RejectTerminalIngress;
	}
}
