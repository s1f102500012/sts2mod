namespace UniversalDominionSword;

internal enum ErasureSettlementTimingDecision
{
	EvaluateImmediately,
	DeferToActionBoundary
}

internal readonly record struct ErasureSettlementTimingSnapshot(
	bool IsGameActionExecuting);

internal static class ErasureSettlementTimingPolicy
{
	public static ErasureSettlementTimingDecision Evaluate(
		in ErasureSettlementTimingSnapshot snapshot)
	{
		return snapshot.IsGameActionExecuting
			? ErasureSettlementTimingDecision.DeferToActionBoundary
			: ErasureSettlementTimingDecision.EvaluateImmediately;
	}
}
