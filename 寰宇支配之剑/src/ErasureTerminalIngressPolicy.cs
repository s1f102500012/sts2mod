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
	bool IsTerminalSealed,
	bool IsCompletionFlightRunning,
	bool IsExpectedCombat,
	bool IsInProgress);

internal static class ErasureTerminalIngressPolicy
{
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

	if (snapshot.IsTerminalSealed)
	{
		return snapshot.IsBaselineEnemy
			? ErasureTerminalIngressDecision.Allow
			: ErasureTerminalIngressDecision.RejectTerminalIngress;
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
