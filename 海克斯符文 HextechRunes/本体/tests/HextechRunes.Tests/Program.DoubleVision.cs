using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HextechRunes;
using FormVfxKind = HextechRunes.HextechFormVfxSafetyHooks.FormVfxKind;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void UniversalScopeChancesAddBeforeSingleRoll()
	{
		Equal(15, UniversalScopeRuneBase.CombineChancePercent([ 15 ]), "one scope keeps its own chance");
		Equal(45, UniversalScopeRuneBase.CombineChancePercent([ 15, 30 ]), "two scope chances add directly");
		Equal(95, UniversalScopeRuneBase.CombineChancePercent([ 15, 30, 50 ]), "all scope chances add directly");
		Equal(100, UniversalScopeRuneBase.CombineChancePercent([ 50, 50, 30 ]), "combined chance is capped at certainty");
	}

	private static void UniversalScopeUpgradeRestorationKeepsCapturedLevels()
	{
		Equal(3, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(0, 3, 30), "restore all lost multi-upgrade levels");
		Equal(2, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(1, 3, 30), "restore only missing levels");
		Equal(0, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(3, 3, 30), "preserve an unchanged card");
		Equal(0, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(4, 3, 30), "never downgrade a card that gained levels while moving");
		Equal(1, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(0, 3, 1), "respect the card max upgrade level");
	}

	private static void EventRewardTransactionCommitsSequentially()
	{
		EventRewardTransaction<int> transaction = new();
		transaction.Record(1);
		transaction.Record(2);
		TaskCompletionSource firstGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
		List<int> started = [];
		List<int> completed = [];

		Task commitTask = transaction.CommitSequentially(async item =>
		{
			started.Add(item);
			if (item == 1)
			{
				await firstGate.Task;
			}
			completed.Add(item);
		});

		Expect(started.SequenceEqual([1]), "second event reward must not start before the first reward completes");
		firstGate.SetResult();
		commitTask.GetAwaiter().GetResult();
		Expect(started.SequenceEqual([1, 2]), "event rewards should start in obtain order");
		Expect(completed.SequenceEqual([1, 2]), "event rewards should complete sequentially");
	}

	private static void EventRewardTransactionRejectsLateRecordsAndSecondCommit()
	{
		EventRewardTransaction<int> transaction = new();
		transaction.Record(1);
		transaction.CommitSequentially(static _ => Task.CompletedTask).GetAwaiter().GetResult();

		ExpectThrows<InvalidOperationException>(
			() => transaction.Record(2),
			"sealed event transaction should reject late records");
		ExpectThrows<InvalidOperationException>(
			() => transaction.CommitSequentially(static _ => Task.CompletedTask).GetAwaiter().GetResult(),
			"event transaction should not commit twice");
	}

	private static void EventRewardTransactionTryRecordSkipsLateAsyncRewards()
	{
		EventRewardTransaction<int> transaction = new();
		Expect(transaction.TryRecord(1), "open event transaction should accept its original reward");
		transaction.CloseForRecording();
		Expect(!transaction.TryRecord(2), "closed event transaction should ignore inherited async rewards");

		List<int> committed = [];
		transaction.CommitSequentially(item =>
		{
			committed.Add(item);
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(committed.SequenceEqual([1]), "late inherited reward must not enter the committed event batch");
	}

	private static void DoubleVisionCopiesTrackedCardsWhenMultiSelectEndsWithoutCompletingReward()
	{
		Expect(
			DoubleVisionRune.ShouldDuplicateTrackedCardRewards(rewardComplete: false, addedCardCount: 1),
			"cards already obtained through Hattrick must still be duplicated when the reward ends via Skip");
		Expect(
			DoubleVisionRune.ShouldDuplicateTrackedCardRewards(rewardComplete: true, addedCardCount: 2),
			"all tracked cards from a completed multi-select reward should be duplicated");
		Expect(
			!DoubleVisionRune.ShouldDuplicateTrackedCardRewards(rewardComplete: false, addedCardCount: 0),
			"an empty skipped reward must not create a card copy");
	}

	private static void DoubleVisionCopiesWaxStateWithoutCopyingMeltedState()
	{
		DustyTome source = CreateBareTestDustyTome();
		source.IsWax = true;
		source.IsMelted = true;
		DustyTome copy = CreateBareTestDustyTome();

		DoubleVisionRune.CopyWaxState(source, copy);

		Expect(copy.IsWax, "Double Vision should preserve wax on a copied relic");
		Expect(!copy.IsMelted, "Double Vision should not copy an already-melted state");
	}

	private static void DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect()
	{
		DustyTome source = CreateTestDustyTome();
		source.IsWax = true;
		DustyTome? unrelated = null;
		int obtainCount = 0;
		int ancientCardGrantCount = 0;
		int broadcastCount = 0;

		DustyTome copy = DoubleVisionRune.DuplicateDustyTomeSpecializedForTest(
			source,
			syncReward: false,
			obtainCopy: candidate =>
			{
				obtainCount++;
				unrelated = CreateTestDustyTome();
				Expect(DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(candidate), "copied Dusty Tome should suppress its own AfterObtained");
				Task copiedAfterObtained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
				bool runCopiedAfterObtained = HextechRewardSafetyHooks.DustyTomePatch.Prefix(candidate, ref copiedAfterObtained);
				if (runCopiedAfterObtained)
				{
					ancientCardGrantCount++;
				}
				Expect(!runCopiedAfterObtained, "copied Dusty Tome AfterObtained prefix should skip the original");
				Expect(copiedAfterObtained.IsCompletedSuccessfully, "copied Dusty Tome AfterObtained should return a completed task");
				Expect(!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(source), "source Dusty Tome must not be suppressed");
				Task sourceAfterObtained = Task.CompletedTask;
				Expect(
					HextechRewardSafetyHooks.DustyTomePatch.Prefix(source, ref sourceAfterObtained),
					"source Dusty Tome AfterObtained must still run");
				Expect(!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(unrelated), "unrelated Dusty Tome must not be suppressed");
				return Task.FromResult(candidate);
			},
			synchronize: _ => broadcastCount++,
			createCopy: CreateBareTestDustyTome,
			assignAncientCard: SetTestDustyTomeAncientCard)
			.GetAwaiter()
			.GetResult();

		Equal(1, obtainCount, "single-player Dusty Tome obtain count");
		Equal(0, ancientCardGrantCount, "single-player duplicated AncientCard grant count");
		Equal(0, broadcastCount, "single-player Dusty Tome broadcast count");
		Expect(!ReferenceEquals(source, copy), "DoubleVision should create a second Dusty Tome instance");
		Equal(source.AncientCard, copy.AncientCard, "copied Dusty Tome AncientCard");
		Expect(copy.IsWax, "copied Dusty Tome should preserve wax");
		Expect(!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(copy), "Dusty Tome suppression must end after obtain");
	}

	private static void DoubleVisionDustyTomeSaveLoadPreservesAncientCard()
	{
		DustyTome source = CreateTestDustyTome();
#if STS2_109_OR_NEWER
		// 测试宿主不会执行原版 Init；仅在测试进程用官方 Debug 入口补齐原版载体。
		// 生产代码仍禁止调用该入口，以免绕过 0.109 的 SavedProperty wire hash。
		MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache
			.CacheSavedPropertiesForTypeDebug(typeof(DustyTome));
#else
		HextechSavedPropertyBootstrap.InjectModelType(typeof(DustyTome));
#endif
		DustyTome copy = DoubleVisionRune.DuplicateDustyTomeSpecializedForTest(
			source,
			syncReward: false,
			obtainCopy: Task.FromResult,
			synchronize: static _ => throw new InvalidOperationException("save test must not broadcast"),
			createCopy: CreateBareTestDustyTome,
			assignAncientCard: SetTestDustyTomeAncientCard)
			.GetAwaiter()
			.GetResult();

		SerializableRelic saved = copy.ToSerializable();
		Expect(saved.Props != null, "Dusty Tome AncientCard was not written to SerializableRelic");
		JsonSerializerOptions saveJsonOptions = new() { IncludeFields = true };
		string json = JsonSerializer.Serialize(saved, saveJsonOptions);
		SerializableRelic loaded = JsonSerializer.Deserialize<SerializableRelic>(json, saveJsonOptions)
			?? throw new InvalidOperationException("Dusty Tome SerializableRelic failed to deserialize");
		ModelId restoredAncientCard = loaded.Props?.modelIds?
			.Single(property => property.name == nameof(DustyTome.AncientCard))
			.value
			?? throw new InvalidOperationException("loaded Dusty Tome is missing AncientCard");

		Expect(
			saved.Props?.modelIds?.Any(property => property.name == nameof(DustyTome.AncientCard)
				&& property.value == source.AncientCard) == true,
			"Dusty Tome save should contain the copied AncientCard model id");
		Equal(copy.Id, loaded.Id, "restored Dusty Tome relic id");
		Equal(source.AncientCard, (ModelId?)restoredAncientCard, "restored Dusty Tome AncientCard");
	}

	private static void DoubleVisionDustyTomeEventMultiplayerRunsOnEveryPeerWithoutBroadcast()
	{
		DustyTome source = CreateTestDustyTome();
		int hostObtainCount = 0;
		int clientObtainCount = 0;
		int broadcastCount = 0;

		DustyTome hostCopy = DoubleVisionRune.DuplicateDustyTomeSpecializedForTest(
			source,
			syncReward: false,
			obtainCopy: candidate =>
			{
				hostObtainCount++;
				Expect(DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(candidate), "host copy should suppress only its own AfterObtained");
				return Task.FromResult(candidate);
			},
			synchronize: _ => broadcastCount++,
			createCopy: CreateBareTestDustyTome,
			assignAncientCard: SetTestDustyTomeAncientCard)
			.GetAwaiter()
			.GetResult();
		DustyTome clientCopy = DoubleVisionRune.DuplicateDustyTomeSpecializedForTest(
			source,
			syncReward: false,
			obtainCopy: candidate =>
			{
				clientObtainCount++;
				Expect(DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(candidate), "client copy should suppress only its own AfterObtained");
				return Task.FromResult(candidate);
			},
			synchronize: _ => broadcastCount++,
			createCopy: CreateBareTestDustyTome,
			assignAncientCard: SetTestDustyTomeAncientCard)
			.GetAwaiter()
			.GetResult();

		Equal(1, hostObtainCount, "host deterministic event obtain count");
		Equal(1, clientObtainCount, "client deterministic event obtain count");
		Equal(0, broadcastCount, "deterministic event Dusty Tome broadcast count");
		Expect(!ReferenceEquals(hostCopy, clientCopy), "each peer should construct its own Dusty Tome instance");
		Equal(hostCopy.Id, clientCopy.Id, "multiplayer Dusty Tome id");
		Equal(hostCopy.AncientCard, clientCopy.AncientCard, "multiplayer Dusty Tome AncientCard");
	}
}
