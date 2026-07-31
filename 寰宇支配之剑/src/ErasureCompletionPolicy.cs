namespace UniversalDominionSword;

internal enum ErasureCompletionDecision
{
	AllowNormalEnd,
	DifferentCombat,
	CombatNotInProgress,
	CombatStarting,
	PendingLoss,
	NoLivingPlayer,
	NoTrackedLineage,
	PersistenceLeaseOpen,
	CompletionNotArmed,
	UncertifiedLineage,
	ActiveConvergence,
	LivingPrimaryEnemy,
	CombatEndHookBlocked
}

internal readonly record struct ErasureCompletionSnapshot(
	bool IsExpectedCombat,
	bool IsInProgress,
	bool IsStarting,
	bool HasPendingLoss,
	bool HasLivingPlayer,
	bool HasTrackedLineage,
	bool HasOpenPersistenceLease,
	bool IsCompletionArmed,
	bool AreAllLineagesCertified,
	bool HasActiveConvergence,
	bool HasLivingUntrackedPrimaryEnemy,
	bool IsBlockedByCombatEndHook);

internal static class ErasureCompletionPolicy
{
	public static bool CanEndNormally(in ErasureCompletionSnapshot snapshot)
	{
		return Evaluate(snapshot) == ErasureCompletionDecision.AllowNormalEnd;
	}

	public static ErasureCompletionDecision Evaluate(
		in ErasureCompletionSnapshot snapshot)
	{
		if (!snapshot.IsExpectedCombat)
		{
			return ErasureCompletionDecision.DifferentCombat;
		}

		if (!snapshot.IsInProgress)
		{
			return ErasureCompletionDecision.CombatNotInProgress;
		}

		if (snapshot.IsStarting)
		{
			return ErasureCompletionDecision.CombatStarting;
		}

		if (snapshot.HasPendingLoss)
		{
			return ErasureCompletionDecision.PendingLoss;
		}

		if (!snapshot.HasLivingPlayer)
		{
			return ErasureCompletionDecision.NoLivingPlayer;
		}

		if (!snapshot.HasTrackedLineage)
		{
			return ErasureCompletionDecision.NoTrackedLineage;
		}

		if (snapshot.HasOpenPersistenceLease)
		{
			return ErasureCompletionDecision.PersistenceLeaseOpen;
		}

		if (!snapshot.IsCompletionArmed)
		{
			return ErasureCompletionDecision.CompletionNotArmed;
		}

		if (!snapshot.AreAllLineagesCertified)
		{
			return ErasureCompletionDecision.UncertifiedLineage;
		}

		if (snapshot.HasActiveConvergence)
		{
			return ErasureCompletionDecision.ActiveConvergence;
		}

		if (snapshot.HasLivingUntrackedPrimaryEnemy)
		{
			return ErasureCompletionDecision.LivingPrimaryEnemy;
		}

		if (snapshot.IsBlockedByCombatEndHook)
		{
			return ErasureCompletionDecision.CombatEndHookBlocked;
		}

		return ErasureCompletionDecision.AllowNormalEnd;
	}
}
