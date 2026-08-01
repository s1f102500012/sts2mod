namespace UniversalDominionSword;

internal enum ErasureDeferredCallbackDecision
{
	ExecuteUntracked,
	ExecuteActive,
	ExecuteUncertified,
	DiscardStaleCombat,
	DiscardCommittedLineage
}

internal readonly record struct ErasureDeferredCallbackSnapshot(
	bool HasTrackedScope,
	bool IsExpectedCombat,
	bool IsInProgress,
	bool IsTerminalBarrierArmed,
	bool IsTerminalSealed,
	bool IsCompletionFlightRunning,
	bool IsLineageCertified);

internal static class ErasureDeferredCallbackPolicy
{
	public static ErasureDeferredCallbackDecision Evaluate(
		ErasureDeferredCallbackSnapshot snapshot)
	{
		if (!snapshot.HasTrackedScope)
		{
			return ErasureDeferredCallbackDecision.ExecuteUntracked;
		}
		if (snapshot.IsTerminalSealed
			|| !snapshot.IsExpectedCombat
			|| !snapshot.IsInProgress)
		{
			return ErasureDeferredCallbackDecision.DiscardStaleCombat;
		}
		if ((snapshot.IsTerminalBarrierArmed
				|| snapshot.IsCompletionFlightRunning)
			&& snapshot.IsLineageCertified)
		{
			return ErasureDeferredCallbackDecision.DiscardCommittedLineage;
		}
		return snapshot.IsLineageCertified
			? ErasureDeferredCallbackDecision.ExecuteActive
			: ErasureDeferredCallbackDecision.ExecuteUncertified;
	}

	public static bool ShouldExecute(
		ErasureDeferredCallbackSnapshot snapshot)
	{
		return Evaluate(snapshot) is
			ErasureDeferredCallbackDecision.ExecuteUntracked
			or ErasureDeferredCallbackDecision.ExecuteActive
			or ErasureDeferredCallbackDecision.ExecuteUncertified;
	}
}
