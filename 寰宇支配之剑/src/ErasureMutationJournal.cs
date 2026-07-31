namespace UniversalDominionSword;

internal readonly record struct ErasureMutationRecord(
	long MutationOrdinal,
	ErasureContinuationToken Token,
	object CandidateRef,
	ErasureMutationKind Kind,
	bool UsedGenericSlotAllocator,
	ErasureAdmissionKind Admission);

internal sealed class ErasureMutationJournal
{
	public const int MaximumRecordedMutations = 512;

	private readonly long _operationSequence;
	private readonly List<ErasureMutationRecord> _records = [];
	private long _nextMutationOrdinal;

	public ErasureMutationJournal(long operationSequence)
	{
		_operationSequence = operationSequence;
	}

	public IReadOnlyList<ErasureMutationRecord> Records => _records;

	public int DroppedRecordCount { get; private set; }

	public void Record(
		ErasureContinuationToken token,
		object candidateRef,
		ErasureMutationKind kind,
		bool usedGenericSlotAllocator,
		ErasureAdmissionKind admission)
	{
		if (token.OperationSequence != _operationSequence)
		{
			throw new InvalidOperationException(
				"Mutation journal token belongs to a different operation.");
		}

		long ordinal = _nextMutationOrdinal++;
		if (_records.Count >= MaximumRecordedMutations)
		{
			DroppedRecordCount++;
			return;
		}

		_records.Add(new ErasureMutationRecord(
			ordinal,
			token,
			candidateRef,
			kind,
			usedGenericSlotAllocator,
			admission));
	}
}
