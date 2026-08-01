namespace UniversalDominionSword;

internal enum ErasureAdmissionKind
{
	None,
	ExactCreature,
	CombatId,
	MonsterInstance,
	CausalToken,
	TerminalTransaction,
	PreexistingCollision,
	LimitReached
}

internal enum ErasureMutationKind
{
	Observed,
	Created,
	Attached,
	Added,
	NodeCreated,
	Reentered
}

internal readonly record struct ErasureContinuationToken(
	long OperationSequence,
	long ParentAdmissionOrdinal);

internal readonly record struct ErasureEvidence(
	object CreatureRef,
	uint? CombatId,
	object? MonsterRef,
	string MonsterType,
	string? SlotName,
	bool IsEnemy,
	bool IsPrimary);

internal readonly record struct ErasureAdmission(
	ErasureAdmissionKind Kind,
	ErasureLineageMember? Member)
{
	public bool IsMember => Member != null;

	public bool RequiresExactConvergence =>
		IsMember || Kind == ErasureAdmissionKind.LimitReached;
}

internal sealed class ErasureLineageMember
{
	public ErasureLineageMember(
		ErasureEvidence evidence,
		ErasureLineageMember? parent,
		int generation,
		ErasureAdmissionKind admission,
		long admissionOrdinal)
	{
		Evidence = evidence;
		Parent = parent;
		Generation = generation;
		Admission = admission;
		AdmissionOrdinal = admissionOrdinal;
	}

	public ErasureEvidence Evidence { get; }

	public ErasureLineageMember? Parent { get; }

	public int Generation { get; }

	public ErasureAdmissionKind Admission { get; }

	public long AdmissionOrdinal { get; }
}

internal sealed partial class ErasureLineage
{
	public const int MaximumGeneration = 64;
	public const int MaximumContinuationClaims = 256;

	private readonly Dictionary<object, ErasureLineageMember> _members =
		new(ReferenceEqualityComparer.Instance);

	private readonly Dictionary<long, ErasureLineageMember> _membersByOrdinal =
		[];

	private readonly HashSet<object> _preexisting =
		new(ReferenceEqualityComparer.Instance);

	private readonly Dictionary<uint, HashSet<object>> _preexistingCombatIds = [];

	private readonly Dictionary<object, HashSet<object>> _preexistingMonsterRefs =
		new(ReferenceEqualityComparer.Instance);

	private readonly HashSet<object> _continuationClaims =
		new(ReferenceEqualityComparer.Instance);

	private readonly ErasureMutationJournal _mutationJournal;

	private long _nextAdmissionOrdinal;

	public ErasureLineage(
		long operationSequence,
		ErasureEvidence root,
		IEnumerable<ErasureEvidence> preexisting,
		bool wasTerminalCandidateAtStart = false)
	{
		OperationSequence = operationSequence;
		WasTerminalCandidateAtStart = wasTerminalCandidateAtStart;
		_mutationJournal = new ErasureMutationJournal(operationSequence);

		foreach (ErasureEvidence evidence in preexisting)
		{
			RecordPreexistingEvidence(evidence);
		}
		RecordPreexistingEvidence(root);

		Root = AddMember(
			root,
			parent: null,
			ErasureAdmissionKind.ExactCreature);
	}

	public long OperationSequence { get; }

	public bool WasTerminalCandidateAtStart { get; }

	public ErasureLineageMember Root { get; }

	public IReadOnlyList<ErasureLineageMember> Members =>
		_members.Values
			.OrderBy(member => member.Generation)
			.ThenBy(member => member.AdmissionOrdinal)
			.ToArray();

	public IReadOnlyList<ErasureMutationRecord> MutationJournal =>
		_mutationJournal.Records;

	public int DroppedMutationRecordCount =>
		_mutationJournal.DroppedRecordCount;

	public bool IsMember(object creatureRef) => _members.ContainsKey(creatureRef);

	public ErasureContinuationToken CreateContinuationToken(
		ErasureLineageMember parent)
	{
		if (!_membersByOrdinal.TryGetValue(
				parent.AdmissionOrdinal,
				out ErasureLineageMember? registered)
			|| !ReferenceEquals(registered, parent))
		{
			throw new InvalidOperationException(
				"Continuation tokens require an exact lineage member.");
		}

		return new ErasureContinuationToken(
			OperationSequence,
			parent.AdmissionOrdinal);
	}

	public ErasureAdmission TryAdmitStrong(ErasureEvidence candidate)
	{
		if (_members.TryGetValue(
			candidate.CreatureRef,
			out ErasureLineageMember? exact))
		{
			return new ErasureAdmission(
				ErasureAdmissionKind.ExactCreature,
				exact);
		}

		if (_preexisting.Contains(candidate.CreatureRef))
		{
			return new ErasureAdmission(
				ErasureAdmissionKind.PreexistingCollision,
				null);
		}

		if (candidate.CombatId is uint combatId)
		{
			ErasureLineageMember? parent = FindStableParent(
				member => member.Evidence.CombatId == combatId);
			if (parent != null)
			{
				if (_preexistingCombatIds.TryGetValue(
						combatId,
						out HashSet<object>? preexistingOwners)
					&& HasForeignPreexistingOwner(preexistingOwners))
				{
					return new ErasureAdmission(
						ErasureAdmissionKind.PreexistingCollision,
						null);
				}

				return AdmitFromParent(
					candidate,
					parent,
					ErasureAdmissionKind.CombatId);
			}
		}

		if (candidate.MonsterRef != null)
		{
			ErasureLineageMember? parent = FindStableParent(
				member => ReferenceEquals(
					member.Evidence.MonsterRef,
					candidate.MonsterRef));
			if (parent != null)
			{
				if (_preexistingMonsterRefs.TryGetValue(
						candidate.MonsterRef,
						out HashSet<object>? preexistingOwners)
					&& HasForeignPreexistingOwner(preexistingOwners))
				{
					return new ErasureAdmission(
						ErasureAdmissionKind.PreexistingCollision,
						null);
				}

				return AdmitFromParent(
					candidate,
					parent,
					ErasureAdmissionKind.MonsterInstance);
			}
		}

		return new ErasureAdmission(ErasureAdmissionKind.None, null);
	}

	public ErasureAdmission ObserveCausal(
		ErasureEvidence candidate,
		ErasureContinuationToken token,
		ErasureMutationKind mutationKind = ErasureMutationKind.Observed)
	{
		if (token.OperationSequence != OperationSequence
			|| !_membersByOrdinal.TryGetValue(
				token.ParentAdmissionOrdinal,
				out ErasureLineageMember? parent))
		{
			return new ErasureAdmission(ErasureAdmissionKind.None, null);
		}

		ErasureAdmission strong = TryAdmitStrong(candidate);
		if (strong.IsMember
			|| strong.Kind == ErasureAdmissionKind.PreexistingCollision)
		{
			_mutationJournal.Record(
				token,
				candidate.CreatureRef,
				mutationKind,
				strong.Kind);
			return strong;
		}

		if (!candidate.IsEnemy)
		{
			_mutationJournal.Record(
				token,
				candidate.CreatureRef,
				mutationKind,
				ErasureAdmissionKind.None);
			return new ErasureAdmission(ErasureAdmissionKind.None, null);
		}

		return AdmitProvenContinuation(
			candidate,
			parent,
			token,
			mutationKind,
			ErasureAdmissionKind.CausalToken);
	}

	public ErasureAdmission ObserveTerminalSuccessor(
		ErasureEvidence candidate,
		ErasureMutationKind mutationKind)
	{
		ErasureLineageMember parent = _members.Values
			.OrderByDescending(member => member.AdmissionOrdinal)
			.First();
		ErasureContinuationToken token = CreateContinuationToken(parent);
		ErasureAdmission strong = TryAdmitStrong(candidate);
		if (strong.IsMember
			|| strong.Kind == ErasureAdmissionKind.PreexistingCollision)
		{
			_mutationJournal.Record(
				token,
				candidate.CreatureRef,
				mutationKind,
				strong.Kind);
			return strong;
		}

		if (!WasTerminalCandidateAtStart
			|| !candidate.IsEnemy
			|| !candidate.IsPrimary)
		{
			_mutationJournal.Record(
				token,
				candidate.CreatureRef,
				mutationKind,
				ErasureAdmissionKind.None);
			return new ErasureAdmission(ErasureAdmissionKind.None, null);
		}

		return AdmitProvenContinuation(
			candidate,
			parent,
			token,
			mutationKind,
			ErasureAdmissionKind.TerminalTransaction);
	}

	private ErasureAdmission AdmitProvenContinuation(
		ErasureEvidence candidate,
		ErasureLineageMember parent,
		ErasureContinuationToken token,
		ErasureMutationKind mutationKind,
		ErasureAdmissionKind admissionKind)
	{
		if (parent.Generation >= MaximumGeneration
			|| (!_continuationClaims.Contains(candidate.CreatureRef)
				&& _continuationClaims.Count >= MaximumContinuationClaims))
		{
			_mutationJournal.Record(
				token,
				candidate.CreatureRef,
				mutationKind,
				ErasureAdmissionKind.LimitReached);
			return new ErasureAdmission(
				ErasureAdmissionKind.LimitReached,
				null);
		}

		_continuationClaims.Add(candidate.CreatureRef);
		ErasureAdmission admission = AdmitFromParent(
			candidate,
			parent,
			admissionKind);
		_mutationJournal.Record(
			token,
			candidate.CreatureRef,
			mutationKind,
			admission.Kind);
		return admission;
	}

	private void RecordPreexistingEvidence(ErasureEvidence evidence)
	{
		_preexisting.Add(evidence.CreatureRef);

		if (evidence.CombatId is uint combatId)
		{
			AddPreexistingOwner(
				_preexistingCombatIds,
				combatId,
				evidence.CreatureRef);
		}
		if (evidence.MonsterRef != null)
		{
			AddPreexistingOwner(
				_preexistingMonsterRefs,
				evidence.MonsterRef,
				evidence.CreatureRef);
		}
	}

	private static void AddPreexistingOwner<TKey>(
		Dictionary<TKey, HashSet<object>> ownersByIdentity,
		TKey identity,
		object owner)
		where TKey : notnull
	{
		if (!ownersByIdentity.TryGetValue(
			identity,
			out HashSet<object>? owners))
		{
			owners = new HashSet<object>(ReferenceEqualityComparer.Instance);
			ownersByIdentity.Add(identity, owners);
		}
		owners.Add(owner);
	}

	private bool HasForeignPreexistingOwner(IEnumerable<object> owners)
	{
		return owners.Any(owner => !_members.ContainsKey(owner));
	}

	private ErasureLineageMember? FindStableParent(
		Func<ErasureLineageMember, bool> predicate)
	{
		return _members.Values
			.Where(predicate)
			.OrderBy(member => member.Generation)
			.ThenBy(member => member.AdmissionOrdinal)
			.FirstOrDefault();
	}

	private ErasureAdmission AdmitFromParent(
		ErasureEvidence candidate,
		ErasureLineageMember parent,
		ErasureAdmissionKind kind)
	{
		if (_members.TryGetValue(
			candidate.CreatureRef,
			out ErasureLineageMember? existing))
		{
			return new ErasureAdmission(kind, existing);
		}
		if (parent.Generation >= MaximumGeneration)
		{
			return new ErasureAdmission(
				ErasureAdmissionKind.LimitReached,
				null);
		}

		return new ErasureAdmission(
			kind,
			AddMember(candidate, parent, kind));
	}

	private ErasureLineageMember AddMember(
		ErasureEvidence evidence,
		ErasureLineageMember? parent,
		ErasureAdmissionKind admission)
	{
		ErasureLineageMember member = new(
			evidence,
			parent,
			parent == null ? 0 : parent.Generation + 1,
			admission,
			_nextAdmissionOrdinal++);
		_members.Add(evidence.CreatureRef, member);
		_membersByOrdinal.Add(member.AdmissionOrdinal, member);
		MarkActivity();
		return member;
	}
}

internal readonly record struct ErasureLayerState(
	bool HpIsZero,
	bool IsAbsentFromCombat,
	bool IsUnattached,
	bool HasNoActiveNode,
	bool HasNoRemovingNode,
	bool HasNoCapturedLiveNode)
{
	public bool IsConverged =>
		HpIsZero
		&& IsAbsentFromCombat
		&& IsUnattached
		&& HasNoActiveNode
		&& HasNoRemovingNode
		&& HasNoCapturedLiveNode;
}
