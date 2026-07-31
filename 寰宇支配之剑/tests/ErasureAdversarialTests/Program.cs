using UniversalDominionSword;
using UniversalDominionSword.Loader;

namespace UniversalDominionSword.Tests;

internal static class Program
{
	private static readonly (string Name, Action Test)[] Cases =
	[
		("exact creature identity is idempotent", ExactCreatureIsIdempotent),
		("combat id adopts a replacement shell", CombatIdAdoptsReplacement),
		("monster instance adopts a replacement shell", MonsterInstanceAdoptsReplacement),
		("preexisting creature is rejected", PreexistingCreatureIsRejected),
		("preexisting combat id collision is rejected", PreexistingCombatIdCollisionIsRejected),
		("preexisting monster collision is rejected", PreexistingMonsterCollisionIsRejected),
		("causal token adopts a different model and slot", TokenAdoptsDifferentModelAndSlot),
		("causal token supports a long replacement chain", TokenSupportsLongReplacementChain),
		("terminal transaction adopts a new primary successor", TerminalTransactionAdoptsPrimarySuccessor),
		("terminal transaction rejects a non-primary summon", TerminalTransactionRejectsSummon),
		("non-terminal lineage rejects an unscoped primary", NonTerminalLineageRejectsUnscopedPrimary),
		("token from another operation is rejected", ForeignOperationTokenIsRejected),
		("token with an unknown parent is rejected", UnknownParentTokenIsRejected),
		("preexisting creature cannot be claimed by a token", PreexistingCreatureCannotBeClaimed),
		("friendly creation cannot be claimed by a token", FriendlyCreationCannotBeClaimed),
		("causal token is independent of slot allocation", CausalTokenIgnoresSlotAllocation),
		("same slot without a token remains unrelated", SameSlotWithoutTokenIsUnrelated),
		("same type without a token remains unrelated", SameTypeWithoutTokenIsUnrelated),
		("identical preexisting sibling remains unrelated", IdenticalSiblingRemainsUnrelated),
		("mutation journal records an admitted edge", JournalRecordsAdmittedEdge),
		("mutation journal records a rejected friendly creation", JournalRecordsRejectedFriendlyCreation),
		("mutation journal is bounded", MutationJournalIsBounded),
		("continuation claim budget is bounded", ContinuationClaimBudgetIsBounded),
		("generation budget is bounded", GenerationBudgetIsBounded),
		("host and client mutation traces are deterministic", MutationTracesAreDeterministic),
		("canonical termination can begin only once", CanonicalTerminationBeginsOnce),
		("matching revision permits certification", MatchingRevisionPermitsCertification),
		("new lineage member invalidates certification", NewMemberInvalidatesCertification),
		("causal token remains valid after certification", TokenRemainsValidAfterCertification),
		("unrelated activity cannot claim a late successor", UnrelatedActivityCannotClaimLateSuccessor),
		("combat completion accepts a certified terminal state", CompletionAcceptsCertifiedState),
		("combat completion rejects every unsafe gate", CompletionRejectsUnsafeGates),
		("non-primary summons do not block completion", NonPrimarySummonsDoNotBlockCompletion),
		("living primary enemy blocks completion", LivingPrimaryEnemyBlocksCompletion),
		("canonical death animation is preserved", CanonicalDeathAnimationIsPreserved),
		("visual exit requires every safety gate", VisualExitRequiresEverySafetyGate),
		("sealed combat rejects late enemy ingress", SealedCombatRejectsLateEnemyIngress),
		("sealed combat preserves its baseline enemies", SealedCombatPreservesBaselineEnemies),
		("completion commit rejects delayed enemy ingress", CompletionCommitRejectsDelayedEnemyIngress),
		("terminal ingress guard preserves valid additions", TerminalIngressGuardPreservesValidAdditions),
		("committed lineage discards its deferred callbacks", CommittedLineageDiscardsDeferredCallbacks),
		("unresolved lineage preserves its deferred callbacks", UnresolvedLineagePreservesDeferredCallbacks),
		("audit contract declares scoped interoperability boundaries", AuditContractDeclaresBoundaries),
		("audit contract acknowledges version-sensitive internals", AuditContractAcknowledgesRisk),
		("loader selection is fail-closed", LoaderSelectionIsFailClosed),
	];

	public static int Main()
	{
		int failures = 0;
		foreach ((string name, Action test) in Cases)
		{
			try
			{
				test();
				Console.WriteLine($"PASS {name}");
			}
			catch (Exception exception)
			{
				failures++;
				Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
			}
		}

		Console.WriteLine(
			$"{Cases.Length - failures}/{Cases.Length} adversarial lineage tests passed.");
		return failures == 0 ? 0 : 1;
	}

	private static void ExactCreatureIsIdempotent()
	{
		Fixture fixture = Fixture.Create();
		ErasureAdmission first = fixture.Lineage.TryAdmitStrong(fixture.Root);
		ErasureAdmission second = fixture.Lineage.TryAdmitStrong(fixture.Root);

		Assert.Equal(ErasureAdmissionKind.ExactCreature, first.Kind);
		Assert.Same(fixture.Lineage.Root, first.Member);
		Assert.Same(first.Member!, second.Member);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void AuditContractDeclaresBoundaries()
	{
		Assert.True(
			ErasurePatchContract.SelectedLineageScope.Contains(
				"same combat",
				StringComparison.Ordinal));
		Assert.True(
			ErasurePatchContract.ThirdPartyInteroperability.Contains(
				"Never enumerate, unpatch",
				StringComparison.Ordinal));
		Assert.True(
			ErasurePatchContract.IdentityAdmission.Contains(
				"model type and slot alone are insufficient",
				StringComparison.Ordinal));
	}

	private static void AuditContractAcknowledgesRisk()
	{
		Assert.True(
			ErasurePatchContract.KnownCompatibilityRisk.Contains(
				"Version-specific private members",
				StringComparison.Ordinal));
		Assert.True(
			ErasurePatchContract.ValidationBoundary.Contains(
				"not gameplay or multiplayer proof",
				StringComparison.Ordinal));
	}

	private static void CombatIdAdoptsReplacement()
	{
		Fixture fixture = Fixture.Create();
		ErasureEvidence replacement = Evidence(
			"replacement",
			fixture.Root.CombatId,
			"replacement-monster",
			"DifferentModel",
			"Z");

		ErasureAdmission admission =
			fixture.Lineage.TryAdmitStrong(replacement);

		Assert.Equal(ErasureAdmissionKind.CombatId, admission.Kind);
		Assert.Same(fixture.Lineage.Root, admission.Member?.Parent);
	}

	private static void MonsterInstanceAdoptsReplacement()
	{
		Fixture fixture = Fixture.Create();
		ErasureEvidence replacement = Evidence(
			"replacement",
			9,
			fixture.Root.MonsterRef!,
			"DifferentModel",
			"Z");

		ErasureAdmission admission =
			fixture.Lineage.TryAdmitStrong(replacement);

		Assert.Equal(ErasureAdmissionKind.MonsterInstance, admission.Kind);
		Assert.Same(fixture.Lineage.Root, admission.Member?.Parent);
	}

	private static void PreexistingCreatureIsRejected()
	{
		Fixture fixture = Fixture.CreateWithPeer();

		ErasureAdmission admission =
			fixture.Lineage.TryAdmitStrong(fixture.Peer!.Value);

		Assert.Equal(ErasureAdmissionKind.PreexistingCollision, admission.Kind);
		Assert.Null(admission.Member);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void PreexistingCombatIdCollisionIsRejected()
	{
		ErasureEvidence root = Evidence("root", 1, "root-monster", "Root", "A");
		ErasureEvidence peer = Evidence("peer", 1, "peer-monster", "Peer", "B");
		ErasureLineage lineage = new(1, root, [root, peer]);
		ErasureEvidence replacement =
			Evidence("replacement", 1, "new-monster", "Replacement", "C");

		ErasureAdmission admission = lineage.TryAdmitStrong(replacement);

		Assert.Equal(ErasureAdmissionKind.PreexistingCollision, admission.Kind);
		Assert.Null(admission.Member);
	}

	private static void PreexistingMonsterCollisionIsRejected()
	{
		Ref sharedMonster = new("shared-monster");
		ErasureEvidence root = Evidence("root", 1, sharedMonster, "Root", "A");
		ErasureEvidence peer = Evidence("peer", 2, sharedMonster, "Peer", "B");
		ErasureLineage lineage = new(1, root, [root, peer]);
		ErasureEvidence replacement =
			Evidence("replacement", 3, sharedMonster, "Replacement", "C");

		ErasureAdmission admission = lineage.TryAdmitStrong(replacement);

		Assert.Equal(ErasureAdmissionKind.PreexistingCollision, admission.Kind);
		Assert.Null(admission.Member);
	}

	private static void TokenAdoptsDifferentModelAndSlot()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		ErasureEvidence replacement = Evidence(
			"phase-two",
			2,
			"phase-two-monster",
			"UnrelatedRuntimeType",
			"Z");

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			replacement,
			token,
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.CausalToken, admission.Kind);
		Assert.True(admission.IsMember);
		Assert.Same(fixture.Lineage.Root, admission.Member?.Parent);
	}

	private static void TokenSupportsLongReplacementChain()
	{
		Fixture fixture = Fixture.Create(slot: null);
		ErasureLineageMember parent = fixture.Lineage.Root;
		for (int index = 0;
			index < ErasureLineage.MaximumGeneration;
			index++)
		{
			ErasureContinuationToken token =
				fixture.Lineage.CreateContinuationToken(parent);
			ErasureEvidence replacement = Evidence(
				$"phase-{index + 2}",
				(uint)(index + 2),
				$"monster-{index + 2}",
				$"Model{index + 2}",
				null);
			ErasureAdmission admission = fixture.Lineage.ObserveCausal(
				replacement,
				token,
				ErasureMutationKind.Created);
			Assert.Equal(ErasureAdmissionKind.CausalToken, admission.Kind);
			parent = admission.Member
				?? throw new InvalidOperationException("Missing continuation.");
		}

		Assert.Equal(ErasureLineage.MaximumGeneration, parent.Generation);
		Assert.Equal(
			ErasureLineage.MaximumGeneration + 1,
			fixture.Lineage.MemberCount);
	}

	private static void TerminalTransactionAdoptsPrimarySuccessor()
	{
		ErasureEvidence root = Evidence(
			"root",
			1,
			"root-monster",
			"RootType",
			"A",
			primary: true);
		ErasureLineage lineage = new(
			1,
			root,
			[root],
			wasSoleLivingPrimaryEnemyAtStart: true);

		ErasureAdmission admission = lineage.ObserveTerminalSuccessor(
			Evidence(
				"successor",
				2,
				"successor-monster",
				"DifferentType",
				"Z",
				primary: true),
			ErasureMutationKind.Added);

		Assert.Equal(
			ErasureAdmissionKind.TerminalTransaction,
			admission.Kind);
		Assert.True(admission.RequiresExactConvergence);
		Assert.Same(lineage.Root, admission.Member?.Parent);
	}

	private static void TerminalTransactionRejectsSummon()
	{
		ErasureEvidence root = Evidence(
			"root",
			1,
			"root-monster",
			"RootType",
			"A",
			primary: true);
		ErasureLineage lineage = new(
			1,
			root,
			[root],
			wasSoleLivingPrimaryEnemyAtStart: true);

		ErasureAdmission admission = lineage.ObserveTerminalSuccessor(
			Evidence(
				"summon",
				2,
				"summon-monster",
				"SummonType",
				"B",
				primary: false),
			ErasureMutationKind.Added);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.False(admission.RequiresExactConvergence);
	}

	private static void NonTerminalLineageRejectsUnscopedPrimary()
	{
		ErasureEvidence root = Evidence(
			"root",
			1,
			"root-monster",
			"RootType",
			"A",
			primary: true);
		ErasureLineage lineage = new(1, root, [root]);

		ErasureAdmission admission = lineage.ObserveTerminalSuccessor(
			Evidence(
				"unscoped-primary",
				2,
				"other-monster",
				"OtherType",
				"B",
				primary: true),
			ErasureMutationKind.Added);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
	}

	private static void ForeignOperationTokenIsRejected()
	{
		Fixture fixture = Fixture.Create(operationSequence: 7);
		ErasureContinuationToken token = new(
			OperationSequence: 8,
			ParentAdmissionOrdinal: fixture.Lineage.Root.AdmissionOrdinal);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			Evidence("foreign", 2, "foreign-monster", "Foreign", null),
			token);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
		Assert.Equal(0, fixture.Lineage.MutationJournal.Count);
	}

	private static void UnknownParentTokenIsRejected()
	{
		Fixture fixture = Fixture.Create(operationSequence: 7);
		ErasureContinuationToken token = new(
			OperationSequence: 7,
			ParentAdmissionOrdinal: 99);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			Evidence("orphan", 2, "orphan-monster", "Orphan", null),
			token);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void PreexistingCreatureCannotBeClaimed()
	{
		Fixture fixture = Fixture.CreateWithPeer();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			fixture.Peer!.Value,
			token,
			ErasureMutationKind.Attached);

		Assert.Equal(ErasureAdmissionKind.PreexistingCollision, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void FriendlyCreationCannotBeClaimed()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		ErasureEvidence friendly = Evidence(
			"friendly",
			2,
			"friendly-monster",
			"Friendly",
			null,
			enemy: false);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			friendly,
			token,
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void CausalTokenIgnoresSlotAllocation()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		ErasureEvidence summon = Evidence(
			"summon",
			2,
			"summon-monster",
			fixture.Root.MonsterType,
			fixture.Root.SlotName);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			summon,
			token,
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.CausalToken, admission.Kind);
		Assert.Equal(2, fixture.Lineage.MemberCount);
	}

	private static void SameSlotWithoutTokenIsUnrelated()
	{
		Fixture fixture = Fixture.Create();
		ErasureEvidence newcomer =
			Evidence("new", 2, "new-monster", "Different", fixture.Root.SlotName);

		ErasureAdmission admission = fixture.Lineage.TryAdmitStrong(newcomer);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void SameTypeWithoutTokenIsUnrelated()
	{
		Fixture fixture = Fixture.Create();
		ErasureEvidence newcomer =
			Evidence("new", 2, "new-monster", fixture.Root.MonsterType, "Z");

		ErasureAdmission admission = fixture.Lineage.TryAdmitStrong(newcomer);

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void IdenticalSiblingRemainsUnrelated()
	{
		Fixture fixture = Fixture.CreateWithPeer(sameTypeAndSlot: true);
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			fixture.Peer!.Value,
			token,
			ErasureMutationKind.Added);

		Assert.Equal(ErasureAdmissionKind.PreexistingCollision, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void JournalRecordsAdmittedEdge()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		ErasureEvidence replacement =
			Evidence("new", 2, "new-monster", "New", null);

		fixture.Lineage.ObserveCausal(
			replacement,
			token,
			ErasureMutationKind.Created);

		ErasureMutationRecord record = fixture.Lineage.MutationJournal.Single();
		Assert.Equal(0L, record.MutationOrdinal);
		Assert.Equal(token, record.Token);
		Assert.Equal(ErasureMutationKind.Created, record.Kind);
		Assert.Equal(ErasureAdmissionKind.CausalToken, record.Admission);
		Assert.Same(replacement.CreatureRef, record.CandidateRef);
	}

	private static void JournalRecordsRejectedFriendlyCreation()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);

		fixture.Lineage.ObserveCausal(
			Evidence(
				"friendly",
				2,
				"friendly-monster",
				"Friendly",
				"A",
				enemy: false),
			token,
			ErasureMutationKind.Created);

		ErasureMutationRecord record = fixture.Lineage.MutationJournal.Single();
		Assert.Equal(ErasureAdmissionKind.None, record.Admission);
	}

	private static void MutationJournalIsBounded()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		for (int index = 0;
			index < ErasureMutationJournal.MaximumRecordedMutations + 9;
			index++)
		{
			fixture.Lineage.ObserveCausal(
				Evidence(
					$"summon-{index}",
					(uint)(index + 2),
					$"summon-monster-{index}",
					"Summon",
					"A",
					enemy: false),
				token,
				ErasureMutationKind.Created);
		}

		Assert.Equal(
			ErasureMutationJournal.MaximumRecordedMutations,
			fixture.Lineage.MutationJournal.Count);
		Assert.Equal(9, fixture.Lineage.DroppedMutationRecordCount);
	}

	private static void ContinuationClaimBudgetIsBounded()
	{
		Fixture fixture = Fixture.Create(slot: null);
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		for (int index = 0;
			index < ErasureLineage.MaximumContinuationClaims;
			index++)
		{
			ErasureAdmission admission = fixture.Lineage.ObserveCausal(
				Evidence(
					$"replacement-{index}",
					(uint)(index + 2),
					$"monster-{index}",
					$"Model{index}",
					null),
				token,
				ErasureMutationKind.Created);
			Assert.Equal(ErasureAdmissionKind.CausalToken, admission.Kind);
		}

		ErasureAdmission overflow = fixture.Lineage.ObserveCausal(
			Evidence("overflow", 999, "overflow-monster", "Overflow", null),
			token,
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.LimitReached, overflow.Kind);
		Assert.True(overflow.RequiresExactConvergence);
		Assert.Equal(
			ErasureLineage.MaximumContinuationClaims + 1,
			fixture.Lineage.MemberCount);
	}

	private static void GenerationBudgetIsBounded()
	{
		Fixture fixture = Fixture.Create(slot: null);
		ErasureLineageMember parent = fixture.Lineage.Root;
		for (int index = 0;
			index < ErasureLineage.MaximumGeneration;
			index++)
		{
			ErasureAdmission admission = fixture.Lineage.ObserveCausal(
				Evidence(
					$"replacement-{index}",
					(uint)(index + 2),
					$"monster-{index}",
					$"Model{index}",
					null),
				fixture.Lineage.CreateContinuationToken(parent),
				ErasureMutationKind.Created);
			parent = admission.Member
				?? throw new InvalidOperationException("Missing continuation.");
		}

		ErasureAdmission overflow = fixture.Lineage.ObserveCausal(
			Evidence("overflow", 999, "overflow-monster", "Overflow", null),
			fixture.Lineage.CreateContinuationToken(parent),
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.LimitReached, overflow.Kind);
		Assert.True(overflow.RequiresExactConvergence);
		Assert.Equal(ErasureLineage.MaximumGeneration, parent.Generation);
	}

	private static void MutationTracesAreDeterministic()
	{
		Assert.Equal(RunTrace("host"), RunTrace("client"));
	}

	private static string RunTrace(string side)
	{
		Fixture fixture = Fixture.Create(
			operationSequence: 41,
			creatureName: $"{side}-root",
			monsterName: $"{side}-monster");
		ErasureContinuationToken rootToken =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		ErasureAdmission first = fixture.Lineage.ObserveCausal(
			Evidence(
				$"{side}-phase-two",
				2,
				$"{side}-phase-two-monster",
				"PhaseTwo",
				null),
			rootToken,
			ErasureMutationKind.Created);
		ErasureContinuationToken secondToken =
			fixture.Lineage.CreateContinuationToken(first.Member!);
		fixture.Lineage.ObserveCausal(
			Evidence(
				$"{side}-phase-three",
				3,
				$"{side}-phase-three-monster",
				"PhaseThree",
				null),
			secondToken,
			ErasureMutationKind.Attached);

		return string.Join(
			"|",
			fixture.Lineage.MutationJournal.Select(record =>
				$"{record.MutationOrdinal}:" +
				$"{record.Token.OperationSequence}:" +
				$"{record.Token.ParentAdmissionOrdinal}:" +
				$"{record.Kind}:{record.Admission}"));
	}

	private static void CanonicalTerminationBeginsOnce()
	{
		Fixture fixture = Fixture.Create();

		Assert.True(fixture.Lineage.TryBeginCanonicalTermination());
		Assert.False(fixture.Lineage.TryBeginCanonicalTermination());
	}

	private static void MatchingRevisionPermitsCertification()
	{
		Fixture fixture = Fixture.Create();

		Assert.True(fixture.Lineage.TryIssueCompletionCertificate(
			fixture.Lineage.ActivityRevision,
			fixture.Lineage.MemberCount));
		Assert.True(fixture.Lineage.TryGetCompletionCertificate(out _));
	}

	private static void NewMemberInvalidatesCertification()
	{
		Fixture fixture = Fixture.Create();
		Assert.True(fixture.Lineage.TryIssueCompletionCertificate(
			fixture.Lineage.ActivityRevision,
			fixture.Lineage.MemberCount));
		Assert.True(fixture.Lineage.TryGetCompletionCertificate(out _));

		fixture.Lineage.ObserveCausal(
			Evidence("replacement", 2, "replacement-monster", "Replacement", null),
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root),
			ErasureMutationKind.Created);

		Assert.False(fixture.Lineage.TryGetCompletionCertificate(out _));
	}

	private static void TokenRemainsValidAfterCertification()
	{
		Fixture fixture = Fixture.Create();
		ErasureContinuationToken token =
			fixture.Lineage.CreateContinuationToken(fixture.Lineage.Root);
		Assert.True(fixture.Lineage.TryIssueCompletionCertificate(
			fixture.Lineage.ActivityRevision,
			fixture.Lineage.MemberCount));

		ErasureAdmission admission = fixture.Lineage.ObserveCausal(
			Evidence("late", 2, "late-monster", "LatePhase", "Z"),
			token,
			ErasureMutationKind.Created);

		Assert.Equal(ErasureAdmissionKind.CausalToken, admission.Kind);
		Assert.False(fixture.Lineage.TryGetCompletionCertificate(out _));
	}

	private static void UnrelatedActivityCannotClaimLateSuccessor()
	{
		Fixture fixture = Fixture.Create();
		for (int index = 0; index < 1000; index++)
		{
			fixture.Lineage.MarkActivity();
		}

		ErasureAdmission admission = fixture.Lineage.TryAdmitStrong(
			Evidence(
				"late-unrelated",
				2,
				"late-unrelated-monster",
				fixture.Root.MonsterType,
				fixture.Root.SlotName));

		Assert.Equal(ErasureAdmissionKind.None, admission.Kind);
		Assert.Equal(1, fixture.Lineage.MemberCount);
	}

	private static void CompletionAcceptsCertifiedState()
	{
		ErasureCompletionSnapshot snapshot = CertifiedSnapshot();

		Assert.True(ErasureCompletionPolicy.CanEndNormally(snapshot));
		Assert.Equal(
			ErasureCompletionDecision.AllowNormalEnd,
			ErasureCompletionPolicy.Evaluate(snapshot));
	}

	private static void CompletionRejectsUnsafeGates()
	{
		ErasureCompletionSnapshot safe = CertifiedSnapshot();
		(ErasureCompletionSnapshot Snapshot, ErasureCompletionDecision Decision)[]
			unsafeStates =
			[
				(safe with { IsExpectedCombat = false },
					ErasureCompletionDecision.DifferentCombat),
				(safe with { IsInProgress = false },
					ErasureCompletionDecision.CombatNotInProgress),
				(safe with { IsStarting = true },
					ErasureCompletionDecision.CombatStarting),
				(safe with { HasPendingLoss = true },
					ErasureCompletionDecision.PendingLoss),
				(safe with { HasLivingPlayer = false },
					ErasureCompletionDecision.NoLivingPlayer),
				(safe with { HasTrackedLineage = false },
					ErasureCompletionDecision.NoTrackedLineage),
				(safe with { HasOpenPersistenceLease = true },
					ErasureCompletionDecision.PersistenceLeaseOpen),
				(safe with { IsCompletionArmed = false },
					ErasureCompletionDecision.CompletionNotArmed),
				(safe with { AreAllLineagesCertified = false },
					ErasureCompletionDecision.UncertifiedLineage),
				(safe with { HasActiveConvergence = true },
					ErasureCompletionDecision.ActiveConvergence),
				(safe with { HasLivingUntrackedPrimaryEnemy = true },
					ErasureCompletionDecision.LivingPrimaryEnemy),
				(safe with { IsBlockedByCombatEndHook = true },
					ErasureCompletionDecision.CombatEndHookBlocked)
			];

		foreach ((ErasureCompletionSnapshot snapshot,
			ErasureCompletionDecision expected) in unsafeStates)
		{
			Assert.False(ErasureCompletionPolicy.CanEndNormally(snapshot));
			Assert.Equal(expected, ErasureCompletionPolicy.Evaluate(snapshot));
		}
	}

	private static void NonPrimarySummonsDoNotBlockCompletion()
	{
		ErasureCompletionSnapshot snapshot = CertifiedSnapshot();

		Assert.False(snapshot.HasLivingUntrackedPrimaryEnemy);
		Assert.True(ErasureCompletionPolicy.CanEndNormally(snapshot));
	}

	private static void LivingPrimaryEnemyBlocksCompletion()
	{
		ErasureCompletionSnapshot snapshot = CertifiedSnapshot() with
		{
			HasLivingUntrackedPrimaryEnemy = true
		};

		Assert.Equal(
			ErasureCompletionDecision.LivingPrimaryEnemy,
			ErasureCompletionPolicy.Evaluate(snapshot));
	}

	private static void CanonicalDeathAnimationIsPreserved()
	{
		ErasureVisualExitSnapshot setup = new(
			IsExactNode: true,
			IsReserved: true,
			IsInRemovingList: false,
			HasIncompleteDeathAnimation: false,
			IsCanonicalTerminationActive: true);
		ErasureVisualExitSnapshot continuing = setup with
		{
			IsInRemovingList = true,
			HasIncompleteDeathAnimation = true,
			IsCanonicalTerminationActive = false,
		};

		Assert.True(ErasureVisualExitPolicy.ShouldPreserve(setup));
		Assert.True(ErasureVisualExitPolicy.ShouldPreserve(continuing));
	}

	private static void VisualExitRequiresEverySafetyGate()
	{
		ErasureVisualExitSnapshot safe = new(
			IsExactNode: true,
			IsReserved: true,
			IsInRemovingList: true,
			HasIncompleteDeathAnimation: true,
			IsCanonicalTerminationActive: false);
		ErasureVisualExitSnapshot[] unsafeStates =
		[
			safe with { IsExactNode = false },
			safe with { IsReserved = false },
			safe with { IsInRemovingList = false },
			safe with { HasIncompleteDeathAnimation = false },
			safe with
			{
				IsInRemovingList = false,
				HasIncompleteDeathAnimation = false
			}
		];

		foreach (ErasureVisualExitSnapshot snapshot in unsafeStates)
		{
			Assert.False(ErasureVisualExitPolicy.ShouldPreserve(snapshot));
		}
	}

	private static void SealedCombatRejectsLateEnemyIngress()
	{
		ErasureTerminalIngressSnapshot snapshot = new(
			HasTrackedCombat: true,
			IsEnemy: true,
			IsBaselineEnemy: false,
			IsTerminalSealed: true,
			IsCompletionFlightRunning: false,
			IsExpectedCombat: false,
			IsInProgress: false);

		Assert.Equal(
			ErasureTerminalIngressDecision.RejectTerminalIngress,
			ErasureTerminalIngressPolicy.Evaluate(snapshot));
	}

	private static void SealedCombatPreservesBaselineEnemies()
	{
		ErasureTerminalIngressSnapshot snapshot = new(
			HasTrackedCombat: true,
			IsEnemy: true,
			IsBaselineEnemy: true,
			IsTerminalSealed: true,
			IsCompletionFlightRunning: true,
			IsExpectedCombat: true,
			IsInProgress: true);

		Assert.Equal(
			ErasureTerminalIngressDecision.Allow,
			ErasureTerminalIngressPolicy.Evaluate(snapshot));
	}

	private static void CompletionCommitRejectsDelayedEnemyIngress()
	{
		ErasureTerminalIngressSnapshot snapshot = new(
			HasTrackedCombat: true,
			IsEnemy: true,
			IsBaselineEnemy: false,
			IsTerminalSealed: false,
			IsCompletionFlightRunning: true,
			IsExpectedCombat: true,
			IsInProgress: false);

		Assert.Equal(
			ErasureTerminalIngressDecision.RejectTerminalIngress,
			ErasureTerminalIngressPolicy.Evaluate(snapshot));
		Assert.Equal(
			ErasureTerminalIngressDecision.RejectTerminalIngress,
			ErasureTerminalIngressPolicy.Evaluate(
				snapshot with { IsExpectedCombat = false }));
	}

	private static void TerminalIngressGuardPreservesValidAdditions()
	{
		ErasureTerminalIngressSnapshot active = new(
			HasTrackedCombat: true,
			IsEnemy: true,
			IsBaselineEnemy: false,
			IsTerminalSealed: false,
			IsCompletionFlightRunning: true,
			IsExpectedCombat: true,
			IsInProgress: true);

		Assert.Equal(
			ErasureTerminalIngressDecision.CompletionNotCommitted,
			ErasureTerminalIngressPolicy.Evaluate(active));
		Assert.Equal(
			ErasureTerminalIngressDecision.FriendlyCreature,
			ErasureTerminalIngressPolicy.Evaluate(
				active with { IsEnemy = false }));
		Assert.Equal(
			ErasureTerminalIngressDecision.NoTrackedCombat,
			ErasureTerminalIngressPolicy.Evaluate(
				active with { HasTrackedCombat = false }));
	}

	private static void CommittedLineageDiscardsDeferredCallbacks()
	{
		ErasureDeferredCallbackSnapshot committed = new(
			HasTrackedScope: true,
			IsExpectedCombat: true,
			IsInProgress: true,
			IsTerminalSealed: false,
			IsCompletionFlightRunning: true,
			IsLineageCertified: true);

		Assert.False(
			ErasureDeferredCallbackPolicy.ShouldExecute(committed));
		Assert.Equal(
			ErasureDeferredCallbackDecision.DiscardCommittedLineage,
			ErasureDeferredCallbackPolicy.Evaluate(committed));
		Assert.Equal(
			ErasureDeferredCallbackDecision.DiscardStaleCombat,
			ErasureDeferredCallbackPolicy.Evaluate(
				committed with { IsCompletionFlightRunning = false, IsTerminalSealed = true }));
		Assert.Equal(
			ErasureDeferredCallbackDecision.DiscardStaleCombat,
			ErasureDeferredCallbackPolicy.Evaluate(
				committed with { IsCompletionFlightRunning = false, IsExpectedCombat = false }));
	}

	private static void UnresolvedLineagePreservesDeferredCallbacks()
	{
		ErasureDeferredCallbackSnapshot active = new(
			HasTrackedScope: true,
			IsExpectedCombat: true,
			IsInProgress: true,
			IsTerminalSealed: false,
			IsCompletionFlightRunning: false,
			IsLineageCertified: false);

		Assert.True(
			ErasureDeferredCallbackPolicy.ShouldExecute(active));
		Assert.Equal(
			ErasureDeferredCallbackDecision.ExecuteUncertified,
			ErasureDeferredCallbackPolicy.Evaluate(active));
		Assert.True(
			ErasureDeferredCallbackPolicy.ShouldExecute(
				active with
				{
					IsCompletionFlightRunning = true,
					IsLineageCertified = false
				}));
		Assert.True(
			ErasureDeferredCallbackPolicy.ShouldExecute(
				active with { HasTrackedScope = false }));
	}

	private static ErasureCompletionSnapshot CertifiedSnapshot()
	{
		return new ErasureCompletionSnapshot(
			IsExpectedCombat: true,
			IsInProgress: true,
			IsStarting: false,
			HasPendingLoss: false,
			HasLivingPlayer: true,
			HasTrackedLineage: true,
			HasOpenPersistenceLease: false,
			IsCompletionArmed: true,
			AreAllLineagesCertified: true,
			HasActiveConvergence: false,
			HasLivingUntrackedPrimaryEnemy: false,
			IsBlockedByCombatEndHook: false);
	}

	private static void LoaderSelectionIsFailClosed()
	{
		Version[] targets =
		[
			new Version(0, 107, 1),
			new Version(0, 110, 0)
		];

		Assert.Equal(
			"0.110.0",
			VariantSelectionPolicy.PickCompatibleVersion(targets, host: null)
				?.ToString() ?? "<null>");
		Assert.Equal(
			"0.107.1",
			VariantSelectionPolicy.PickCompatibleVersion(
				targets,
				new Version(0, 109, 0))
				?.ToString() ?? "<null>");
		Assert.Null(
			VariantSelectionPolicy.PickCompatibleVersion(
				targets,
				new Version(0, 107, 0)));
	}

	private static ErasureEvidence Evidence(
		string creature,
		uint? combatId,
		string monster,
		string monsterType,
		string? slot,
		bool primary = false,
		bool enemy = true)
	{
		return Evidence(
			new Ref(creature),
			combatId,
			new Ref(monster),
			monsterType,
			slot,
			primary,
			enemy);
	}

	private static ErasureEvidence Evidence(
		string creature,
		uint? combatId,
		object monster,
		string monsterType,
		string? slot,
		bool primary = false,
		bool enemy = true)
	{
		return Evidence(
			new Ref(creature),
			combatId,
			monster,
			monsterType,
			slot,
			primary,
			enemy);
	}

	private static ErasureEvidence Evidence(
		object creature,
		uint? combatId,
		object? monster,
		string monsterType,
		string? slot,
		bool primary = false,
		bool enemy = true)
	{
		return new ErasureEvidence(
			creature,
			combatId,
			monster,
			monsterType,
			slot,
			enemy,
			primary);
	}

	private sealed record Ref(string Name);

	private readonly record struct Fixture(
		ErasureLineage Lineage,
		ErasureEvidence Root,
		ErasureEvidence? Peer)
	{
		public static Fixture Create(
			long operationSequence = 1,
			string creatureName = "root",
			string monsterName = "root-monster",
			string? slot = "A")
		{
			ErasureEvidence root = Evidence(
				creatureName,
				1,
				monsterName,
				"RootType",
				slot);
			return new Fixture(
				new ErasureLineage(operationSequence, root, [root]),
				root,
				null);
		}

		public static Fixture CreateWithPeer(bool sameTypeAndSlot = false)
		{
			ErasureEvidence root =
				Evidence("root", 1, "root-monster", "RootType", "A");
			ErasureEvidence peer = Evidence(
				"peer",
				2,
				"peer-monster",
				sameTypeAndSlot ? root.MonsterType : "PeerType",
				sameTypeAndSlot ? root.SlotName : "B");
			return new Fixture(
				new ErasureLineage(1, root, [root, peer]),
				root,
				peer);
		}
	}

	private static class Assert
	{
		public static void True(bool condition)
		{
			if (!condition)
			{
				throw new InvalidOperationException("Expected true, got false.");
			}
		}

		public static void False(bool condition)
		{
			if (condition)
			{
				throw new InvalidOperationException("Expected false, got true.");
			}
		}

		public static void Null(object? value)
		{
			if (value != null)
			{
				throw new InvalidOperationException(
					$"Expected null, got {value}.");
			}
		}

		public static void Same(object expected, object? actual)
		{
			if (!ReferenceEquals(expected, actual))
			{
				throw new InvalidOperationException(
					"Expected both values to reference the same object.");
			}
		}

		public static void Equal<T>(T expected, T actual)
			where T : notnull
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
			{
				throw new InvalidOperationException(
					$"Expected {expected}, got {actual}.");
			}
		}
	}
}
