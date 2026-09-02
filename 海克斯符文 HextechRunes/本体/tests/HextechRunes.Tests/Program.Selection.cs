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
	private static void ActRollRoundTripKeepsHostSnapshot()
	{
		ModelId disabledRune = HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.First();
		HashSet<string> disabledIds = [ disabledRune.Entry ];
		string disabledForgeId = HextechCatalog.GetAllForgeTypes()
			.Select(ModelDb.GetId)
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.First()
			.Entry;
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot() with
		{
			PlayerHexCountsByAct = [ 2, 0, 8 ],
			EnemyHexCountsByAct = [ -1, 7, 3 ],
			DisabledPlayerRuneIds = disabledIds,
			DisabledMonsterHexIds = [ MonsterHexKind.FrostWraith.ToString() ],
			DisabledForgeIds = [ disabledForgeId ],
			RuneRarityWeightsByAct =
			[
				new HextechRarityWeights(4, 5, 6),
				new HextechRarityWeights(7, 8, 9),
				new HextechRarityWeights(10, 11, 12)
			],
			PreventConsecutiveSilverRunes = false,
			GoldenRerollChancePercent = 37,
			ForgeRarityWeights = new HextechForgeRarityWeights(9, 10, 11),
			RandomForgeShopPrice = 123,
			PlayerRuneRerollLimit = 8,
			MonsterHexRerollLimit = HextechRuneConfiguration.InfiniteRerollLimit
		};

		PlayerChoiceResult result = HextechChoiceCodec.CreateActRoll(
			actIndex: 1,
			rarity: HextechRarityTier.Gold,
			monsterHex: MonsterHexKind.ShrinkRay,
			hostUsesBetterMultiplayerScaling: true,
			enemyHexCountsByAct: [ -1, 7, 3 ],
			disabledPlayerRuneIds: disabledIds,
			runConfigurationSnapshot: snapshot);

		Expect(HextechChoiceCodec.TryDecodeActRoll(
			result,
			expectedActIndex: 1,
			out HextechRarityTier rarity,
			out MonsterHexKind? monsterHex,
			out bool hostUsesBetterMultiplayerScaling,
			out int[] enemyHexCountsByAct,
			out HashSet<string> decodedDisabledIds,
			out HextechRunConfigurationSnapshot decodedSnapshot), "act roll should decode");

		Equal(HextechRarityTier.Gold, rarity, "rarity");
		Equal(MonsterHexKind.ShrinkRay, monsterHex, "monster hex");
		Equal(true, hostUsesBetterMultiplayerScaling, "host scaling flag");
		SequenceEqual(new[] { 0, 6, 3 }, enemyHexCountsByAct, "enemy count snapshot");
		Expect(decodedDisabledIds.Contains(disabledRune.Entry), "disabled player rune id should round-trip");
		SequenceEqual(new[] { 2, 0, 6 }, decodedSnapshot.PlayerHexCountsByAct, "player count snapshot");
		SetEqual([ MonsterHexKind.FrostWraith.ToString() ], decodedSnapshot.DisabledMonsterHexIds, "disabled monster hex ids");
		SetEqual([ disabledForgeId ], decodedSnapshot.DisabledForgeIds, "disabled forge ids");
		Equal(123, decodedSnapshot.RandomForgeShopPrice, "forge shop price");
		Equal(8, decodedSnapshot.PlayerRuneRerollLimit, "player reroll limit");
		Equal(HextechRuneConfiguration.InfiniteRerollLimit, decodedSnapshot.MonsterHexRerollLimit, "monster reroll limit");
		SequenceEqual(
			new[]
			{
				new HextechRarityWeights(4, 5, 6),
				new HextechRarityWeights(7, 8, 9),
				new HextechRarityWeights(10, 11, 12)
			},
			decodedSnapshot.RuneRarityWeightsByAct,
			"rune rarity weights by act");
		Equal(false, decodedSnapshot.PreventConsecutiveSilverRunes, "prevent consecutive Silver toggle");
		Equal(37, decodedSnapshot.GoldenRerollChancePercent, "golden reroll chance");
		Equal(10, decodedSnapshot.ForgeRarityWeights.Gold, "forge rarity weight");
		Expect(!HextechChoiceCodec.TryDecodeActRoll(result, 0, out _, out _, out _, out _, out _), "wrong act should be rejected");
	}

	private static void RuneSelectionRoundTripRequiresMatchingActAndOrdinal()
	{
		RelicModel[] finalOptions = CreateRuneSelectionTestOptions(3);
		ModelId[] finalOptionIds = finalOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToArray();
		PlayerChoiceResult result = HextechChoiceCodec.CreateRuneSelection(
			actIndex: 1,
			choiceOrdinal: 2,
			selectedIndex: 1,
			rerollHistory: [ 2, 0 ],
			finalOptions);

		Expect(HextechChoiceCodec.IsRuneSelection(result), "rune selection kind predicate should decode");
		Expect(HextechChoiceCodec.IsRuneSelection(result, 1, 2), "matching rune selection act and ordinal should decode");
		Expect(HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 2, out int selectedIndex, out List<int> rerollHistory, out List<ModelId> decodedFinalOptionIds), "matching rune selection should decode");
		Equal(1, selectedIndex, "selected index");
		SequenceEqual(new[] { 2, 0 }, rerollHistory, "reroll history");
		SequenceEqual(finalOptionIds, decodedFinalOptionIds, "final option ids");
	}

	private static void RuneSelectionRejectsWrongActOrOrdinal()
	{
		PlayerChoiceResult result = HextechChoiceCodec.CreateRuneSelection(
			actIndex: 1,
			choiceOrdinal: 2,
			selectedIndex: 0,
			rerollHistory: [],
			CreateRuneSelectionTestOptions(3));

		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(result, 0, 2, out _, out _, out _), "wrong rune selection act should be rejected");
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 1, out _, out _, out _), "wrong rune selection ordinal should be rejected");

		PlayerChoiceResult malformed = PlayerChoiceResult.FromIndexes(new List<int> { Magic, ChoiceKindRuneSelection, 1, 2, 0, 2, 0 });
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(malformed, 1, 2, out _, out _, out _), "malformed rune selection should be rejected");
	}

	private static void ActSelectionAppliedRejectsWrongActOrOrdinal()
	{
		PlayerChoiceResult result = HextechChoiceCodec.CreateActSelectionApplied(2, 3);

		Expect(HextechChoiceCodec.TryDecodeActSelectionApplied(result, 2, 3), "matching act and ordinal should decode");
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(result, 1, 3), "wrong act should be rejected");
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(result, 2, 2), "wrong ordinal should be rejected");

		PlayerChoiceResult malformed = PlayerChoiceResult.FromIndexes(new List<int> { Magic, ChoiceKindActSelectionApplied, 2, 3, 0 });
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(malformed, 2, 3), "missing applied flag should be rejected");
	}

	private static void EnemyHexAdjustmentRoundTripKeepsAllSlots()
	{
		const int OperationToken = 112233;
		EnemyHexAdjustmentPayload source = new(
			ActIndex: 0,
			Sequence: 12,
			MonsterHexes:
			[
				MonsterHexKind.FrostWraith,
				null,
				MonsterHexKind.PandorasBox
			],
			RerollCounts: [ 2, -3 ],
			IsFinal: true);

		PlayerChoiceResult result = HextechChoiceCodec.CreateEnemyHexAdjustment(OperationToken, source);

		Expect(HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 0, 12, out EnemyHexAdjustmentPayload decoded), "enemy adjustment should decode");
		Equal(0, decoded.ActIndex, "act");
		Equal(12, decoded.Sequence, "sequence");
		Equal(true, decoded.IsFinal, "final flag");
		SequenceEqual(source.MonsterHexes, decoded.MonsterHexes, "monster hex slots");
		SequenceEqual(new[] { 2, 0, 0 }, decoded.RerollCounts, "reroll counts");
		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 12, out _), "wrong act should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsInvalidHex()
	{
		const int OperationToken = 223344;
		PlayerChoiceResult result = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			1,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			1,
			int.MaxValue,
			0
		});

		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 0, 1, out _), "invalid monster hex enum should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsUnexpectedSequence()
	{
		const int OperationToken = 334455;
		EnemyHexAdjustmentPayload source = new(
			ActIndex: 1,
			Sequence: 3,
			MonsterHexes: [ MonsterHexKind.FrostWraith ],
			RerollCounts: [ 0 ],
			IsFinal: false);
		PlayerChoiceResult result = HextechChoiceCodec.CreateEnemyHexAdjustment(OperationToken, source);

		Expect(
			HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 3, out _),
			"exact enemy adjustment sequence should decode");
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 2, out _),
			"stale enemy adjustment sequence should be rejected");
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 4, out _),
			"future enemy adjustment sequence should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsExtremeCounts()
	{
		const int OperationToken = 445566;
		PlayerChoiceResult extremeHexCount = PlayerChoiceResult.FromIndexes(
		[
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			0,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			int.MaxValue
		]);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(extremeHexCount, OperationToken, 0, 0, out _),
			"extreme enemy hex count should be rejected without allocation");

		PlayerChoiceResult extremeRerollCount = PlayerChoiceResult.FromIndexes(
		[
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			0,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			0,
			int.MaxValue
		]);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(extremeRerollCount, OperationToken, 0, 0, out _),
			"extreme enemy reroll count should be rejected without allocation");
	}

	private static void LegacyEnemyHexAdjustmentIsRejected()
	{
		PlayerChoiceResult result = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindEnemyHexAdjustment,
			1,
			9,
			0,
			(int)MonsterHexKind.FrostWraith,
			2,
			1
		});

		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 556677, 1, 9, out _),
			"legacy enemy adjustment payload should be rejected after the protocol gate");
	}

	private static void RandomRuneGrantRoundTripKeepsStableModelIds()
	{
		const int OperationToken = 667788;
		ModelId[] source =
		[
			new("HEXTECH_TEST", "FIRST_RUNE"),
			new("HEXTECH_TEST", "SECOND_RUNE")
		];

		PlayerChoiceResult result = HextechChoiceCodec.CreateRandomRuneGrant(OperationToken, source);

		Expect(HextechChoiceCodec.TryDecodeRandomRuneGrant(result, OperationToken, out List<ModelId> decoded), "random grant should decode");
		SequenceEqual(source, decoded, "stable model ids");
		Expect(HextechChoiceCodec.IsRandomRuneGrant(result, OperationToken), "random grant predicate");
	}

	private static void RandomRuneGrantRejectsMalformedStableModelIdList()
	{
		const int OperationToken = 778899;
		PlayerChoiceResult tooManyIds = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			StableModelIdListVersion,
			65
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(tooManyIds, OperationToken, out _), "oversized stable id list should be rejected");

		PlayerChoiceResult badSerializedId = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			StableModelIdListVersion,
			1,
			3,
			'B',
			'A',
			'D'
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(badSerializedId, OperationToken, out _), "malformed model id should be rejected");

		PlayerChoiceResult runeSelectionWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRuneSelection,
			1,
			2,
			0,
			0,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeRuneSelection(runeSelectionWithOutOfRangeLegacyOrdinal, 1, 2, out _, out _, out _),
			"out-of-range legacy rune selection ordinal should be rejected");

		PlayerChoiceResult forgeSelectionWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindForgeSelection,
			OperationToken,
			0,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeForgeSelection(forgeSelectionWithOutOfRangeLegacyOrdinal, OperationToken, out _, out _),
			"out-of-range legacy forge selection ordinal should be rejected");
		Expect(
			HextechChoiceCodec.IsMalformedForgeSelectionEnvelope(forgeSelectionWithOutOfRangeLegacyOrdinal, OperationToken),
			"malformed forge selection envelope should remain identifiable");

		PlayerChoiceResult malformedRelicOptionSelection = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRelicOptionSelection,
			OperationToken,
			0,
			StableModelIdListVersion
		});
		Expect(
			HextechChoiceCodec.IsMalformedRelicOptionSelectionEnvelope(malformedRelicOptionSelection, OperationToken),
			"malformed relic option envelope should remain identifiable");

		PlayerChoiceResult randomGrantWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeRandomRuneGrant(randomGrantWithOutOfRangeLegacyOrdinal, OperationToken, out _),
			"out-of-range legacy random grant ordinal should be rejected");
	}

	private static void RelicOptionSelectionRoundTripRequiresMatchingOptions()
	{
		const int OperationToken = 889900;
		RelicModel[] options = CreateRuneSelectionTestOptions(2);
		ModelId[] optionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToArray();
		PlayerChoiceResult result = HextechChoiceCodec.CreateRelicOptionSelection(OperationToken, 1, options);

		Expect(HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, options), "matching relic option selection should be expected");
		Expect(HextechChoiceCodec.TryDecodeRelicOptionSelection(result, OperationToken, out int selectedIndex, out List<ModelId> decodedOptionIds), "relic option selection should decode");
		Equal(1, selectedIndex, "selected relic option index");
		SequenceEqual(optionIds, decodedOptionIds, "relic option ids");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, options.Reverse().ToArray()), "reordered relic options should not be expected");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, CreateRuneSelectionTestOptions(3)), "different relic option count should not be expected");
	}

	private static void OperationTokensRejectCrossedPayloads()
	{
		const uint ChoiceId = 42;
		const ulong PlayerNetId = 9001;
		int forgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:0");
		int sameForgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:0");
		int crossedForgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:1");
		Equal(forgeToken, sameForgeToken, "operation token must be stable");
		Expect(forgeToken != crossedForgeToken, "different stable contexts should produce different operation tokens");

		RelicModel[] options = CreateRuneSelectionTestOptions(2);
		PlayerChoiceResult forge = HextechChoiceCodec.CreateForgeSelection(forgeToken, 0, options);
		Expect(HextechChoiceCodec.TryDecodeForgeSelection(forge, forgeToken, out _, out _), "matching forge operation should decode");
		Expect(!HextechChoiceCodec.TryDecodeForgeSelection(forge, crossedForgeToken, out _, out _), "crossed forge operation should be rejected");

		int relicToken = HextechChoiceCodec.ComputeOperationToken(
			"relic-option-selection",
			ChoiceId,
			PlayerNetId,
			"relic-source");
		PlayerChoiceResult relic = HextechChoiceCodec.CreateRelicOptionSelection(relicToken, 1, options);
		Expect(!HextechChoiceCodec.TryDecodeRelicOptionSelection(relic, relicToken + 1, out _, out _), "crossed relic operation should be rejected");

		int randomToken = HextechChoiceCodec.ComputeOperationToken(
			"random-rune-grant",
			ChoiceId,
			PlayerNetId,
			"consume:HEXTECH_TEST:RUNE");
		PlayerChoiceResult random = HextechChoiceCodec.CreateRandomRuneGrant(
			randomToken,
			[ new ModelId("HEXTECH_TEST", "RUNE") ]);
		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(random, randomToken + 1, out _), "crossed random grant operation should be rejected");

		int enemyToken = HextechChoiceCodec.ComputeOperationToken(
			"enemy-hex-adjustment",
			ChoiceId,
			PlayerNetId,
			"act=1;sequence=2");
		EnemyHexAdjustmentPayload enemyPayload = new(
			ActIndex: 1,
			Sequence: 2,
			MonsterHexes: [ MonsterHexKind.FrostWraith ],
			RerollCounts: [ 0 ],
			IsFinal: true);
		PlayerChoiceResult enemy = HextechChoiceCodec.CreateEnemyHexAdjustment(enemyToken, enemyPayload);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(enemy, enemyToken + 1, 1, 2, out _),
			"crossed enemy adjustment operation should be rejected");
	}

	private static void NetworkChoiceTimeoutUsesNominalWallClockSeconds()
	{
		Equal(TimeSpan.Zero, HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(0), "zero timeout");
		Equal(TimeSpan.FromSeconds(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(600), "ack timeout");
		Equal(TimeSpan.FromMinutes(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(36000), "selection timeout");
	}

	private static void StableModelIdListCodecRoundTripsFromNonzeroCursor()
	{
		ModelId[] source =
		[
			new("HEXTECH_TEST", "FIRST"),
			new("HEXTECH_TEST", "SECOND")
		];
		List<int> payload = [ 17, 23 ];

		HextechStableModelIdListCodec.Append(payload, source);

		Expect(HextechStableModelIdListCodec.TryDecode(payload, 2, out List<ModelId> decoded, out int nextCursor), "stable model id list should decode");
		SequenceEqual(source, decoded, "stable model id helper round-trip");
		Equal(payload.Count, nextCursor, "stable model id helper next cursor");
	}

	private static void StableModelIdListCodecRejectsMalformedLength()
	{
		List<int> payload =
		[
			HextechStableModelIdListCodec.Version,
			1,
			129
		];

		Expect(!HextechStableModelIdListCodec.TryDecode(payload, 0, out List<ModelId> decoded, out int nextCursor), "oversized stable model id length should be rejected");
		Expect(decoded.Count == 0, "malformed stable model id list should not keep partial ids");
		Equal(0, nextCursor, "failed stable model id decode should keep original cursor");
	}

	private static void StableModelIdListCodecRejectsEncoderOverflow()
	{
		ModelId id = new("HEXTECH_TEST", "ENTRY");
		ExpectThrows<ArgumentOutOfRangeException>(
			() => HextechStableModelIdListCodec.Append(
				[],
				Enumerable.Repeat(id, HextechStableModelIdListCodec.MaxCount + 1)),
			"stable ModelId encoder should reject more than 64 items");

		ModelId oversized = new(
			new string('C', 64),
			new string('E', HextechStableModelIdListCodec.MaxSerializedLength));
		ExpectThrows<ArgumentException>(
			() => HextechStableModelIdListCodec.Append([], [ oversized ]),
			"stable ModelId encoder should reject a serialized ID longer than 128 characters");

		Expect(
			!HextechStableModelIdListCodec.TryDecode(
				[ StableModelIdListVersion, 0 ],
				-1,
				out _,
				out _),
			"stable ModelId decoder should reject a negative cursor");
	}

	private static void PlayerRuneRarityConfigExcludesFullyDisabledTier()
	{
		HashSet<string> disabledIds = GetConfigurableRuneEntries(HextechRarityTier.Silver);

		IReadOnlyList<HextechRarityTier> enabled = HextechRunePoolBuilder.GetEnabledPlayerRuneRaritiesForDisabledIds(disabledIds);

		Expect(!enabled.Contains(HextechRarityTier.Silver), "fully disabled silver tier should be excluded");
		Expect(enabled.Contains(HextechRarityTier.Gold), "gold tier should remain enabled");
		Expect(enabled.Contains(HextechRarityTier.Prismatic), "prismatic tier should remain enabled");
	}

	private static void PlayerRuneRarityConfigFallsBackWhenAllTiersDisabled()
	{
		HashSet<string> disabledIds = GetConfigurableRuneEntries(
			HextechRarityTier.Silver,
			HextechRarityTier.Gold,
			HextechRarityTier.Prismatic);

		IReadOnlyList<HextechRarityTier> enabled = HextechRunePoolBuilder.GetEnabledPlayerRuneRaritiesForDisabledIds(disabledIds);

		SequenceEqual(Enum.GetValues<HextechRarityTier>(), enabled, "all disabled fallback rarities");
	}

	private static void FlyingKickDisableSurvivesNormalizationAndStrictPoolFiltering()
	{
		string flyingKickId = ModelDb.GetId<FlyingKickRune>().Entry;
		HashSet<string> disabledIds = HextechRuneConfiguration.NormalizeDisabledPlayerRuneIds([ flyingKickId ]);
		Expect(disabledIds.Contains(flyingKickId), "Flying Kick disable should survive config import normalization");

		RelicModel flyingKick = CreateMutableTestModel<FlyingKickRune>();
		RelicModel doubleVision = CreateMutableTestModel<DoubleVisionRune>();
		List<RelicModel> filtered = HextechRunePoolBuilder.FilterDisabledPlayerRunes(
			[ flyingKick, doubleVision ],
			disabledIds);
		SequenceEqual(
			new[] { ModelDb.GetId<DoubleVisionRune>() },
			filtered.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id),
			"disabled Flying Kick should never re-enter a partially filtered pool");
		Expect(
			HextechRunePoolBuilder.FilterDisabledPlayerRunes([ flyingKick ], disabledIds).Count == 0,
			"an exhausted pool must remain empty instead of restoring disabled Flying Kick");
	}

	private static void RarityRollResolverFiltersWeightedRarities()
	{
		HextechRarityWeights weights = HextechRarityRollResolver.ApplyEnabledRarities(
			silverWeight: 20,
			goldWeight: 50,
			prismaticWeight: 30,
			enabledRarities: [ HextechRarityTier.Gold, HextechRarityTier.Prismatic ]);

		Equal(0, weights.Silver, "silver weight");
		Equal(50, weights.Gold, "gold weight");
		Equal(30, weights.Prismatic, "prismatic weight");
		Equal(80, weights.Total, "total weight");
		Equal(HextechRarityTier.Gold, HextechRarityRollResolver.ResolveWeighted(weights, 0), "first gold roll");
		Equal(HextechRarityTier.Gold, HextechRarityRollResolver.ResolveWeighted(weights, 49), "last gold roll");
		Equal(HextechRarityTier.Prismatic, HextechRarityRollResolver.ResolveWeighted(weights, 50), "first prismatic roll");
		Equal(HextechRarityTier.Prismatic, HextechRarityRollResolver.ResolveWeighted(weights, 79), "last prismatic roll");
	}

	private static void RarityRollResolverUsesOrderedUniformFallback()
	{
		HextechRarityTier[] order = HextechRarityRollResolver.GetUniformRarityOrder(
			[ HextechRarityTier.Prismatic, HextechRarityTier.Silver ]);

		SequenceEqual(new[] { HextechRarityTier.Silver, HextechRarityTier.Prismatic }, order, "uniform rarity order");
		Equal(HextechRarityTier.Silver, HextechRarityRollResolver.ResolveUniform(order, 0), "first uniform rarity");
		Equal(HextechRarityTier.Prismatic, HextechRarityRollResolver.ResolveUniform(order, 1), "second uniform rarity");
		SequenceEqual(Enum.GetValues<HextechRarityTier>(), HextechRarityRollResolver.GetUniformRarityOrder([]), "empty enabled fallback order");
		Expect(HextechRarityRollResolver.HasAllRarities(Enum.GetValues<HextechRarityTier>()), "all-rarity detection");
		Expect(!HextechRarityRollResolver.HasAllRarities(order), "partial-rarity detection");
	}

	private static void ConsecutiveSilverRuleExcludesSilverFromEveryLaterAct()
	{
		HextechRarityWeights configured = new(2, 5, 3);
		Equal(
			configured,
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(configured, true, 0, null),
			"first act weights");
		Equal(
			configured,
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(configured, true, 1, HextechRarityTier.Gold),
			"weights after non-Silver act");
		Equal(
			configured,
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(configured, false, 1, HextechRarityTier.Silver),
			"disabled consecutive-Silver rule");
		Equal(
			new HextechRarityWeights(0, 5, 3),
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(configured, true, 1, HextechRarityTier.Silver),
			"second act weights after Silver");
		Equal(
			new HextechRarityWeights(0, 5, 3),
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(configured, true, 2, HextechRarityTier.Silver),
			"third act weights after Silver");
		Equal(
			new HextechRarityWeights(0, 1, 1),
			HextechRuneSelectionCoordinator.GetEffectiveActRarityWeights(new HextechRarityWeights(9, 0, 0), true, 2, HextechRarityTier.Silver),
			"non-Silver zero-weight fallback");

		SequenceEqual(
			new[] { HextechRarityTier.Gold },
			HextechRuneSelectionCoordinator.GetEffectiveActRarityCandidates(
				[ HextechRarityTier.Silver, HextechRarityTier.Gold ],
				true,
				1,
				HextechRarityTier.Silver),
			"enabled non-Silver candidates");
		SequenceEqual(
			new[] { HextechRarityTier.Gold, HextechRarityTier.Prismatic },
			HextechRuneSelectionCoordinator.GetEffectiveActRarityCandidates(
				[ HextechRarityTier.Silver ],
				true,
				1,
				HextechRarityTier.Silver),
			"strict non-Silver fallback candidates");
	}

	private static void GoldenRerollOnlyUpgradesSilverAndGold()
	{
		Expect(
			HextechGoldenRerollRules.TryGetUpgradedRarity(
				HextechRarityTier.Silver,
				out HextechRarityTier upgradedSilver),
			"silver should be eligible for a golden reroll");
		Equal(HextechRarityTier.Gold, upgradedSilver, "silver golden reroll target");

		Expect(
			HextechGoldenRerollRules.TryGetUpgradedRarity(
				HextechRarityTier.Gold,
				out HextechRarityTier upgradedGold),
			"gold should be eligible for a golden reroll");
		Equal(HextechRarityTier.Prismatic, upgradedGold, "gold golden reroll target");

		Expect(
			!HextechGoldenRerollRules.TryGetUpgradedRarity(
				HextechRarityTier.Prismatic,
				out HextechRarityTier unchangedPrismatic),
			"prismatic should not be eligible for a golden reroll");
		Equal(HextechRarityTier.Prismatic, unchangedPrismatic, "prismatic fallback target");
	}

	private static void GoldenRerollUsesExactFivePercentWindow()
	{
		for (int roll = 0; roll < 100; roll++)
		{
			Equal(
				roll < 5,
				HextechGoldenRerollRules.ShouldActivateForRoll(
					HextechRarityTier.Silver,
					hasUpgradedCandidates: true,
					roll,
					activationPercent: 5),
				$"silver golden reroll roll {roll}");
		}

		Expect(
			!HextechGoldenRerollRules.ShouldActivateForRoll(
				HextechRarityTier.Silver,
				hasUpgradedCandidates: true,
				percentRoll: 0,
				activationPercent: 0),
			"zero percent should never activate");
		Expect(
			HextechGoldenRerollRules.ShouldActivateForRoll(
				HextechRarityTier.Gold,
				hasUpgradedCandidates: true,
				percentRoll: 99,
				activationPercent: 100),
			"one hundred percent should always activate for eligible rolls");

		Expect(
			!HextechGoldenRerollRules.ShouldActivateForRoll(
				HextechRarityTier.Prismatic,
				hasUpgradedCandidates: true,
				percentRoll: 0,
				activationPercent: 100),
			"prismatic should not activate even on a winning roll");
		Expect(
			!HextechGoldenRerollRules.ShouldActivateForRoll(
				HextechRarityTier.Gold,
				hasUpgradedCandidates: false,
				percentRoll: 0,
				activationPercent: 100),
			"gold should not activate when the upgraded pool is unavailable");
	}

	private static void GoldenRerollSeparatesPlayersAndKeepsConsoleLocal()
	{
		string[] firstPlayerSalt = HextechGoldenRerollRules.BuildSaltParts(
			actIndex: 1,
			choiceOrdinal: 0,
			playerKey: "net:100");
		string[] secondPlayerSalt = HextechGoldenRerollRules.BuildSaltParts(
			actIndex: 1,
			choiceOrdinal: 0,
			playerKey: "net:200");

		Expect(
			!firstPlayerSalt.SequenceEqual(secondPlayerSalt),
			"different multiplayer players must receive independent golden reroll rolls");
		Equal("net:100", firstPlayerSalt[^1], "first player golden reroll salt");
		Equal("net:200", secondPlayerSalt[^1], "second player golden reroll salt");
		Expect(
			!new GoldenRerollConsoleCmd().IsNetworked,
			"golden reroll test command must only affect the issuing client");
	}

	private static void GoldenRerollDebugForceIsOneShot()
	{
		HextechGoldenRerollDebug.ResetForTests();
		HextechGoldenRerollDebug.ForceCurrentOrNext(out bool activatedCurrent);
		Expect(!activatedCurrent, "force without an open selection should target the next eligible selection");
		Expect(HextechGoldenRerollDebug.IsNextEligibleForced, "next eligible selection should be forced");
		Expect(HextechGoldenRerollDebug.ConsumeNextEligibleForce(), "first eligible selection should consume the force");
		Expect(!HextechGoldenRerollDebug.ConsumeNextEligibleForce(), "force should not leak to another player or selection");
		Expect(!HextechGoldenRerollDebug.IsNextEligibleForced, "consumed force should clear");
	}

	private static void GoldenRerollVisualKeepsAnimatingWhileOverlayIsPaused()
	{
		Expect(
			HextechGoldenRerollVisual.ShaderCode.Contains("uniform float animation_time", StringComparison.Ordinal),
			"golden reroll shader should receive an explicit animation clock");
		Expect(
			HextechGoldenRerollVisual.ShaderCode.Contains("sweep_position", StringComparison.Ordinal),
			"golden reroll shader should include a visible moving sweep");
		Expect(
			HextechGoldenRerollVisual.ShaderCode.Contains("sparkles", StringComparison.Ordinal),
			"golden reroll shader should include animated noise sparkles");
		MethodInfo? processOverride = typeof(HextechGoldenRerollVisual).GetMethod(
			"_Process",
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		Expect(
			processOverride == null,
			"golden reroll animation should not depend on an unreliable dynamic Control _Process callback");
		Expect(
			typeof(HextechGoldenRerollVisual).GetMethod(
				"StartAnimationLoop",
				BindingFlags.Public | BindingFlags.Instance) != null,
			"golden reroll animation should expose the ProcessFrame loop started after overlay open");
	}

	private static void GoldenRerollCardThemeFollowsRerolledRuneRarity()
	{
		foreach (HextechRarityTier rarity in Enum.GetValues<HextechRarityTier>())
		{
			Type runeType = HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(rarity).First();
			RelicModel rune = (RelicModel)Activator.CreateInstance(runeType)!;
			string expected = rarity switch
			{
				HextechRarityTier.Silver => "SILVER",
				HextechRarityTier.Prismatic => "PRISMATIC",
				_ => "GOLD"
			};
			Equal(
				expected,
				HextechRuneSelectionScreen.DetermineCardRarityKey(
					rune,
					HextechSelectionMetadataMode.PlayerRune),
				$"{rarity} rerolled card theme");
		}
	}

	private static void WeightedIndexBoundarySelection()
	{
		int[] weights = [ 100, 150, 100 ];

		Equal(0, HextechRunePoolBuilder.SelectWeightedIndex(weights, 0), "first slot start");
		Equal(0, HextechRunePoolBuilder.SelectWeightedIndex(weights, 99), "first slot end");
		Equal(1, HextechRunePoolBuilder.SelectWeightedIndex(weights, 100), "second slot start");
		Equal(1, HextechRunePoolBuilder.SelectWeightedIndex(weights, 249), "second slot end");
		Equal(2, HextechRunePoolBuilder.SelectWeightedIndex(weights, 250), "third slot start");
		Equal(2, HextechRunePoolBuilder.SelectWeightedIndex(weights, 999), "overflow clamps to last slot");
	}

	private static void RuneSelectionCandidateConstraintsReserveCharacterAndLimitUpgrades()
	{
		RelicModel ironcladRune = new BerserkRune();
		RelicModel ironcladUpgrade = new BloodlettingUpgradeRune();
		RelicModel silentRune = new SnakebiteRune();
		RelicModel genericRune = new JudicatorRune();
		RelicModel genericUpgrade = new AutomationUpgradeRune();
		RelicModel[] all = [ genericUpgrade, silentRune, ironcladUpgrade, genericRune, ironcladRune ];

		List<RelicModel> reserved = HextechRunePoolBuilder.ConstrainCandidatesForSlot(
			all,
			PlayerRuneCharacterPool.Ironclad,
			HextechRunePoolBuilder.CharacterReservedSlotIndex,
			upgradeAlreadySelected: false);
		SetEqual(
			new[] { ironcladRune, ironcladUpgrade },
			reserved,
			"reserved slot should contain only current-character candidates while that pool is available");

		List<RelicModel> reservedWithUpgradeTaken = HextechRunePoolBuilder.ConstrainCandidatesForSlot(
			all,
			PlayerRuneCharacterPool.Ironclad,
			HextechRunePoolBuilder.CharacterReservedSlotIndex,
			upgradeAlreadySelected: true);
		SequenceEqual(
			new[] { ironcladRune },
			reservedWithUpgradeTaken,
			"reserved slot should preserve the character guarantee without creating a second UpgradeRune");

		List<RelicModel> genericFallback = HextechRunePoolBuilder.ConstrainCandidatesForSlot(
			[ silentRune, genericUpgrade, genericRune ],
			PlayerRuneCharacterPool.Ironclad,
			HextechRunePoolBuilder.CharacterReservedSlotIndex,
			upgradeAlreadySelected: false);
		SetEqual(
			new[] { genericRune, genericUpgrade },
			genericFallback,
			"reserved slot should use generic candidates only after the current-character pool is exhausted");

		List<RelicModel> openSlotWithUpgradeTaken = HextechRunePoolBuilder.ConstrainCandidatesForSlot(
			all,
			PlayerRuneCharacterPool.Ironclad,
			slotIndex: 1,
			upgradeAlreadySelected: true);
		Expect(
			openSlotWithUpgradeTaken.All(static relic => !HextechRunePoolBuilder.IsUpgradeRune(relic)),
			"open slots must not expose a second UpgradeRune");
	}

	private static void UnconfirmedRuneSelectionCancelsInsteadOfDefaultingToFirstOption()
	{
		RelicModel confirmed = new JudicatorRune();
		Equal(
			confirmed,
			HextechRuneSelectionCoordinator.RequireCompletedSelection(confirmed, "test"),
			"confirmed selection");

		try
		{
			HextechRuneSelectionCoordinator.RequireCompletedSelection<RelicModel>(null, "test");
			throw new InvalidOperationException("missing selection should cancel");
		}
		catch (OperationCanceledException ex)
		{
			Expect(ex.Message.Contains("test", StringComparison.Ordinal), "cancellation should retain diagnostic context");
		}
	}

	private static void SelectionUiWaitsForControllerInputBeforeFocusing()
	{
		MethodInfo defaultFocusGetter = typeof(HextechRuneSelectionScreen)
			.GetProperty(nameof(HextechRuneSelectionScreen.DefaultFocusedControl))!
			.GetMethod!;
		Expect(
			PatchProcessor.GetOriginalInstructions(defaultFocusGetter)
				.Select(static instruction => instruction.operand)
				.OfType<FieldInfo>()
				.Any(static field => field.Name == "_controllerNavigationActivated"),
			"selection overlay should not expose an initial focus target before controller navigation activates");

		MethodInfo selectionInput = typeof(HextechRuneSelectionScreen).GetMethod(
			nameof(HextechRuneSelectionScreen._UnhandledInput),
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(HextechRuneSelectionScreen), nameof(HextechRuneSelectionScreen._UnhandledInput));
		Expect(
			PatchProcessor.GetOriginalInstructions(selectionInput)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.DeclaringType == typeof(HextechControllerInput) && method.Name == nameof(HextechControllerInput.IsIntentional)),
			"selection overlay should activate focus from real joypad input");

		MethodInfo openConfig = typeof(HextechRuneConfigMenuHooks).GetMethod(
			"OpenOverlay",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRuneConfigMenuHooks), "OpenOverlay");
		MethodInfo[] configCalls = PatchProcessor.GetOriginalInstructions(openConfig)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			configCalls.Any(static method => method.DeclaringType == typeof(HextechControllerOverlay) && method.Name == "set_InitialFocus"),
			"config overlay should register a deferred controller focus target");
		Expect(
			configCalls.All(static method => method.Name != nameof(Control.GrabFocus)),
			"opening config with mouse should not explicitly focus an option");
	}

	private static void EnemyHexRerollPlaysRerollSound()
	{
		MethodInfo reroll = typeof(HextechRuneSelectionScreen).GetMethod(
			"OnEnemyHexRerollPressed",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRuneSelectionScreen), "OnEnemyHexRerollPressed");
		Expect(
			PatchProcessor.GetOriginalInstructions(reroll)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "PlayRerollSfx"),
			"successful enemy hex rerolls should use the same reroll sound as player rerolls");
	}

	private static void EnemyHexRemovalCanBeUndoneWithoutConsumingTheSlot()
	{
		List<MonsterHexKind?> current = [ MonsterHexKind.EightPennyGate ];
		List<MonsterHexKind?> beforeRemoval = [ null ];
		Expect(
			HextechRuneSelectionScreen.ToggleEnemyHexRemoval(current, beforeRemoval, 0),
			"an active enemy hex should be removable");
		Equal<MonsterHexKind?>(null, current[0], "removed enemy hex slot");
		Equal<MonsterHexKind?>(MonsterHexKind.EightPennyGate, beforeRemoval[0], "removed enemy hex undo snapshot");

		Expect(
			HextechRuneSelectionScreen.ToggleEnemyHexRemoval(current, beforeRemoval, 0),
			"a locally removed enemy hex should be restorable");
		Equal<MonsterHexKind?>(MonsterHexKind.EightPennyGate, current[0], "restored enemy hex slot");
		Equal<MonsterHexKind?>(null, beforeRemoval[0], "consumed enemy hex undo snapshot");
		Expect(
			!HextechRuneSelectionScreen.ToggleEnemyHexRemoval([ null ], [ null ], 0),
			"a remotely removed slot without an undo snapshot should stay disabled");
	}

	private static void EnemyHexActionButtonsUseTexturesWithoutTooltipText()
	{
		Expect(
			!HextechRuneSelectionScreen.ShouldShowEnemyHexUndoButton(MonsterHexKind.EightPennyGate),
			"active enemy hexes should show reroll and remove actions");
		Expect(
			HextechRuneSelectionScreen.ShouldShowEnemyHexUndoButton(null),
			"removed enemy hexes should replace both actions with undo");
		SetEqual(
			new[]
			{
				"res://HextechRunes/images/ui/hextechRemoveButton.png",
				"res://HextechRunes/images/ui/hextechRemoveButtonHover.png",
				"res://HextechRunes/images/ui/hextechRemoveButtonPressed.png",
				"res://HextechRunes/images/ui/hextechRemoveButtonDisabled.png",
				"res://HextechRunes/images/ui/hextechUndoButton.png",
				"res://HextechRunes/images/ui/hextechUndoButtonHover.png",
				"res://HextechRunes/images/ui/hextechUndoButtonPressed.png",
				"res://HextechRunes/images/ui/hextechUndoButtonDisabled.png"
			},
			new[]
			{
				"RemoveButtonTexturePath",
				"RemoveButtonHoverTexturePath",
				"RemoveButtonPressedTexturePath",
				"RemoveButtonDisabledTexturePath",
				"UndoButtonTexturePath",
				"UndoButtonHoverTexturePath",
				"UndoButtonPressedTexturePath",
				"UndoButtonDisabledTexturePath"
			}.Select(name => (string)typeof(HextechRuneSelectionScreen)
				.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!
				.GetRawConstantValue()!),
			"enemy remove and undo button state textures");
		Equal(
			"res://HextechRunes/images/ui/hextechUndoButtonDisabled.png",
			HextechRuneSelectionScreen.ResolveEnemyHexRemovalButtonTexture(undo: true, disabled: true, pressed: false, highlighted: false),
			"disabled undo texture");
		Equal(
			"res://HextechRunes/images/ui/hextechUndoButtonPressed.png",
			HextechRuneSelectionScreen.ResolveEnemyHexRemovalButtonTexture(undo: true, disabled: false, pressed: true, highlighted: true),
			"pressed undo texture");
		Equal(
			"res://HextechRunes/images/ui/hextechRemoveButtonHover.png",
			HextechRuneSelectionScreen.ResolveEnemyHexRemovalButtonTexture(undo: false, disabled: false, pressed: false, highlighted: true),
			"hovered remove texture");

		MethodInfo previewRow = typeof(HextechRuneSelectionScreen).GetMethod(
			"CreateEnemyPreviewRow",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRuneSelectionScreen), "CreateEnemyPreviewRow");
		Expect(
			PatchProcessor.GetOriginalInstructions(previewRow)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.All(static method => method.Name != "set_TooltipText"),
			"enemy reroll and remove buttons should not show hover text");

		MethodInfo remove = typeof(HextechRuneSelectionScreen).GetMethod(
			"OnEnemyHexRemovePressed",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRuneSelectionScreen), "OnEnemyHexRemovePressed");
		Expect(
			PatchProcessor.GetOriginalInstructions(remove)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "PlayButtonClickSfx"),
			"enemy remove and undo actions should play the standard UI click sound");
	}

	private static void CollapsedEnemyHexPanelFollowsTopBarButtonLifecycle()
	{
		MethodInfo ensureButton = typeof(HextechEnemyHexCollapseView).GetMethod(
			"EnsureButton",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechEnemyHexCollapseView), "EnsureButton");
		Expect(
			PatchProcessor.GetOriginalInstructions(ensureButton)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "add_TreeExiting"),
			"collapsed enemy hex button should own a tree-exit cleanup hook");

		MethodInfo cleanup = typeof(HextechEnemyHexCollapseView).GetMethod(
			"OnButtonTreeExiting",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechEnemyHexCollapseView), "OnButtonTreeExiting");
		Expect(
			PatchProcessor.GetOriginalInstructions(cleanup)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "QueueFreeIfValid"),
			"top bar exit should release the globally hosted collapsed enemy hex panel");
	}

	private static void DestructivePickupRunesAreExcludedFromRandomRewards()
	{
		Type[] destructiveTypes =
		[
			typeof(TransmuteChaosRune),
			typeof(TransmutePrismaticRune),
			typeof(TransmuteGoldRune),
			typeof(PandorasBoxRune)
		];
		foreach (Type runeType in destructiveTypes)
		{
			Expect(
				HextechRuneGrantHelper.IsDestructiveRandomRewardRuneType(runeType),
				$"{runeType.Name} must not be generated as a random reward");
		}

		Expect(
			!HextechRuneGrantHelper.IsDestructiveRandomRewardRuneType(typeof(JudicatorRune)),
			"ordinary runes should remain eligible for random rewards");
	}

	private static void ActSelectionGatePreventsReentryAndClearsCurrentRun()
	{
		HextechActSelectionGate gate = new();
		object run = new();
		object otherRun = new();

		Expect(!gate.IsHandling, "new gate should be idle");
		gate.Enter(run);
		Expect(gate.IsHandling, "entered gate should be handling");
		Expect(!gate.ResetIfStaleRun(run), "same run should not be stale");
		Expect(!gate.ExitIfCurrent(otherRun), "different run should not exit current handling");
		Expect(gate.IsHandling, "gate should keep handling after different-run exit");
		Expect(gate.ExitIfCurrent(run), "current run should exit");
		Expect(!gate.IsHandling, "gate should be idle after current-run exit");
	}

	private static void ActSelectionGateClearsStaleRun()
	{
		HextechActSelectionGate gate = new();
		object oldRun = new();
		object newRun = new();

		gate.Enter(oldRun);
		Expect(gate.ResetIfStaleRun(newRun), "different run should clear stale handling state");
		Expect(!gate.IsHandling, "gate should be idle after stale reset");
		gate.Enter(newRun);
		Expect(gate.IsHandling, "gate should accept a new run after stale reset");
	}

	private static void RuneSelectionJournalRoundTripsInStableOrder()
	{
		HextechRuneSelectionJournalState state = new();
		ModelId later = new("HEXTECH_TEST", "LATER");
		ModelId earlier = new("HEXTECH_TEST", "EARLIER");
		state.RecordSelected(2, 1, 99, later);
		state.RecordSelected(0, 0, 7, earlier);
		state.MarkApplied(2, 1, 99, later);
		Expect(state.HasEntriesForAct(0), "journal should report a pending operation for act zero");
		Expect(state.HasEntriesForAct(2), "journal should retain completed operations until the run resets");
		Expect(!state.HasEntriesForAct(1), "journal should not report an unrelated act");

		string json = state.Serialize();
		Expect(
			json.IndexOf("EARLIER", StringComparison.Ordinal)
				< json.IndexOf("LATER", StringComparison.Ordinal),
			"journal JSON should sort operations by act, ordinal and player id");

		HextechRuneSelectionJournalState restored = new();
		restored.Restore(json);
		Expect(
			restored.TryGet(0, 0, 7, out HextechRuneSelectionJournalEntry earlierEntry),
			"earlier journal entry should restore");
		Equal(earlier, earlierEntry.SelectedId, "restored earlier selected ModelId");
		Equal(false, earlierEntry.Applied, "restored earlier applied state");
		Expect(
			restored.TryGet(2, 1, 99, out HextechRuneSelectionJournalEntry laterEntry),
			"later journal entry should restore");
		Equal(later, laterEntry.SelectedId, "restored later selected ModelId");
		Equal(true, laterEntry.Applied, "restored later applied state");
	}

	private static void RuneSelectionJournalRejectsConflictingSelections()
	{
		HextechRuneSelectionJournalState state = new();
		ModelId selected = new("HEXTECH_TEST", "SELECTED");
		ModelId conflicting = new("HEXTECH_TEST", "CONFLICTING");

		Expect(state.RecordSelected(1, 2, 33, selected), "first journal selection should be recorded");
		Expect(!state.RecordSelected(1, 2, 33, selected), "same journal selection should be idempotent");
		ExpectThrows<InvalidOperationException>(
			() => state.RecordSelected(1, 2, 33, conflicting),
			"same operation must reject a different selected ModelId");
		Expect(state.MarkApplied(1, 2, 33, selected), "first applied transition should be recorded");
		Expect(!state.MarkApplied(1, 2, 33, selected), "applied transition should be idempotent");
		ExpectThrows<InvalidOperationException>(
			() => state.MarkApplied(1, 2, 33, conflicting),
			"applied transition must reject a different ModelId");
	}

	private static void AppliedRuneSelectionJournalDoesNotRequireInventoryPresence()
	{
		Expect(
			!HextechRuneSelectionJournalState.RequiresRelicObtain(
				applied: true,
				currentlyOwned: false),
			"an applied journal entry must not replay after a self-consuming rune leaves the inventory");
		Expect(
			!HextechRuneSelectionJournalState.RequiresRelicObtain(
				applied: false,
				currentlyOwned: true),
			"an inventory-boundary recovery should mark the pending entry instead of obtaining it twice");
		Expect(
			HextechRuneSelectionJournalState.RequiresRelicObtain(
				applied: false,
				currentlyOwned: false),
			"only a pending and absent journal entry should resume relic obtain");
	}

	private static void MonsterHexRollerBuildActPoolExcludesKnownAndFallsBack()
	{
		(HextechRarityTier rarity, IReadOnlyList<MonsterHexKind> rarityPool) = GetMonsterHexPoolWithMinimum(2);

		IReadOnlyList<MonsterHexKind> filteredPool = HextechMonsterHexRoller.BuildActPool(
			rarity,
			rarityPool.Take(rarityPool.Count - 1));
		SequenceEqual(new[] { rarityPool[^1] }, filteredPool, "act monster hex pool should exclude known hexes");

		IReadOnlyList<MonsterHexKind> fallbackPool = HextechMonsterHexRoller.BuildActPool(rarity, rarityPool);
		SequenceEqual(rarityPool, fallbackPool, "act monster hex pool should fall back to full rarity pool when exhausted");
	}

	private static void MonsterHexRollerResolveNewHexesPreservesPrimaryAndAvoidsDuplicates()
	{
		MonsterHexKind[] kinds = Enum.GetValues<MonsterHexKind>()
			.Take(4)
			.ToArray();
		Expect(kinds.Length >= 4, "monster hex enum should have at least four values for resolution test");

		IReadOnlyList<MonsterHexKind> resolved = HextechMonsterHexRoller.ResolveNewMonsterHexes(
			newEnemyHexCount: 3,
			previousHexes: [ kinds[0] ],
			primaryMonsterHex: kinds[1],
			chooseExtraHex: (excludedHexes, _) =>
			{
				foreach (MonsterHexKind kind in kinds)
				{
					if (!excludedHexes.Contains(kind))
					{
						return kind;
					}
				}

				return null;
			});

		SequenceEqual(new[] { kinds[1], kinds[2], kinds[3] }, resolved, "resolved new monster hexes");
		Expect(HextechMonsterHexRoller.ResolveNewMonsterHexes(0, [ kinds[0] ], kinds[1], (_, _) => kinds[2]).Count == 0, "zero enemy hex count should resolve none");
	}

	private static void MonsterHexRollerBuildRerollPoolHonorsIconExclusionsThenFallbacks()
	{
		(HextechRarityTier rarity, IReadOnlyList<MonsterHexKind> rarityPool) = GetMonsterHexPoolWithMinimum(4);
		MonsterHexKind currentHex = rarityPool[0];
		MonsterHexKind knownHex = rarityPool[1];
		MonsterHexKind iconBlockedHex = rarityPool[2];
		MonsterHexKind allowedHex = rarityPool[3];

		IReadOnlyList<MonsterHexKind> rerollPool = HextechMonsterHexRoller.BuildRerollPool(
			rarity,
			[ knownHex ],
			currentHex,
			new HashSet<ModelId> { TestMonsterHexIconId(iconBlockedHex) },
			TestMonsterHexIconId);
		Expect(!rerollPool.Contains(currentHex), "reroll pool should exclude current hex");
		Expect(!rerollPool.Contains(knownHex), "reroll pool should exclude known hexes");
		Expect(!rerollPool.Contains(iconBlockedHex), "reroll pool should exclude icon-blocked hexes while alternatives remain");
		Expect(rerollPool.Contains(allowedHex), "reroll pool should keep unblocked alternatives");

		IReadOnlyList<MonsterHexKind> fallbackPool = HextechMonsterHexRoller.BuildRerollPool(
			rarity,
			rarityPool.Skip(1),
			currentHex,
			new HashSet<ModelId>(),
			TestMonsterHexIconId);
		SequenceEqual(rarityPool.Where(hex => hex != currentHex), fallbackPool, "reroll pool should fall back to non-current rarity pool when known exclusions exhaust it");
	}
}
