using System.Reflection;
using System.Runtime.CompilerServices;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private const int Magic = 0x48585452; // HXTR
	private const int ChoiceKindActRoll = 1;
	private const int ChoiceKindRuneSelection = 2;
	private const int ChoiceKindActSelectionApplied = 3;
	private const int ChoiceKindEnemyHexAdjustment = 4;
	private const int ChoiceKindRandomRuneGrant = 6;
	private const int EnemyHexAdjustmentListVersion = -2;
	private const int StableModelIdListVersion = -3;

	public static int Main()
	{
#if STS2_109_OR_NEWER
		// 0.109 起游戏引用 System.IO.Hashing(XxHash32);它不在测试的 deps.json 里(仅作文件复制),
		// 默认加载上下文按 deps.json 解析会失败,这里从输出目录兜底加载。
		System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += static (context, name) =>
		{
			string candidate = Path.Combine(AppContext.BaseDirectory, $"{name.Name}.dll");
			return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
		};

		// 0.109 起缓存并入 Multiplayer.Serialization.ModelIdSerializationCache,守卫语义同 0.108。
		typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache)
			.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static)
			?.SetValue(null, true);
#elif STS2_108_OR_NEWER
		// 0.108 起 SavedPropertiesTypeCache 未初始化即用会抛;真实游戏由启动流程 Init(),但 Init 又依赖
		// AssemblyInfo 等更多游戏启动态。测试环境直接置 _initialized 标志,恢复 0.107.1 的无守卫语义。
		typeof(MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache)
			.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static)
			?.SetValue(null, true);
#endif
		TestCase[] tests =
		[
			new(nameof(ActRollRoundTripKeepsHostSnapshot), ActRollRoundTripKeepsHostSnapshot),
			new(nameof(RuneSelectionRoundTripRequiresMatchingActAndOrdinal), RuneSelectionRoundTripRequiresMatchingActAndOrdinal),
			new(nameof(RuneSelectionRejectsWrongActOrOrdinal), RuneSelectionRejectsWrongActOrOrdinal),
			new(nameof(ActSelectionAppliedRejectsWrongActOrOrdinal), ActSelectionAppliedRejectsWrongActOrOrdinal),
			new(nameof(EnemyHexAdjustmentRoundTripKeepsAllSlots), EnemyHexAdjustmentRoundTripKeepsAllSlots),
			new(nameof(EnemyHexAdjustmentRejectsInvalidHex), EnemyHexAdjustmentRejectsInvalidHex),
			new(nameof(LegacyEnemyHexAdjustmentStillDecodes), LegacyEnemyHexAdjustmentStillDecodes),
			new(nameof(RandomRuneGrantRoundTripKeepsStableModelIds), RandomRuneGrantRoundTripKeepsStableModelIds),
			new(nameof(RandomRuneGrantRejectsMalformedStableModelIdList), RandomRuneGrantRejectsMalformedStableModelIdList),
			new(nameof(RelicOptionSelectionRoundTripRequiresMatchingOptions), RelicOptionSelectionRoundTripRequiresMatchingOptions),
			new(nameof(StableModelIdListCodecRoundTripsFromNonzeroCursor), StableModelIdListCodecRoundTripsFromNonzeroCursor),
			new(nameof(StableModelIdListCodecRejectsMalformedLength), StableModelIdListCodecRejectsMalformedLength),
			new(nameof(PlayerRuneRarityConfigExcludesFullyDisabledTier), PlayerRuneRarityConfigExcludesFullyDisabledTier),
			new(nameof(PlayerRuneRarityConfigFallsBackWhenAllTiersDisabled), PlayerRuneRarityConfigFallsBackWhenAllTiersDisabled),
			new(nameof(RarityRollResolverFiltersWeightedRarities), RarityRollResolverFiltersWeightedRarities),
			new(nameof(RarityRollResolverUsesOrderedUniformFallback), RarityRollResolverUsesOrderedUniformFallback),
			new(nameof(WeightedIndexBoundarySelection), WeightedIndexBoundarySelection),
			new(nameof(RuneSelectionCandidateConstraintsReserveCharacterAndLimitUpgrades), RuneSelectionCandidateConstraintsReserveCharacterAndLimitUpgrades),
			new(nameof(UnconfirmedRuneSelectionCancelsInsteadOfDefaultingToFirstOption), UnconfirmedRuneSelectionCancelsInsteadOfDefaultingToFirstOption),
			new(nameof(DestructivePickupRunesAreExcludedFromRandomRewards), DestructivePickupRunesAreExcludedFromRandomRewards),
			new(nameof(SearingAttackRuneGrantsUpgradedCard), SearingAttackRuneGrantsUpgradedCard),
			new(nameof(FortuneForgeRewardScalesByStacks), FortuneForgeRewardScalesByStacks),
			new(nameof(NightmareHooksEveryDarkOrbPassiveTrigger), NightmareHooksEveryDarkOrbPassiveTrigger),
			new(nameof(NightmareEffectRunsOnceAfterEachPassiveTask), NightmareEffectRunsOnceAfterEachPassiveTask),
			new(nameof(DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune), DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune),
			new(nameof(DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights), DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights),
			new(nameof(StableRandomPlayerIdentityUsesNetIdBeforeLocalSlot), StableRandomPlayerIdentityUsesNetIdBeforeLocalSlot),
			new(nameof(StableRandomSequentialFloorsAvoidExcessClustering), StableRandomSequentialFloorsAvoidExcessClustering),
			new(nameof(StableRandomPowerOfTwoIndexesAvoidTerminalCounterCycle), StableRandomPowerOfTwoIndexesAvoidTerminalCounterCycle),
			new(nameof(RandomForgeShopRelicUpdatesDisplayedPrice), RandomForgeShopRelicUpdatesDisplayedPrice),
			new(nameof(ActSelectionGatePreventsReentryAndClearsCurrentRun), ActSelectionGatePreventsReentryAndClearsCurrentRun),
			new(nameof(ActSelectionGateClearsStaleRun), ActSelectionGateClearsStaleRun),
			new(nameof(RunConfigurationDefaultSnapshotUsesExpectedActCounts), RunConfigurationDefaultSnapshotUsesExpectedActCounts),
			new(nameof(RunConfigurationDefaultSnapshotDisablesRiskyContent), RunConfigurationDefaultSnapshotDisablesRiskyContent),
			new(nameof(RerollLimitConfigUsesZeroToNineThenInfinite), RerollLimitConfigUsesZeroToNineThenInfinite),
			new(nameof(EnemyHexCountStateNormalizesMissingAndOutOfRangeValues), EnemyHexCountStateNormalizesMissingAndOutOfRangeValues),
			new(nameof(EnemyHexCountStateUsesThirdActForEndlessAndBeyondThirdAct), EnemyHexCountStateUsesThirdActForEndlessAndBeyondThirdAct),
			new(nameof(PlayerRuneConfigSnapshotStateUsesClientFallbackWithoutSnapshot), PlayerRuneConfigSnapshotStateUsesClientFallbackWithoutSnapshot),
			new(nameof(PlayerRuneConfigSnapshotStateSnapshotOverridesLocalFallback), PlayerRuneConfigSnapshotStateSnapshotOverridesLocalFallback),
				new(nameof(PlayerRuneConfigSnapshotStateSerializesAndClearsMalformedData), PlayerRuneConfigSnapshotStateSerializesAndClearsMalformedData),
				new(nameof(NetworkChoiceTimeoutUsesNominalWallClockSeconds), NetworkChoiceTimeoutUsesNominalWallClockSeconds),
				new(nameof(CombatTrackingPerTurnProcLimitsResetOncePerRound), CombatTrackingPerTurnProcLimitsResetOncePerRound),
				new(nameof(MindOverMatterFirstDrawTrackingResetsPerPlayerTurn), MindOverMatterFirstDrawTrackingResetsPerPlayerTurn),
				new(nameof(CombatTrackingGlobalProcOrdinalsSerializeAndReset), CombatTrackingGlobalProcOrdinalsSerializeAndReset),
				new(nameof(CombatTrackingPlayerRuneProcOrdinalPeekDoesNotConsume), CombatTrackingPlayerRuneProcOrdinalPeekDoesNotConsume),
			new(nameof(CombatTrackingSerializationIsCultureInvariant), CombatTrackingSerializationIsCultureInvariant),
			new(nameof(SavedPropertyManifestMatchesCheckedInList), SavedPropertyManifestMatchesCheckedInList),
			new(nameof(ConfigMigrationForceResetsBelowV15), ConfigMigrationForceResetsBelowV15),
			new(nameof(ConfigMigrationV15BaselineReachesCurrentDefault), ConfigMigrationV15BaselineReachesCurrentDefault),
			new(nameof(ConfigMigrationV25AddsNewDefaultDisables), ConfigMigrationV25AddsNewDefaultDisables),
			new(nameof(ConfigMigrationCurrentVersionPreservesCustomDisabledIds), ConfigMigrationCurrentVersionPreservesCustomDisabledIds),
				new(nameof(MayhemRunContextResetForNewRunClearsState), MayhemRunContextResetForNewRunClearsState),
			new(nameof(MayhemRunContextResetForEndlessLoopCarriesActiveMonsterHex), MayhemRunContextResetForEndlessLoopCarriesActiveMonsterHex),
			new(nameof(MayhemRunContextDebugResetSetsOnlyRequestedMonsterHex), MayhemRunContextDebugResetSetsOnlyRequestedMonsterHex),
			new(nameof(PlayerRuneMetadataHasUniqueTypes), PlayerRuneMetadataHasUniqueTypes),
			new(nameof(PlayerRuneMetadataMatchesContentRegistrySlices), PlayerRuneMetadataMatchesContentRegistrySlices),
			new(nameof(PlayerRuneMetadataPreservesCharacterOrder), PlayerRuneMetadataPreservesCharacterOrder),
			new(nameof(PlayerRuneMetadataClassifiesConfigStates), PlayerRuneMetadataClassifiesConfigStates),
			new(nameof(WellLaidPlansUpgradeRuneIsRetiredButSaveCompatible), WellLaidPlansUpgradeRuneIsRetiredButSaveCompatible),
			new(nameof(PlayerRuneMetadataCatalogOutputsMatchCatalogQueries), PlayerRuneMetadataCatalogOutputsMatchCatalogQueries),
			new(nameof(PlayerRuneMetadataFallbacksAreStable), PlayerRuneMetadataFallbacksAreStable),
			new(nameof(ForgeMetadataHasUniqueTypes), ForgeMetadataHasUniqueTypes),
			new(nameof(ForgeMetadataMatchesContentRegistrySlices), ForgeMetadataMatchesContentRegistrySlices),
			new(nameof(ForgeMetadataFallbacksAreStable), ForgeMetadataFallbacksAreStable),
			new(nameof(MonsterHexMetadataHasUniqueKinds), MonsterHexMetadataHasUniqueKinds),
			new(nameof(MonsterHexMetadataMatchesContentRegistrySlices), MonsterHexMetadataMatchesContentRegistrySlices),
			new(nameof(MonsterHexMetadataKeepsDisabledKindsOutOfRarityPools), MonsterHexMetadataKeepsDisabledKindsOutOfRarityPools),
			new(nameof(MonsterInteractionPolicyPreservesStructuralMonsterBuffs), MonsterInteractionPolicyPreservesStructuralMonsterBuffs),
			new(nameof(PersonalHiveSafetyRejectsPlayerSideCopies), PersonalHiveSafetyRejectsPlayerSideCopies),
			new(nameof(EnemyCompensationPoisonUsesOneThirdRoundedDownWithMinimum), EnemyCompensationPoisonUsesOneThirdRoundedDownWithMinimum),
			new(nameof(EnemyCompensationSkipsPoisonDamageSignature), EnemyCompensationSkipsPoisonDamageSignature),
			new(nameof(EnemyCompensationSkipsOutbreakPoisonResponse), EnemyCompensationSkipsOutbreakPoisonResponse),
			new(nameof(EnemyCompensationSkipsSleightOfFleshResponse), EnemyCompensationSkipsSleightOfFleshResponse),
			new(nameof(ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless), ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless),
			new(nameof(IllusoryWeaponPenNibPrefixesCanReturnSkippedTask), IllusoryWeaponPenNibPrefixesCanReturnSkippedTask),
			new(nameof(AttackCommandCompatibilityRestoresNullExecuteResult), AttackCommandCompatibilityRestoresNullExecuteResult),
			new(nameof(MultiplayerGameplaySignatureExcludesRuntimeSavedProperties), MultiplayerGameplaySignatureExcludesRuntimeSavedProperties),
			new(nameof(SavedPropertyNetIdCanonicalizationIsInjectionOrderIndependent), SavedPropertyNetIdCanonicalizationIsInjectionOrderIndependent),
			new(nameof(SavedPropertyNetIdBitSizeMatchesGameFormula), SavedPropertyNetIdBitSizeMatchesGameFormula),
			new(nameof(CompensationReplacementGuardScopesAsyncWork), CompensationReplacementGuardScopesAsyncWork),
			new(nameof(CompensationReplacementSuppressesSleightOfFleshResponse), CompensationReplacementSuppressesSleightOfFleshResponse),
			new(nameof(EventRewardTransactionCommitsSequentially), EventRewardTransactionCommitsSequentially),
			new(nameof(EventRewardTransactionRejectsLateRecordsAndSecondCommit), EventRewardTransactionRejectsLateRecordsAndSecondCommit),
			new(nameof(DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect), DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect),
			new(nameof(DoubleVisionDustyTomeSaveLoadPreservesAncientCard), DoubleVisionDustyTomeSaveLoadPreservesAncientCard),
			new(nameof(DoubleVisionDustyTomeEventMultiplayerRunsOnEveryPeerWithoutBroadcast), DoubleVisionDustyTomeEventMultiplayerRunsOnEveryPeerWithoutBroadcast),
			new(nameof(PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries), PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries),
			new(nameof(MonsterHexRollerBuildActPoolExcludesKnownAndFallsBack), MonsterHexRollerBuildActPoolExcludesKnownAndFallsBack),
			new(nameof(MonsterHexRollerResolveNewHexesPreservesPrimaryAndAvoidsDuplicates), MonsterHexRollerResolveNewHexesPreservesPrimaryAndAvoidsDuplicates),
			new(nameof(MonsterHexRollerBuildRerollPoolHonorsIconExclusionsThenFallbacks), MonsterHexRollerBuildRerollPoolHonorsIconExclusionsThenFallbacks),
			new(nameof(ExternalConfigDisabledIdsPreserveUnloadedContent), ExternalConfigDisabledIdsPreserveUnloadedContent),
			new(nameof(ExternalPlayerRuneRegistrationUpdatesCatalog), ExternalPlayerRuneRegistrationUpdatesCatalog),
			new(nameof(ExternalEventRelicRegistrationUpdatesRegistry), ExternalEventRelicRegistrationUpdatesRegistry),
			new(nameof(ExternalForgeRegistrationUpdatesCatalog), ExternalForgeRegistrationUpdatesCatalog),
			new(nameof(ExternalEnchantmentIconRegistrationTracksPath), ExternalEnchantmentIconRegistrationTracksPath),
			new(nameof(RepeatableEnchantmentsRequireCurrentlyOwnedEnchantmentMasterRune), RepeatableEnchantmentsRequireCurrentlyOwnedEnchantmentMasterRune),
			new(nameof(EnchantmentCompositionAdapterFindsDirectEnchantments), EnchantmentCompositionAdapterFindsDirectEnchantments),
			new(nameof(EnchantmentCompositionAdapterFindsSponsorCompositeEnchantments), EnchantmentCompositionAdapterFindsSponsorCompositeEnchantments),
			new(nameof(AbyssalContractChoiceModelsMapToExpectedContracts), AbyssalContractChoiceModelsMapToExpectedContracts),
			new(nameof(AbyssalContractWarriorEliteThresholdGrows), AbyssalContractWarriorEliteThresholdGrows),
			new(nameof(AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters), AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters),
			new(nameof(AbyssalContractWarriorCardFilterRejectsSkillsAndPowers), AbyssalContractWarriorCardFilterRejectsSkillsAndPowers)
		];

		int failed = 0;
		foreach (TestCase test in tests)
		{
			try
			{
				test.Run();
				Console.WriteLine($"PASS {test.Name}");
			}
			catch (Exception ex)
			{
				failed++;
				Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
			}
		}

		Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
		return failed == 0 ? 0 : 1;
	}

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
			FirstActRuneRarityWeights = new HextechRarityWeights(1, 2, 3),
			NormalRuneRarityWeights = new HextechRarityWeights(4, 5, 6),
			SecondActAfterSilverRuneRarityWeights = new HextechRarityWeights(0, 7, 8),
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

		PlayerChoiceResult result = HextechChoiceCodec.CreateEnemyHexAdjustment(source);

		Expect(HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 0, out EnemyHexAdjustmentPayload decoded), "enemy adjustment should decode");
		Equal(0, decoded.ActIndex, "act");
		Equal(12, decoded.Sequence, "sequence");
		Equal(true, decoded.IsFinal, "final flag");
		SequenceEqual(source.MonsterHexes, decoded.MonsterHexes, "monster hex slots");
		SequenceEqual(new[] { 2, 0, 0 }, decoded.RerollCounts, "reroll counts");
		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 1, out _), "wrong act should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsInvalidHex()
	{
		PlayerChoiceResult result = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			1,
			EnemyHexAdjustmentListVersion,
			0,
			1,
			int.MaxValue,
			0
		});

		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 0, out _), "invalid monster hex enum should be rejected");
	}

	private static void LegacyEnemyHexAdjustmentStillDecodes()
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

		Expect(HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 1, out EnemyHexAdjustmentPayload decoded), "legacy enemy adjustment should decode");
		Equal(1, decoded.ActIndex, "act");
		Equal(9, decoded.Sequence, "sequence");
		SequenceEqual(new MonsterHexKind?[] { MonsterHexKind.FrostWraith }, decoded.MonsterHexes, "legacy monster hex");
		SequenceEqual(new[] { 2 }, decoded.RerollCounts, "legacy reroll count");
		Equal(true, decoded.IsFinal, "legacy final flag");
	}

	private static void RandomRuneGrantRoundTripKeepsStableModelIds()
	{
		ModelId[] source =
		[
			new("HEXTECH_TEST", "FIRST_RUNE"),
			new("HEXTECH_TEST", "SECOND_RUNE")
		];

		PlayerChoiceResult result = HextechChoiceCodec.CreateRandomRuneGrant(source);

		Expect(HextechChoiceCodec.TryDecodeRandomRuneGrant(result, out List<ModelId> decoded), "random grant should decode");
		SequenceEqual(source, decoded, "stable model ids");
		Expect(HextechChoiceCodec.IsRandomRuneGrant(result), "random grant predicate");
	}

	private static void RandomRuneGrantRejectsMalformedStableModelIdList()
	{
		PlayerChoiceResult tooManyIds = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			StableModelIdListVersion,
			65
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(tooManyIds, out _), "oversized stable id list should be rejected");

		PlayerChoiceResult badSerializedId = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			StableModelIdListVersion,
			1,
			3,
			'B',
			'A',
			'D'
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(badSerializedId, out _), "malformed model id should be rejected");
	}

	private static void RelicOptionSelectionRoundTripRequiresMatchingOptions()
	{
		RelicModel[] options = CreateRuneSelectionTestOptions(2);
		ModelId[] optionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToArray();
		PlayerChoiceResult result = HextechChoiceCodec.CreateRelicOptionSelection(1, options);

		Expect(HextechChoiceCodec.IsRelicOptionSelection(result, options), "matching relic option selection should be expected");
		Expect(HextechChoiceCodec.TryDecodeRelicOptionSelection(result, out int selectedIndex, out List<ModelId> decodedOptionIds), "relic option selection should decode");
		Equal(1, selectedIndex, "selected relic option index");
		SequenceEqual(optionIds, decodedOptionIds, "relic option ids");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, options.Reverse().ToArray()), "reordered relic options should not be expected");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, CreateRuneSelectionTestOptions(3)), "different relic option count should not be expected");
	}

	private static void NetworkChoiceTimeoutUsesNominalWallClockSeconds()
	{
		Equal(TimeSpan.Zero, HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(0), "zero timeout");
		Equal(TimeSpan.FromSeconds(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(600), "ack timeout");
		Equal(TimeSpan.FromMinutes(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(36000), "selection timeout");
	}

	private static void CombatTrackingPerTurnProcLimitsResetOncePerRound()
	{
		HextechMayhemCombatTrackingState tracking = new();
		tracking.SlapProcsThisTurn[1] = 1;
		tracking.TormentorProcsThisTurn[2] = 1;
		tracking.CourageProcsThisTurn[3] = 1;
		tracking.BloodPactProcsThisTurn[4] = 1;
		tracking.PlayerRuneProcsThisTurn["player:rune"] = 1;
		tracking.ClownCollegeProcsThisTurn[5] = 1;
		tracking.DevilsDanceTriggeredThisTurn.Add(6);
		tracking.FinalFormTriggeredThisTurn.Add(7);
		tracking.EnemyPorcupineTriggersThisTurn[8] = 1;
		tracking.EightPennyGatePlayersTriggeredThisTurn.Add(9);
		tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Add(10);
		tracking.MonsterDebuffActionProcKeysThisTurn.Add("debuff-action");

		tracking.PreparePlayerSideTurnEnd();

		Equal(1, tracking.ClownCollegeProcsThisTurn.Count, "player side end should keep clown college round proc count");
		Equal(1, tracking.EnemyPorcupineTriggersThisTurn.Count, "player side end should keep porcupine round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredThisTurn.Count, "player side end should keep eight penny gate first round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Count, "player side end should keep eight penny gate second round proc count");

		tracking.PrepareEnemySideTurnStart();

		Equal(1, tracking.SlapProcsThisTurn.Count, "enemy side start should keep slap round proc count");
		Equal(1, tracking.TormentorProcsThisTurn.Count, "enemy side start should keep tormentor round proc count");
		Equal(1, tracking.CourageProcsThisTurn.Count, "enemy side start should keep courage round proc count");
		Equal(1, tracking.BloodPactProcsThisTurn.Count, "enemy side start should keep blood pact round proc count");
		Equal(1, tracking.PlayerRuneProcsThisTurn.Count, "enemy side start should keep player rune round proc count");
		Equal(1, tracking.ClownCollegeProcsThisTurn.Count, "enemy side start should keep clown college round proc count");
		Equal(1, tracking.DevilsDanceTriggeredThisTurn.Count, "enemy side start should keep devil's dance round proc count");
		Equal(1, tracking.FinalFormTriggeredThisTurn.Count, "enemy side start should keep final form round proc count");
		Equal(1, tracking.EnemyPorcupineTriggersThisTurn.Count, "enemy side start should keep porcupine round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredThisTurn.Count, "enemy side start should keep eight penny gate first round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Count, "enemy side start should keep eight penny gate second round proc count");
		Equal(1, tracking.MonsterDebuffActionProcKeysThisTurn.Count, "enemy side start should keep monster debuff round guard");

		tracking.PreparePlayerSideTurnStart();

		Equal(0, tracking.SlapProcsThisTurn.Count, "player side start should reset slap round proc count");
		Equal(0, tracking.TormentorProcsThisTurn.Count, "player side start should reset tormentor round proc count");
		Equal(0, tracking.CourageProcsThisTurn.Count, "player side start should reset courage round proc count");
		Equal(0, tracking.BloodPactProcsThisTurn.Count, "player side start should reset blood pact round proc count");
		Equal(0, tracking.PlayerRuneProcsThisTurn.Count, "player side start should reset player rune round proc count");
		Equal(0, tracking.ClownCollegeProcsThisTurn.Count, "player side start should reset clown college round proc count");
		Equal(0, tracking.DevilsDanceTriggeredThisTurn.Count, "player side start should reset devil's dance round proc count");
		Equal(0, tracking.FinalFormTriggeredThisTurn.Count, "player side start should reset final form round proc count");
		Equal(0, tracking.EnemyPorcupineTriggersThisTurn.Count, "player side start should reset porcupine round proc count");
		Equal(0, tracking.EightPennyGatePlayersTriggeredThisTurn.Count, "player side start should reset eight penny gate first round proc count");
		Equal(0, tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Count, "player side start should reset eight penny gate second round proc count");
		Equal(0, tracking.MonsterDebuffActionProcKeysThisTurn.Count, "player side start should reset monster debuff round guard");
	}

	private static void MindOverMatterFirstDrawTrackingResetsPerPlayerTurn()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(MindOverMatterEnemyHex.TryConsumeFirstDraw(tracking, 11), "first draw for player one should trigger");
		Expect(!MindOverMatterEnemyHex.TryConsumeFirstDraw(tracking, 11), "second draw for player one should not trigger");
		Expect(MindOverMatterEnemyHex.TryConsumeFirstDraw(tracking, 22), "first draw for a different player should trigger independently");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);
		SetEqual(new ulong[] { 11, 22 }, restored.MindOverMatterPlayersTriggeredThisTurn, "first-draw guards should survive a mid-turn save/load");

		restored.PrepareEnemySideTurnStart();
		Equal(2, restored.MindOverMatterPlayersTriggeredThisTurn.Count, "enemy side start should not reopen the player-turn first draw");

		restored.PreparePlayerSideTurnStart();
		Equal(0, restored.MindOverMatterPlayersTriggeredThisTurn.Count, "next player turn should reset first-draw guards");
		Expect(MindOverMatterEnemyHex.TryConsumeFirstDraw(restored, 11), "the next player turn should trigger again");
	}

	private static void CombatTrackingGlobalProcOrdinalsSerializeAndReset()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Equal(0, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy-archmage:net:1"), "first global proc ordinal");
		Equal(1, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy-archmage:net:1"), "second global proc ordinal");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);

		Equal(2, restored.GlobalProcsThisCombat["enemy-archmage:net:1"], "global proc count should restore");
		Equal(2, HextechCombatProcTracker.ConsumeGlobalProcInCombat(restored, "enemy-archmage:net:1"), "restored next global proc ordinal");

		restored.PreparePlayerSideTurnStart();
		Equal(3, restored.GlobalProcsThisCombat["enemy-archmage:net:1"], "global proc count should persist across turn reset");

		restored.Reset();
		Equal(0, restored.GlobalProcsThisCombat.Count, "global proc count should clear on combat tracking reset");
	}

	// (PR#18)回归测试:镶宝铁拳符文曾在 ModifyCardPlayCount(引擎/UI 可能对同一次出牌重复求值)里直接调用
	// 会推进计数的 ConsumePlayerRuneProcInCombat,导致联机各端序号推进次数不一致、稳定随机结果分叉出即时断线。
	// 修复后 ModifyCardPlayCount 只应 peek(GetPlayerRuneProcsInCombat),真正消费放在每次真实出牌只触发一次的钩子里。
	private static void CombatTrackingPlayerRuneProcOrdinalPeekDoesNotConsume()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Player player = CreateOrdinalTestPlayer(7);
		const string procKey = nameof(JeweledGauntletRune);

		Equal(0, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "peek before any play should read zero");
		Equal(0, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "repeated peeks must not mutate the ordinal");
		Equal(0, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "a speculative ModifyCardPlayCount re-evaluation must be side-effect free");

		Equal(0, HextechCombatProcTracker.ConsumePlayerRuneProcInCombat(tracking, player, procKey), "first real play should consume ordinal 0");
		Equal(1, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "peek after one real play should reflect the committed ordinal");
		Equal(1, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "peeking again before the next real play must not advance the ordinal");

		Equal(1, HextechCombatProcTracker.ConsumePlayerRuneProcInCombat(tracking, player, procKey), "second real play should consume ordinal 1");
		Equal(2, HextechCombatProcTracker.GetPlayerRuneProcsInCombat(tracking, player, procKey), "ordinal should advance exactly once per real play, never per peek");
	}

	private static void CombatTrackingSerializationIsCultureInvariant()
	{
		HextechMayhemCombatTrackingState tracking = new();
		// 大小写混合键：culture 比较排 a<B，ordinal 排 B<a，用来暴露 culture-sensitive 排序。
		HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy:net:1:apower");
		HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy:net:1:Bpower");
		HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy:net:1:co-op");
		HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy:net:1:coop");

		System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
		try
		{
			List<string> serialized = [];
			foreach (string culture in new[] { "en-US", "zh-CN", "da-DK", "tr-TR" })
			{
				System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
				serialized.Add(tracking.Serialize());
			}

			for (int i = 1; i < serialized.Count; i++)
			{
				Equal(serialized[0], serialized[i], $"combat tracking serialization should be culture invariant (culture #{i})");
			}

			int upperIndex = serialized[0].IndexOf("Bpower", StringComparison.Ordinal);
			int lowerIndex = serialized[0].IndexOf("apower", StringComparison.Ordinal);
			Expect(upperIndex >= 0 && lowerIndex >= 0 && upperIndex < lowerIndex, "combat tracking keys should sort ordinally (B before a)");
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = original;
		}
	}

	// v15(0.8.4 出厂)默认禁用集的冻结快照。这是历史事实,不随注册表演进——注册表每次翻转默认启停
	// 都必须新增迁移链段,链走完应恰好落在当前出厂默认上(由下方测试守护)。
	private static readonly Type[] Version15FactoryDisabledRuneTypes =
	[
		typeof(AdaptiveCapacitorRune),
		typeof(AdvanceToRetreatRune),
		typeof(AnthonyBiasRune),
		typeof(AstralBodyRune),
		typeof(CorruptedBranchRune),
		typeof(CrackTheEggRune),
		typeof(CuttingEdgeAlchemistRune),
		typeof(DawnbringersResolveRune),
		typeof(EarthAwakensRune),
		typeof(EndlessRecoveryRune),
		typeof(EscapePlanRune),
		typeof(FeelTheBurnRune),
		typeof(HappyAccidentRune),
		typeof(HardBonesRune),
		typeof(HolyFireRune),
		typeof(MasterOfDualityRune),
		typeof(MindPurificationRune),
		typeof(NeowsGrudgeRune),
		typeof(NightParadeRune),
		typeof(NoNonsenseRune),
		typeof(OkBoomerangRune),
		typeof(OldIdolRune),
		typeof(PrimitiveMadnessRune),
		typeof(RegenerationSuppressionRune),
		typeof(SuperBrainRune),
		typeof(SwordFlightRune),
		typeof(WarmogsSpiritRune),
		typeof(WarmupExerciseRune)
	];

	private static void ConfigMigrationForceResetsBelowV15()
	{
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(14, ["some-user-custom-id"]);
		Equal(26, version, "v14 config should land on current version");
		SetEqual(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().ToArray(), disabled, "v14 config should force-reset to factory defaults");
	}

	// 「迁移链终点 == 新用户默认」双真值源守护:v15(0.8.4 出厂)默认禁用集是冻结基线,勿随注册表更新。
	// 若未来翻转某符文默认启停时只改了注册表旗标、忘了加迁移链段,此测试即红。
	private static void ConfigMigrationV15BaselineReachesCurrentDefault()
	{
		IReadOnlySet<string> baseline = HextechPlayerRuneConfigIds.FromTypes(Version15FactoryDisabledRuneTypes);
		(int version, IReadOnlySet<string> migrated) = HextechRuneConfiguration.MigrateDisabledIdsForTests(15, baseline);
		Equal(26, version, "v15 config should land on current version");
		SetEqual(
			HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().ToArray(),
			migrated,
			$"v15 factory defaults + migration chain should equal current factory defaults; migrated:\n{string.Join("\n", migrated.OrderBy(static id => id, StringComparer.Ordinal))}\ncurrent defaults:\n{string.Join("\n", HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().OrderBy(static id => id, StringComparer.Ordinal))}");
	}

	private static void ConfigMigrationV25AddsNewDefaultDisables()
	{
		(int playerVersion, IReadOnlySet<string> disabledPlayers) = HextechRuneConfiguration.MigrateDisabledIdsForTests(25, []);
		Equal(26, playerVersion, "v25 player config should land on current version");
		Expect(
			disabledPlayers.Contains(ModelDb.GetId<DullBladeRune>().Entry),
			"v25 player config migration should default-disable Dull Blade");

		(int monsterVersion, IReadOnlySet<string> disabledMonsters) =
			HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(25, []);
		Equal(26, monsterVersion, "v25 monster config should land on current version");
		Expect(
			disabledMonsters.Contains(MonsterHexKind.BlankCheck.ToString()),
			"v25 monster config migration should default-disable enemy Blank Check");
	}

	private static void ConfigMigrationCurrentVersionPreservesCustomDisabledIds()
	{
		string customId = HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().OrderBy(static id => id, StringComparer.Ordinal).First();
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(26, [customId]);
		Equal(26, version, "current-version config keeps version");
		SetEqual([customId], disabled, "current-version config should pass user selection through unchanged");

		(int monsterVersion, IReadOnlySet<string> disabledMonsters) =
			HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(26, [MonsterHexKind.FrostWraith.ToString()]);
		Equal(26, monsterVersion, "current-version monster config keeps version");
		SetEqual(
			[MonsterHexKind.FrostWraith.ToString()],
			disabledMonsters,
			"current-version monster config should preserve a user-enabled Blank Check");
	}

	// SavedProperty 属性名集合直接决定联机 net-id 布局(规范化按名排序):任何新增/改名/删除都必须是
	// 有意为之并同步更新清单文件,否则与线上旧版联机会 1014。此测试把该风险面从线上提前到 CI。
	private static void SavedPropertyManifestMatchesCheckedInList()
	{
		string manifestPath = Path.Combine(AppContext.BaseDirectory, "saved_property_manifest.txt");
		Expect(File.Exists(manifestPath), $"saved_property_manifest.txt should exist at {manifestPath}");

		string[] expected = File.ReadAllLines(manifestPath)
			.Select(static line => line.Trim())
			.Where(static line => line.Length > 0 && !line.StartsWith('#'))
			.ToArray();

		Type abstractModelType = typeof(AbstractModel);
		const BindingFlags propertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		HashSet<string> names = new(StringComparer.Ordinal);
		foreach (Type type in typeof(HextechCatalog).Assembly.GetTypes())
		{
			if (type.IsAbstract || !type.IsClass || !abstractModelType.IsAssignableFrom(type))
			{
				continue;
			}

			foreach (PropertyInfo property in type.GetProperties(propertyFlags))
			{
				bool isSavedProperty = property
					.GetCustomAttributes(inherit: true)
					.Any(static attr => attr.GetType().Name == "SavedPropertyAttribute");
				if (isSavedProperty)
				{
					names.Add(property.Name);
				}
			}
		}

		string[] actual = names.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
		SequenceEqual(
			expected,
			actual,
			$"SavedProperty manifest drift; actual list:\n{string.Join("\n", actual)}");
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

	private static void SearingAttackRuneGrantsUpgradedCard()
	{
		SearingAttackCard card = CreateMutableTestModel<SearingAttackCard>();

		SearingAttackRune.UpgradeGrantedCard(card);

		Equal(1, card.CurrentUpgradeLevel, "granted Searing Attack upgrade level");
		Equal(16m, card.DynamicVars.Damage.BaseValue, "granted Searing Attack damage");
	}

	private static void FortuneForgeRewardScalesByStacks()
	{
		FortuneForge forge = CreateMutableTestModel<FortuneForge>();
		Equal(100, forge.ExtraGoldRewardAmount, "single-stack Fortune Forge reward");

		forge.SavedStackCount = 2;
		Equal(200, forge.ExtraGoldRewardAmount, "two-stack Fortune Forge reward");
	}

	private static void NightmareHooksEveryDarkOrbPassiveTrigger()
	{
		MethodInfo target = HextechNightmareHooks.ResolvePassiveHookTarget();
		Equal(typeof(DarkOrb), target.DeclaringType, "nightmare hook declaring type");
		Equal(nameof(DarkOrb.Passive), target.Name, "nightmare hook method");
		SequenceEqual(
			new[] { typeof(PlayerChoiceContext), typeof(Creature) },
			target.GetParameters().Select(static parameter => parameter.ParameterType),
			"nightmare hook parameter types");
	}

	private static void NightmareEffectRunsOnceAfterEachPassiveTask()
	{
		TaskCompletionSource passive = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource effect = new(TaskCreationOptions.RunContinuationsAsynchronously);
		int effectCount = 0;
		Task wrapped = HextechNightmareHooks.CompletePassiveThen(
			passive.Task,
			() =>
			{
				Interlocked.Increment(ref effectCount);
				return effect.Task;
			});

		Equal(0, effectCount, "nightmare must wait for the dark orb passive");
		Expect(!wrapped.IsCompleted, "nightmare wrapper should await the passive");

		passive.SetResult();
		Expect(
			SpinWait.SpinUntil(() => Volatile.Read(ref effectCount) == 1, TimeSpan.FromSeconds(1)),
			"nightmare effect should begin after the passive completes");
		Expect(!wrapped.IsCompleted, "nightmare wrapper should await its appended damage");

		effect.SetResult();
		wrapped.GetAwaiter().GetResult();
		Equal(1, effectCount, "one passive should append exactly one nightmare effect");

		int repeatedEffectCount = 0;
		for (int i = 0; i < 2; i++)
		{
			HextechNightmareHooks.CompletePassiveThen(
				Task.CompletedTask,
				() =>
				{
					repeatedEffectCount++;
					return Task.CompletedTask;
				}).GetAwaiter().GetResult();
		}
		Equal(2, repeatedEffectCount, "two passive triggers should append exactly two nightmare effects");

		int failedPassiveEffectCount = 0;
		try
		{
			HextechNightmareHooks.CompletePassiveThen(
				Task.FromException(new InvalidOperationException("passive failed")),
				() =>
				{
					failedPassiveEffectCount++;
					return Task.CompletedTask;
				}).GetAwaiter().GetResult();
			throw new InvalidOperationException("failed passive should propagate");
		}
		catch (InvalidOperationException ex) when (ex.Message == "passive failed")
		{
		}
		Equal(0, failedPassiveEffectCount, "failed passive must not append nightmare damage");
	}

	private static void DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune()
	{
		HextechForgeRarityWeights weights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(65, 25, 10),
			hasDiceManiac: false);

		Equal(65, weights.Silver, "silver weight");
		Equal(25, weights.Gold, "gold weight");
		Equal(10, weights.Prismatic, "prismatic weight");
		Equal(100, weights.Total, "total weight");
	}

	private static void DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights()
	{
		HextechForgeRarityWeights defaultWeights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(65, 25, 10),
			hasDiceManiac: true);
		Equal(65, defaultWeights.Silver, "default silver weight");
		Equal(50, defaultWeights.Gold, "default gold weight");
		Equal(20, defaultWeights.Prismatic, "default prismatic weight");
		Equal(135, defaultWeights.Total, "default total weight");

		HextechForgeRarityWeights customWeights = HextechForgeGrantHelper.ApplyDiceManiacForgeRarityModifier(
			new HextechForgeRarityWeights(10, 20, 30),
			hasDiceManiac: true);
		Equal(10, customWeights.Silver, "custom silver weight");
		Equal(40, customWeights.Gold, "custom gold weight");
		Equal(60, customWeights.Prismatic, "custom prismatic weight");
		Equal(110, customWeights.Total, "custom total weight");
	}

	private static void StableRandomPlayerIdentityUsesNetIdBeforeLocalSlot()
	{
		Equal("net:123456789", HextechStableRandom.PlayerIdentityKey(0, 123456789UL), "host-local slot");
		Equal("net:123456789", HextechStableRandom.PlayerIdentityKey(1, 123456789UL), "client-local slot");
		Equal("slot:2", HextechStableRandom.PlayerIdentityKey(2, 0UL), "local fallback");
	}

	private static void StableRandomSequentialFloorsAvoidExcessClustering()
	{
		const int seedCount = 2048;
		const int floorCount = 24;
		double[] hitRates = new double[seedCount];
		double lagX = 0;
		double lagY = 0;
		double lagXX = 0;
		double lagYY = 0;
		double lagXY = 0;
		int lagPairs = 0;

		for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
		{
			string seed = $"TEST-SEED-{seedIndex:00000}";
			int hits = 0;
			int previousHit = -1;
			for (int floor = 1; floor <= floorCount; floor++)
			{
				int roll = HextechStableRandom.IndexFromRawParts(
					100,
					seed,
					"|act:",
					"0",
					"|floor:",
					floor.ToString(),
					"|",
					"dice-maniac-forge-reward",
					"|",
					"0:1",
					"|",
					"7");
				int hit = roll < 50 ? 1 : 0;
				hits += hit;
				if (previousHit >= 0)
				{
					lagX += previousHit;
					lagY += hit;
					lagXX += previousHit * previousHit;
					lagYY += hit * hit;
					lagXY += previousHit * hit;
					lagPairs++;
				}

				previousHit = hit;
			}

			hitRates[seedIndex] = (double)hits / floorCount;
		}

		double mean = hitRates.Average();
		double variance = hitRates.Select(rate => (rate - mean) * (rate - mean)).Average();
		double stdev = Math.Sqrt(variance);
		double lagMeanX = lagX / lagPairs;
		double lagMeanY = lagY / lagPairs;
		double lagVarianceX = lagXX / lagPairs - lagMeanX * lagMeanX;
		double lagVarianceY = lagYY / lagPairs - lagMeanY * lagMeanY;
		double lagCorrelation = (lagXY / lagPairs - lagMeanX * lagMeanY) / Math.Sqrt(lagVarianceX * lagVarianceY);

		Expect(mean is > 0.48 and < 0.52, $"stable random 50% mean should stay unbiased, got {mean:F4}");
		Expect(stdev < 0.11, $"stable random sequential floor stdev should not show excess clustering, got {stdev:F4}");
		Expect(Math.Abs(lagCorrelation) < 0.02, $"stable random lag-1 correlation should stay near zero, got {lagCorrelation:F4}");
	}

	private static void StableRandomPowerOfTwoIndexesAvoidTerminalCounterCycle()
	{
		int[] circleTargets = Enumerable.Range(0, 8)
			.Select(historyCount => HextechStableRandom.IndexFromRawParts(
				4,
				"TEST-SEED",
				"|act:",
				"0",
				"|floor:",
				"12",
				"|",
				"circle-of-death-target",
				"|",
				"0:1",
				"|",
				"1",
				"|",
				"12",
				"|",
				historyCount.ToString()))
			.ToArray();

		int[] miseryTargets = Enumerable.Range(1, 8)
			.Select(roundNumber => HextechStableRandom.IndexFromRawParts(
				4,
				"TEST-SEED",
				"|act:",
				"0",
				"|floor:",
				"12",
				"|",
				"misery-target",
				"|",
				"0:1",
				"|",
				roundNumber.ToString()))
			.ToArray();

		Expect(!IsModuloStepCycle(circleTargets, 4), $"circle-of-death target sequence should not be a fixed modulo cycle: [{string.Join(", ", circleTargets)}]");
		Expect(!IsModuloStepCycle(miseryTargets, 4), $"misery target sequence should not be a fixed modulo cycle: [{string.Join(", ", miseryTargets)}]");
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

	private static void EnemyHexCountStateNormalizesMissingAndOutOfRangeValues()
	{
		SequenceEqual(new[] { 1, 1, 1 }, HextechPlayerHexCountState.Normalize(null), "null player count snapshot");
		SequenceEqual(new[] { 1, 2, 3 }, HextechEnemyHexCountState.Normalize(null), "null enemy count snapshot");
		SequenceEqual(new[] { 0, 6, 3 }, HextechEnemyHexCountState.Normalize([ -1, 7 ]), "partial clamped enemy count snapshot");

		HextechEnemyHexCountState state = new();
		state.Set([ 2, 3, 4, 5 ]);
		SequenceEqual(new[] { 2, 3, 4 }, state.Snapshot, "state should keep exactly three normalized act counts");
	}

	private static void RunConfigurationDefaultSnapshotUsesExpectedActCounts()
	{
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot();
		SequenceEqual(new[] { 1, 1, 1 }, snapshot.PlayerHexCountsByAct, "default player act counts");
		SequenceEqual(new[] { 1, 2, 3 }, snapshot.EnemyHexCountsByAct, "default enemy act counts");
		Equal(1, snapshot.PlayerRuneRerollLimit, "default player reroll limit");
		Equal(HextechRuneConfiguration.InfiniteRerollLimit, snapshot.MonsterHexRerollLimit, "default monster reroll limit");
	}

	private static void RunConfigurationDefaultSnapshotDisablesRiskyContent()
	{
		// 腐化树枝自配置 v16 起转为默认启用;改用长期默认禁用的逃跑计划做代表。
		string escapePlanId = ModelDb.GetId<EscapePlanRune>().Entry;
		string corruptedBranchId = ModelDb.GetId<CorruptedBranchRune>().Entry;
		string dullBladeId = ModelDb.GetId<DullBladeRune>().Entry;
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot();

		Expect(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().Contains(escapePlanId), "default player rune ids should disable escape plan");
		Expect(snapshot.DisabledPlayerRuneIds.Contains(escapePlanId), "default snapshot should disable escape plan");
		Expect(snapshot.DisabledPlayerRuneIds.Contains(dullBladeId), "default snapshot should disable Dull Blade");
		Expect(!snapshot.DisabledPlayerRuneIds.Contains(corruptedBranchId), "corrupted branch should be enabled by default since config v16");
		Expect(
			snapshot.DisabledMonsterHexIds.Contains(MonsterHexKind.BlankCheck.ToString()),
			"default snapshot should disable enemy Blank Check without removing it from the configurable pool");
	}

	private static void RerollLimitConfigUsesZeroToNineThenInfinite()
	{
		Equal(0, HextechRuneConfiguration.StepRerollLimit(0, -1), "zero stays zero on decrement");
		Equal(1, HextechRuneConfiguration.StepRerollLimit(0, 1), "zero increments to one");
		Equal(9, HextechRuneConfiguration.StepRerollLimit(8, 1), "eight increments to nine");
		Equal(HextechRuneConfiguration.InfiniteRerollLimit, HextechRuneConfiguration.StepRerollLimit(9, 1), "nine increments to infinite");
		Equal(9, HextechRuneConfiguration.StepRerollLimit(HextechRuneConfiguration.InfiniteRerollLimit, -1), "infinite decrements to nine");
		Equal(HextechRuneConfiguration.InfiniteRerollLimit, HextechRuneConfiguration.StepRerollLimit(HextechRuneConfiguration.InfiniteRerollLimit, 1), "infinite stays infinite on increment");
		Equal(9, HextechRuneConfiguration.ClampRerollLimit(99), "finite values clamp to nine");
	}

	private static void RandomForgeShopRelicUpdatesDisplayedPrice()
	{
		RandomForgeShopRelic relic = new();

		Equal(HextechRuneConfiguration.GetDefaultRandomForgeShopPrice(), relic.DynamicVars["Price"].IntValue, "default displayed forge price");
		relic.SetDisplayedPrice(777);
		Equal(777, relic.DynamicVars["Price"].IntValue, "updated displayed forge price");
		relic.SetDisplayedPrice(99999);
		Equal(9999, relic.DynamicVars["Price"].IntValue, "displayed forge price clamps to config maximum");
		relic.SetDisplayedPrice(-12);
		Equal(0, relic.DynamicVars["Price"].IntValue, "displayed forge price clamps to config minimum");
	}

	private static void EnemyHexCountStateUsesThirdActForEndlessAndBeyondThirdAct()
	{
		HextechEnemyHexCountState state = new();
		state.Set([ 1, 2, 3 ]);

		Equal(1, state.GetForAct(-1, endless: false), "negative act clamps to first act");
		Equal(1, state.GetForAct(0, endless: false), "first act count");
		Equal(2, state.GetForAct(1, endless: false), "second act count");
		Equal(3, state.GetForAct(2, endless: false), "third act count");
		Equal(3, state.GetForAct(3, endless: false), "beyond third act count");
		Equal(3, state.GetForAct(0, endless: true), "endless first loop uses third act count");
	}

	private static void PlayerRuneConfigSnapshotStateUsesClientFallbackWithoutSnapshot()
	{
		string localDisabledId = HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.First()
			.Entry;
		HextechPlayerRuneConfigSnapshotState state = new();

		Expect(!state.HasSnapshot, "new player rune config state should not have snapshot");
		SetEqual([ localDisabledId ], state.GetDisabledIdsForPool(isClient: false, [ localDisabledId ]), "host/local fallback disabled ids");
		Expect(state.GetDisabledIdsForPool(isClient: true, [ localDisabledId ]).Count == 0, "client fallback should ignore local disabled ids without host snapshot");
	}

	private static void PlayerRuneConfigSnapshotStateSnapshotOverridesLocalFallback()
	{
		string[] ids = HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.Take(2)
			.Select(static id => id.Entry)
			.ToArray();
		HextechPlayerRuneConfigSnapshotState state = new();

		state.Set([ ids[1] ]);

		Expect(state.HasSnapshot, "snapshot should be present after set");
		Equal(1, state.SnapshotCount, "snapshot count");
		SetEqual([ ids[1] ], state.GetDisabledIdsForPool(isClient: true, [ ids[0] ]), "snapshot should override client fallback");
		SetEqual([ ids[1] ], state.GetDisabledIdsForPool(isClient: false, [ ids[0] ]), "snapshot should override host fallback");
	}

	private static void PlayerRuneConfigSnapshotStateSerializesAndClearsMalformedData()
	{
		string[] ids = HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderByDescending(static id => id.Entry, StringComparer.Ordinal)
			.Take(2)
			.Select(static id => id.Entry)
			.ToArray();
		HextechPlayerRuneConfigSnapshotState state = new();

		state.Set(ids);
		string serialized = state.Serialize();
		HextechPlayerRuneConfigSnapshotState restored = new();
		Expect(restored.TryRestore(serialized, out string? restoreError), $"serialized snapshot should restore: {restoreError}");
		SetEqual(ids, restored.GetDisabledIdsForPool(isClient: true, []), "restored snapshot ids");

		Expect(!restored.TryRestore("{", out string? malformedError), "malformed snapshot should fail");
		Expect(!string.IsNullOrWhiteSpace(malformedError), "malformed snapshot should return an error");
		Expect(!restored.HasSnapshot, "malformed snapshot should clear existing snapshot");
		Expect(restored.Serialize() == "", "cleared snapshot should serialize as empty string");
	}

	private static void MayhemRunContextResetForNewRunClearsState()
	{
		HextechMayhemRunContext context = new();
		context.ActState.SetResolved(0, true);
		context.ChoiceHistory.SavedTelemetryChoicesJson = "[1]";
		context.CombatTracking.EnemyProtectiveVeilTurnCounter = 7;
		context.HexCountRecoveryBaseline = 5;
		context.MonsterHexStrengthTierFloor = 3;
		context.EnemyTezcatarasMercyCombatCounter = 4;
		context.HostUsesBetterMultiplayerScaling = true;

		context.ResetForNewRun([ 7, -1, 2 ], [ 2, 7, -1 ]);

		SequenceEqual(new[] { 6, 0, 2 }, context.PlayerHexCounts.Snapshot, "new-run player count snapshot");
		SequenceEqual(new[] { 2, 6, 0 }, context.EnemyHexCounts.Snapshot, "new-run enemy count snapshot");
		Equal(0, context.HexCountRecoveryBaseline, "new-run recovery baseline");
		Equal(0, context.MonsterHexStrengthTierFloor, "new-run strength floor");
		Equal(0, context.EnemyTezcatarasMercyCombatCounter, "new-run tezcataras counter");
		Expect(!context.ActState.IsResolved(0), "new-run act state should reset");
		Equal("", context.ChoiceHistory.SavedTelemetryChoicesJson, "new-run telemetry choices should reset");
		Equal(0, context.CombatTracking.EnemyProtectiveVeilTurnCounter, "new-run combat tracking should reset");
		Equal(true, context.HostUsesBetterMultiplayerScaling, "new-run should preserve host scaling flag until act roll refreshes it");
	}

	private static void MayhemRunContextResetForEndlessLoopCarriesActiveMonsterHex()
	{
		HextechMayhemRunContext context = new();
		context.EnemyHexCounts.Set([ 1, 2, 3 ]);
		context.ActState.SetMonsterHexes(1, [ MonsterHexKind.ShrinkRay ]);
		context.ActState.SetResolved(1, true);
		context.ChoiceHistory.SavedSeenPlayerRuneIdsJson = "{\"0\":[\"A\"]}";
		context.CombatTracking.EnemyProtectiveVeilTurnCounter = 9;

		context.ResetForEndlessLoop(6);

		SequenceEqual(new[] { 1, 2, 3 }, context.EnemyHexCounts.Snapshot, "endless reset should keep enemy count snapshot");
		Equal(6, context.HexCountRecoveryBaseline, "endless recovery baseline");
		Equal(3, context.MonsterHexStrengthTierFloor, "endless strength floor");
		Expect(context.IsEndlessLoopActive, "endless flag");
		Expect(!context.ActState.IsResolved(1), "endless reset should clear resolved acts");
		Expect(context.ActState.GetKnownMonsterHexes().Contains(MonsterHexKind.ShrinkRay), "endless reset should carry latest active monster hex");
		Equal("", context.ChoiceHistory.SavedSeenPlayerRuneIdsJson, "endless reset should clear seen runes");
		Equal(0, context.CombatTracking.EnemyProtectiveVeilTurnCounter, "endless reset should clear combat tracking");
	}

	private static void MayhemRunContextDebugResetSetsOnlyRequestedMonsterHex()
	{
		HextechMayhemRunContext context = new();
		context.EnemyHexCounts.Set([ 2, 3, 4 ]);
		context.ActState.SetMonsterHexes(0, [ MonsterHexKind.FrostWraith ]);
		context.ActState.SetResolved(0, true);
		context.HexCountRecoveryBaseline = 2;
		context.MonsterHexStrengthTierFloor = 3;
		context.EnemyTezcatarasMercyCombatCounter = 5;

		context.ResetForDebugMonsterHex(2, MonsterHexKind.PandorasBox, HextechRarityTier.Prismatic);

		SequenceEqual(new[] { 1, 2, 3 }, context.EnemyHexCounts.Snapshot, "debug reset enemy count snapshot");
		Equal(0, context.HexCountRecoveryBaseline, "debug reset recovery baseline");
		Equal(0, context.MonsterHexStrengthTierFloor, "debug reset strength floor");
		Equal(0, context.EnemyTezcatarasMercyCombatCounter, "debug reset tezcataras counter");
		SequenceEqual(new[] { MonsterHexKind.PandorasBox }, context.ActState.GetMonsterHexes(2), "debug reset monster hex");
		Expect(context.ActState.IsResolved(2), "debug reset should resolve requested act");
		Expect(!context.ActState.GetKnownMonsterHexes().Contains(MonsterHexKind.FrostWraith), "debug reset should discard previous monster hexes");
	}

	private static HashSet<string> GetConfigurableRuneEntries(params HextechRarityTier[] rarities)
	{
		return rarities
			.SelectMany(HextechCatalog.GetConfigurablePlayerRuneTypesForRarity)
			.Select(static type => ModelDb.GetId(type).Entry)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static void PlayerRuneMetadataHasUniqueTypes()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;
		Type[] duplicatedTypes = metadata.Registrations
			.GroupBy(static registration => registration.Type)
			.Where(static group => group.Count() > 1)
			.Select(static group => group.Key)
			.ToArray();

		Expect(duplicatedTypes.Length == 0, $"duplicate player rune registrations: {string.Join(", ", duplicatedTypes.Select(static type => type.Name))}");
		SequenceEqual(
			metadata.Registrations.Select(static registration => registration.Type).Distinct(),
			metadata.AllTypes,
			"all player rune metadata types");
	}

	private static void PlayerRuneMetadataMatchesContentRegistrySlices()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;

		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Silver], HextechContentRegistry.SilverRuneTypes, "silver runes");
		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Gold], HextechContentRegistry.GoldRuneTypes, "gold runes");
		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Prismatic], HextechContentRegistry.PrismaticRuneTypes, "prismatic runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.Disabled], HextechContentRegistry.DisabledPlayerRuneTypes, "default disabled runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.SelectionExcluded], HextechContentRegistry.SelectionExcludedPlayerRuneTypes, "selection excluded runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.Retired], HextechContentRegistry.RetiredPlayerRuneTypes, "retired runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.FirstActExcluded], HextechContentRegistry.FirstActExcludedRuneTypes, "first act excluded runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.ThirdActExcluded], HextechContentRegistry.ThirdActExcludedRuneTypes, "third act excluded runes");
		SequenceEqual(metadata.TypesByFlag[PlayerRuneFlags.AttributeConversionExclusive], HextechContentRegistry.AttributeConversionExclusiveRuneTypes, "attribute conversion exclusive runes");
		Expect(metadata.TagKeys.Count == HextechContentRegistry.PlayerRuneTagKeys.Count, "tag key count should match");
		foreach ((Type type, string tagKey) in metadata.TagKeys)
		{
			Expect(HextechContentRegistry.PlayerRuneTagKeys.TryGetValue(type, out string? registryTag), $"missing tag key for {type.Name}");
			Equal(tagKey, registryTag, $"tag key for {type.Name}");
		}
	}

	private static void PlayerRuneMetadataPreservesCharacterOrder()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;

		foreach (PlayerRuneCharacterPool characterPool in Enum.GetValues<PlayerRuneCharacterPool>())
		{
			Type[] expected = metadata.Registrations
				.Where(registration => registration.CharacterPool == characterPool)
				.OrderBy(static registration => registration.CharacterOrder)
				.Select(static registration => registration.Type)
				.ToArray();
			SequenceEqual(expected, metadata.TypesByCharacter[characterPool], $"{characterPool} character runes");
		}
	}

	private static void PlayerRuneMetadataClassifiesConfigStates()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;
		PlayerRuneRegistration defaultDisabled = metadata.Registrations.First(registration =>
			metadata.HasFlag(registration.Type, PlayerRuneFlags.Disabled)
			&& !metadata.HasFlag(registration.Type, PlayerRuneFlags.SelectionExcluded));

		Expect(!metadata.IsVisible(defaultDisabled.Type), "default disabled rune should not be visible by default");
		Expect(metadata.IsConfigurable(defaultDisabled.Type), "default disabled rune should remain configurable");
		Expect(!metadata.IsSelectable(defaultDisabled.Type), "default disabled rune should not be selectable");
		Expect(!HextechCatalog.IsPlayerRuneTypeVisible(defaultDisabled.Type), "catalog default disabled visibility");
		Expect(HextechCatalog.IsPlayerRuneTypeConfigurable(defaultDisabled.Type), "catalog default disabled configurability");
		Expect(!HextechCatalog.IsPlayerRuneTypeSelectable(defaultDisabled.Type), "catalog default disabled selectability");

		PlayerRuneRegistration selectionExcluded = metadata.Registrations.First(registration =>
			metadata.HasFlag(registration.Type, PlayerRuneFlags.SelectionExcluded)
			&& !metadata.HasFlag(registration.Type, PlayerRuneFlags.Disabled));
		Expect(metadata.IsVisible(selectionExcluded.Type), "selection excluded rune should still be visible");
		Expect(!metadata.IsConfigurable(selectionExcluded.Type), "selection excluded rune should not be configurable");
		Expect(!metadata.IsSelectable(selectionExcluded.Type), "selection excluded rune should not be selectable");
		Expect(HextechCatalog.IsPlayerRuneTypeVisible(selectionExcluded.Type), "catalog selection excluded visibility");
		Expect(!HextechCatalog.IsPlayerRuneTypeConfigurable(selectionExcluded.Type), "catalog selection excluded configurability");
		Expect(!HextechCatalog.IsPlayerRuneTypeSelectable(selectionExcluded.Type), "catalog selection excluded selectability");
	}

	private static void WellLaidPlansUpgradeRuneIsRetiredButSaveCompatible()
	{
		Type retiredType = typeof(WellLaidPlansUpgradeRune);
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;

		Expect(metadata.IsRegistered(retiredType), "retired Well-Laid Plans rune model should remain registered for old saves");
		Expect(metadata.HasFlag(retiredType, PlayerRuneFlags.Retired), "Well-Laid Plans rune should carry the retired flag");
		Expect(HextechContentRegistry.RetiredPlayerRuneTypes.Contains(retiredType), "retired registry slice should contain Well-Laid Plans");
		Expect(!HextechCatalog.IsPlayerRuneTypeVisible(retiredType), "retired Well-Laid Plans rune should be hidden");
		Expect(!HextechCatalog.IsPlayerRuneTypeConfigurable(retiredType), "retired Well-Laid Plans rune should not be configurable");
		Expect(!HextechCatalog.IsPlayerRuneTypeSelectable(retiredType), "retired Well-Laid Plans rune should not be selectable");
		Expect(
			HextechCatalog.GetAllCustomRelicTypes().Contains(retiredType),
			"retired Well-Laid Plans rune model should remain in custom model registration for old saves");
	}

	private static void PlayerRuneMetadataCatalogOutputsMatchCatalogQueries()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;

		foreach (HextechRarityTier rarity in Enum.GetValues<HextechRarityTier>())
		{
			SequenceEqual(
				metadata.GetSelectableTypesForRarity(rarity),
				HextechCatalog.GetPlayerRuneTypesForRarity(rarity),
				$"{rarity} selectable runes");
			SequenceEqual(
				metadata.GetConfigurableTypesForRarity(rarity),
				HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(rarity),
				$"{rarity} configurable runes");
		}
	}

	private static void PlayerRuneMetadataFallbacksAreStable()
	{
		PlayerRuneMetadataCatalog metadata = HextechContentRegistry.PlayerRuneMetadata;

		Expect(!metadata.IsRegistered(typeof(Program)), "test program type should not be registered as rune metadata");
		Expect(!metadata.TryGetRegistration(typeof(Program), out _), "unknown type registration lookup should fail");
		Expect(!metadata.TryGetRarity(typeof(Program), out _), "unknown type rarity lookup should fail");
		Equal(3, metadata.GetRaritySortOrder(typeof(Program)), "unknown type rarity sort order");
		Equal(HextechPlayerRuneRegistry.DefaultTagKey, metadata.GetTagKey(typeof(Program)), "unknown type tag key");
	}

	private static void ForgeMetadataHasUniqueTypes()
	{
		ForgeMetadataCatalog metadata = HextechContentRegistry.ForgeMetadata;
		Type[] duplicatedTypes = metadata.Registrations
			.GroupBy(static registration => registration.Type)
			.Where(static group => group.Count() > 1)
			.Select(static group => group.Key)
			.ToArray();

		Expect(duplicatedTypes.Length == 0, $"duplicate forge registrations: {string.Join(", ", duplicatedTypes.Select(static type => type.Name))}");
		SequenceEqual(
			metadata.Registrations.Select(static registration => registration.Type).Distinct(),
			metadata.AllTypes,
			"all forge metadata types");
	}

	private static void ForgeMetadataMatchesContentRegistrySlices()
	{
		ForgeMetadataCatalog metadata = HextechContentRegistry.ForgeMetadata;

		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Silver], HextechContentRegistry.SilverForgeTypes, "silver forges");
		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Gold], HextechContentRegistry.GoldForgeTypes, "gold forges");
		SequenceEqual(metadata.TypesByRarity[HextechRarityTier.Prismatic], HextechContentRegistry.PrismaticForgeTypes, "prismatic forges");
		SequenceEqual(metadata.AllTypes, HextechContentRegistry.AllForgeTypes, "all forges");
	}

	private static void ForgeMetadataFallbacksAreStable()
	{
		ForgeMetadataCatalog metadata = HextechContentRegistry.ForgeMetadata;

		Expect(!metadata.IsRegistered(typeof(Program)), "test program type should not be registered as forge metadata");
		Expect(!metadata.TryGetRarity(typeof(Program), out _), "unknown forge type rarity lookup should fail");
	}

	private static void MonsterHexMetadataHasUniqueKinds()
	{
		MonsterHexMetadataCatalog metadata = HextechContentRegistry.MonsterHexMetadata;
		MonsterHexKind[] duplicatedKinds = metadata.Registrations
			.GroupBy(static registration => registration.Kind)
			.Where(static group => group.Count() > 1)
			.Select(static group => group.Key)
			.ToArray();

		Expect(duplicatedKinds.Length == 0, $"duplicate monster hex registrations: {string.Join(", ", duplicatedKinds)}");
		SetEqual(
			metadata.Registrations.Select(static registration => registration.Kind),
			metadata.AllKinds,
			"all monster hex metadata kinds");
	}

	private static void MonsterHexMetadataMatchesContentRegistrySlices()
	{
		MonsterHexMetadataCatalog metadata = HextechContentRegistry.MonsterHexMetadata;

		SequenceEqual(metadata.EnabledKindsByRarity[HextechRarityTier.Silver], HextechContentRegistry.SilverMonsterHexes, "silver monster hexes");
		SequenceEqual(metadata.EnabledKindsByRarity[HextechRarityTier.Gold], HextechContentRegistry.GoldMonsterHexes, "gold monster hexes");
		SequenceEqual(metadata.EnabledKindsByRarity[HextechRarityTier.Prismatic], HextechContentRegistry.PrismaticMonsterHexes, "prismatic monster hexes");
		SetEqual(metadata.DisabledKinds, HextechContentRegistry.DisabledMonsterHexes, "disabled monster hexes");
		SetEqual(metadata.BurnHoverTipKinds, HextechContentRegistry.MonsterHexesWithBurnHoverTip, "burn hover tip monster hexes");
		SetEqual(metadata.AllKinds, HextechContentRegistry.AllMonsterHexKinds, "all monster hexes");
		Expect(metadata.IconRelicTypes.Count == HextechContentRegistry.MonsterHexIconRelicTypes.Count, "monster hex icon count should match");
		foreach ((MonsterHexKind kind, Type iconRelicType) in metadata.IconRelicTypes)
		{
			Expect(HextechContentRegistry.MonsterHexIconRelicTypes.TryGetValue(kind, out Type? registryIconType), $"missing monster hex icon for {kind}");
			Equal(iconRelicType, registryIconType, $"monster hex icon for {kind}");
		}
	}

	private static void MonsterHexMetadataKeepsDisabledKindsOutOfRarityPools()
	{
		MonsterHexMetadataCatalog metadata = HextechContentRegistry.MonsterHexMetadata;
		Expect(
			metadata.IsEnabled(MonsterHexKind.BlankCheck),
			"enemy Blank Check should remain registry-enabled so players can opt it back in through configuration");
		MonsterHexRegistration[] disabledRegistrations = metadata.Registrations
			.Where(static registration => registration.Disabled)
			.ToArray();
		if (disabledRegistrations.Length == 0)
		{
			Expect(metadata.DisabledKinds.Count == 0, "no disabled monster hexes should leave disabled set empty");
			Expect(!metadata.EnabledKindsByRarity.Values.Any(kinds => kinds.Any(kind => metadata.DisabledKinds.Contains(kind))), "rarity pools should not contain disabled monster hexes");
			Expect(!metadata.IsRegistered((MonsterHexKind)int.MaxValue), "invalid monster hex kind should not be registered");
			return;
		}

		MonsterHexRegistration disabledRegistration = disabledRegistrations[0];
		Expect(metadata.AllKinds.Contains(disabledRegistration.Kind), "disabled monster hex should stay in all-kinds set");
		Expect(metadata.DisabledKinds.Contains(disabledRegistration.Kind), "disabled monster hex should stay in disabled set");
		Expect(!metadata.IsEnabled(disabledRegistration.Kind), "disabled monster hex should not be enabled");
		Expect(!metadata.EnabledKindsByRarity[disabledRegistration.Rarity].Contains(disabledRegistration.Kind), "disabled monster hex should not appear in rarity pool");
		Expect(metadata.TryGetRegistration(disabledRegistration.Kind, out MonsterHexRegistration decoded), "disabled monster hex registration should decode");
		Equal(disabledRegistration.IconRelicType, decoded.IconRelicType, "disabled monster hex icon relic type");
		Expect(!metadata.IsRegistered((MonsterHexKind)int.MaxValue), "invalid monster hex kind should not be registered");
	}

	private static void MonsterInteractionPolicyPreservesStructuralMonsterBuffs()
	{
		Expect(HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(new ReattachPower()), "reattach power should be structural");
		Expect(HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(new AdaptablePower()), "adaptable power should be structural");
		Expect(HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(new SandpitPower()), "sandpit power should be structural");
		Expect(!HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(new StrengthPower()), "ordinary strength should not be structural");
		Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(new PersonalHivePower()), "personal hive should not be mirrored to players");
		Expect(!HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(new StrengthPower()), "ordinary strength should remain mirrorable");
	}

	private static void PersonalHiveSafetyRejectsPlayerSideCopies()
	{
		MethodInfo target = HextechPersonalHiveSafetyHooks.ResolveDamageResponseTarget();
		Equal(typeof(PersonalHivePower), target.DeclaringType, "personal hive safety hook declaring type");
		Equal(nameof(PersonalHivePower.AfterDamageReceived), target.Name, "personal hive safety hook method");
		SequenceEqual(
			new[]
			{
				typeof(PlayerChoiceContext),
				typeof(Creature),
				typeof(DamageResult),
				typeof(ValueProp),
				typeof(Creature),
				typeof(CardModel),
			},
			target.GetParameters().Select(static parameter => parameter.ParameterType),
			"personal hive safety hook parameter types");

		Expect(HextechPersonalHiveSafetyHooks.ShouldRunOriginal(CombatSide.Enemy), "enemy-owned personal hive should keep vanilla behavior");
		Expect(!HextechPersonalHiveSafetyHooks.ShouldRunOriginal(CombatSide.Player), "player-owned personal hive should be neutralized");
		Expect(!HextechPersonalHiveSafetyHooks.ShouldRunOriginal(null), "ownerless personal hive should be neutralized");
	}

	private static void EnemyCompensationPoisonUsesOneThirdRoundedDownWithMinimum()
	{
		Equal(0, CompensationEnemyHex.CalculateReplacementPoison(0m), "zero damage replacement poison");
		Equal(1, CompensationEnemyHex.CalculateReplacementPoison(1m), "one damage replacement poison");
		Equal(1, CompensationEnemyHex.CalculateReplacementPoison(2m), "two damage replacement poison");
		Equal(1, CompensationEnemyHex.CalculateReplacementPoison(3m), "three damage replacement poison");
		Equal(1, CompensationEnemyHex.CalculateReplacementPoison(5m), "five damage replacement poison");
		Equal(2, CompensationEnemyHex.CalculateReplacementPoison(6m), "six damage replacement poison");
		Equal(333, CompensationEnemyHex.CalculateReplacementPoison(999m), "large damage replacement poison");
	}

	private static void EnemyCompensationSkipsPoisonDamageSignature()
	{
		Expect(
			CompensationEnemyHex.IsPoisonDamageSignature(ValueProp.Unblockable | ValueProp.Unpowered, null, null),
			"unblockable unpowered damage without dealer or card should match poison damage signature");
		Expect(
			!CompensationEnemyHex.IsPoisonDamageSignature(ValueProp.Unblockable, null, null),
			"missing unpowered flag should not match poison damage signature");
		Expect(
			!CompensationEnemyHex.IsPoisonDamageSignature(ValueProp.Unpowered, null, null),
			"missing unblockable flag should not match poison damage signature");
		Expect(
			!CompensationEnemyHex.IsPoisonDamageSignature(ValueProp.Unblockable | ValueProp.Unpowered, (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature)), null),
			"damage with dealer should not match poison damage signature");
		Expect(
			!CompensationEnemyHex.IsPoisonDamageSignature(ValueProp.Unblockable | ValueProp.Unpowered, null, UninitializedCard<SovereignBlade>()),
			"damage with card source should not match poison damage signature");
	}

	private static void EnemyCompensationSkipsOutbreakPoisonResponse()
	{
		Creature target = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		Creature dealer = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));

		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(target, ValueProp.Unpowered, dealer, null),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithOutbreakPowerPoisonResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement(target, ValueProp.Unpowered, dealer, null);
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedInsideGuard, "outbreak poison response damage should skip compensation replacement");
		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should reset after guarded work");
	}

	private static void EnemyCompensationSkipsSleightOfFleshResponse()
	{
		Creature target = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		Creature dealer = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));

		Expect(!HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse, "sleight response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(target, ValueProp.Unpowered, dealer, null),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithSleightOfFleshPowerDebuffResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement(target, ValueProp.Unpowered, dealer, null);
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedInsideGuard, "sleight of flesh response damage should skip compensation replacement to avoid the poison recursion stack overflow");
		Expect(!HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse, "sleight response guard should reset after guarded work");
	}

	private static void ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless()
	{
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<SovereignBlade>()), "sovereign blade should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionStrike>()), "minion strike should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionDiveBomb>()), "minion dive bomb should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionSacrifice>()), "minion sacrifice should count as colorless");
	}

	private static T UninitializedCard<T>() where T : CardModel
	{
		return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
	}

	private static T CreateMutableTestModel<T>()
		where T : AbstractModel, new()
	{
		T model = new();
		typeof(AbstractModel)
			.GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(model, true);
		return model;
	}

	private static void CompensationReplacementGuardScopesAsyncWork()
	{
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should start inactive");
		TaskCompletionSource gate = new();
		bool sawActiveBeforeAwait = false;
		bool sawActiveAfterAwait = false;
		Task guarded = HextechCombatHooks.RunWithCompensationReplacementGuard(async () =>
		{
			sawActiveBeforeAwait = HextechCombatHooks.IsApplyingCompensationReplacement;
			await gate.Task;
			sawActiveAfterAwait = HextechCombatHooks.IsApplyingCompensationReplacement;
		});

		Expect(sawActiveBeforeAwait, "compensation replacement guard should be active before guarded work awaits");
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should not leak to caller context");
		gate.SetResult();
		guarded.GetAwaiter().GetResult();
		Expect(sawActiveAfterAwait, "compensation replacement guard should remain active after await inside guarded work");
		Expect(!HextechCombatHooks.IsApplyingCompensationReplacement, "compensation replacement guard should reset after guarded work");
	}

	private static void CompensationReplacementSuppressesSleightOfFleshResponse()
	{
		Expect(
			!HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(true),
			"sleight response should not be suppressed outside compensation replacement");

		bool suppressedInsideGuard = false;
		HextechCombatHooks.RunWithCompensationReplacementGuard(() =>
		{
			suppressedInsideGuard = HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(true);
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(suppressedInsideGuard, "sleight response should be suppressed during compensation replacement");
		Expect(
			!HextechCombatHooks.ShouldSuppressSleightOfFleshPowerDebuffResponse(false),
			"sleight response should not be suppressed when the power change would not trigger sleight");
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

	private static void DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect()
	{
		DustyTome source = CreateTestDustyTome();
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
				bool runCopiedAfterObtained = HextechRewardSafetyHooks.DustyTomeAfterObtainedPrefix(candidate, ref copiedAfterObtained);
				if (runCopiedAfterObtained)
				{
					ancientCardGrantCount++;
				}
				Expect(!runCopiedAfterObtained, "copied Dusty Tome AfterObtained prefix should skip the original");
				Expect(copiedAfterObtained.IsCompletedSuccessfully, "copied Dusty Tome AfterObtained should return a completed task");
				Expect(!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(source), "source Dusty Tome must not be suppressed");
				Task sourceAfterObtained = Task.CompletedTask;
				Expect(
					HextechRewardSafetyHooks.DustyTomeAfterObtainedPrefix(source, ref sourceAfterObtained),
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
		Expect(!DoubleVisionRune.ShouldSuppressDustyTomeAfterObtained(copy), "Dusty Tome suppression must end after obtain");
	}

	private static void DoubleVisionDustyTomeSaveLoadPreservesAncientCard()
	{
		DustyTome source = CreateTestDustyTome();
		// 测试宿主不会执行原版 ModelIdSerializationCache.Init；显式注入这个原版载体，
		// 等价于真实启动时游戏自动收录 DustyTome 的 [SavedProperty]。
		HextechSavedPropertyBootstrap.InjectModelType(typeof(DustyTome));
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

	private static DustyTome CreateTestDustyTome()
	{
		DustyTome dustyTome = CreateBareTestDustyTome();
		SetTestDustyTomeAncientCard(dustyTome, ModelDb.GetId<Apotheosis>());
		return dustyTome;
	}

	private static DustyTome CreateBareTestDustyTome()
	{
		DustyTome dustyTome = new();
		typeof(AbstractModel)
			.GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(dustyTome, true);
		return dustyTome;
	}

	private static void SetTestDustyTomeAncientCard(DustyTome dustyTome, ModelId ancientCard)
	{
		typeof(DustyTome)
			.GetField("_ancientCard", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(dustyTome, (ModelId?)ancientCard);
	}

	private static void PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries()
	{
		HextechMayhemCombatTrackingState tracking = new();
		tracking.EnemyPorcupineTemporaryThornsThisTurn[101] = 2;
		tracking.EnemyPorcupineTemporaryThornsThisTurn[102] = 0;
		tracking.EnemyPorcupineTemporaryThornsThisTurn[103] = -1;

		IReadOnlyList<(uint CombatId, int Thorns)> removal = PorcupineEnemyHex.GetTemporaryThornsToRemove(tracking);

		Equal(1, removal.Count, "porcupine temporary thorns removal count");
		Equal(101u, removal[0].CombatId, "porcupine temporary thorns removal target");
		Equal(2, removal[0].Thorns, "porcupine temporary thorns removal amount");
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

	private static void ExternalPlayerRuneRegistrationUpdatesCatalog()
	{
		Type runeType = typeof(ExternalRegistrationTestRune);
		Expect(!HextechCatalog.IsPlayerRuneTypeVisible(runeType), "external rune should not be visible before registration");
		HextechRunesApi.RegisterPlayerRune<ExternalRegistrationTestRune>(
			HextechRarityTier.Gold,
			tagKey: "COMPREHENSIVE",
			assetModId: "HextechRunes.Tests");
		Expect(HextechCatalog.IsPlayerRuneTypeVisible(runeType), "external rune should be visible after registration");
		Expect(HextechCatalog.IsPlayerRuneTypeConfigurable(runeType), "external rune should be configurable after registration");
		Expect(HextechCatalog.IsPlayerRuneTypeSelectable(runeType), "external rune should be selectable after registration");
		Expect(HextechCatalog.GetPlayerRuneTypesForRarity(HextechRarityTier.Gold).Contains(runeType), "external rune should enter rarity pool");
		Expect(HextechCatalog.GetAllConfigurableRuneTypes().Contains(runeType), "external rune should enter configurable rune type pool");
		Expect(HextechCatalog.GetConfigurablePlayerRuneIds().Contains(ModelDb.GetId(runeType)), "external rune should enter configurable rune id pool");
	}

	private static void ExternalEventRelicRegistrationUpdatesRegistry()
	{
		Type relicType = typeof(ExternalRegistrationEventRelic);
		Expect(!HextechContentRegistry.EventRelicTypes.Contains(relicType), "external event relic should not be registered initially");
		HextechRunesApi.RegisterEventRelic<ExternalRegistrationEventRelic>("HextechRunes.Tests");
		Expect(HextechContentRegistry.EventRelicTypes.Contains(relicType), "external event relic should be registered");
	}

	private static void ExternalForgeRegistrationUpdatesCatalog()
	{
		Type forgeType = typeof(ExternalRegistrationForge);
		Expect(!HextechContentRegistry.AllForgeTypes.Contains(forgeType), "external forge should not be registered initially");
		HextechRunesApi.RegisterForge<ExternalRegistrationForge>(HextechRarityTier.Prismatic, "HextechRunes.Tests");
		Expect(HextechContentRegistry.AllForgeTypes.Contains(forgeType), "external forge should enter all forge types");
		Expect(HextechContentRegistry.PrismaticForgeTypes.Contains(forgeType), "external forge should enter prismatic pool");
		Expect(HextechCatalog.GetForgeTypesForRarity(HextechRarityTier.Prismatic).Contains(forgeType), "external forge should enter catalog rarity pool");
		string forgeId = ModelDb.GetId(forgeType).Entry;
		Expect(HextechRuneConfiguration.NormalizeDisabledForgeIds([ forgeId ]).Contains(forgeId), "external forge should be accepted by disabled forge config");
	}

	private static void ExternalConfigDisabledIdsPreserveUnloadedContent()
	{
		const string unloadedRuneId = "ExternalMod.UnloadedRune";
		const string unloadedForgeId = "ExternalMod.UnloadedForge";

		SetEqual(
			[ unloadedRuneId ],
			HextechPlayerRuneConfigIds.Normalize([ unloadedRuneId, unloadedRuneId, " " ]),
			"unloaded external rune disabled id should be preserved");
		SetEqual(
			[ unloadedForgeId ],
			HextechRuneConfiguration.NormalizeDisabledForgeIds([ unloadedForgeId, unloadedForgeId, " " ]),
			"unloaded external forge disabled id should be preserved");
	}

	private static void ExternalEnchantmentIconRegistrationTracksPath()
	{
		ModelId id = ModelDb.GetId<ExternalRegistrationEnchantment>();
		const string iconPath = "res://HextechRunes.Tests/images/enchantments/externalRegistrationEnchantment.png";
		Expect(HextechExternalContentRegistry.GetEnchantmentIconPath(id) == null, "external enchantment icon should not be registered initially");
		HextechRunesApi.RegisterEnchantmentIcon<ExternalRegistrationEnchantment>(iconPath);
		Equal(iconPath, HextechExternalContentRegistry.GetEnchantmentIconPath(id), "external enchantment icon path");

		SavedProperties? props = SavedProperties.FromInternal(new ExternalRegistrationEnchantment(), id);
		Expect(
			props?.ints?.Any(static property => property.name == "PersistentCounter" && property.value == 7) == true,
			"external enchantment saved property should be registered");
	}

	private static void IllusoryWeaponPenNibPrefixesCanReturnSkippedTask()
	{
		AssertHarmonyTaskPrefixCanReturnSkippedTask("PenNibBeforeCardPlayedPrefix");
		AssertHarmonyTaskPrefixCanReturnSkippedTask("PenNibAfterCardPlayedPrefix");
	}

	private static void AttackCommandCompatibilityRestoresNullExecuteResult()
	{
		AttackCommand command = new(1m);
		AttackCommand result = HextechCombatHooks.EnsureAttackCommandExecuteResult(Task.FromResult<AttackCommand>(null!), command).GetAwaiter().GetResult();
		Expect(ReferenceEquals(command, result), "null AttackCommand.Execute result should fall back to command instance");

		AttackCommand completed = HextechCombatHooks.EnsureAttackCommandExecuteResult(Task.FromResult(command), new AttackCommand(2m)).GetAwaiter().GetResult();
		Expect(ReferenceEquals(command, completed), "non-null AttackCommand.Execute result should be preserved");
	}

	private static void MultiplayerGameplaySignatureExcludesRuntimeSavedProperties()
	{
		string gameplaySignature = HextechMultiplayerCompatibilityHooks.BuildModNetworkSignature(
			"HextechRunes",
			"0.8.1",
			null,
			"",
			"",
			includeSavedProperties: false);
		string diagnosticSignature = HextechMultiplayerCompatibilityHooks.BuildModNetworkSignature(
			"HextechRunes",
			"0.8.1",
			null,
			"",
			"",
			includeSavedProperties: true);

		Expect(!gameplaySignature.Contains("savedProps=", StringComparison.Ordinal), "gameplay signature must not include runtime SavedProperties state");
		Expect(diagnosticSignature.Contains("savedProps=", StringComparison.Ordinal), "diagnostic signature should still include SavedProperties state");
		Expect(!string.Equals(gameplaySignature, diagnosticSignature, StringComparison.Ordinal), "diagnostic signature should remain more detailed than gameplay signature");
	}

	private static void SavedPropertyNetIdCanonicalizationIsInjectionOrderIndependent()
	{
		IReadOnlySet<string> vanilla = new HashSet<string>(StringComparer.Ordinal) { "V0", "V1", "V2" };

		// 同一组模组属性名,但两端注入顺序不同(模拟不同的本地模组加载顺序)。
		List<string> mapClientA = ["V0", "V1", "V2", "Zebra", "Apple", "SavedTriggeredThisTurn", "Mango"];
		List<string> mapClientB = ["V0", "V1", "V2", "SavedTriggeredThisTurn", "Mango", "Apple", "Zebra"];

		List<string>? canonicalA = HextechSavedPropertyNetIdCanonicalizer.Canonicalize(mapClientA, vanilla);
		List<string>? canonicalB = HextechSavedPropertyNetIdCanonicalizer.Canonicalize(mapClientB, vanilla);

		Expect(canonicalA != null && canonicalB != null, "canonicalization should succeed for valid input");
		Equal(string.Join(",", canonicalA!), string.Join(",", canonicalB!), "two clients with the same mod set must produce identical net-id layout regardless of injection order");

		// 原版前缀按原顺序保留(net-id 0..2 不变)。
		Equal("V0,V1,V2", string.Join(",", canonicalA!.Take(3)), "vanilla prefix preserved in original order");
		// 模组后缀按序数排序。
		Equal("Apple,Mango,SavedTriggeredThisTurn,Zebra", string.Join(",", canonicalA!.Skip(3)), "modded suffix is ordinal-sorted");
		// 条目总数不变(位宽不会因规范化漂移)。
		Equal(mapClientA.Count, canonicalA!.Count, "canonicalization preserves entry count");

		// 非法输入(原版集合缺失)应放弃改写。
		Expect(HextechSavedPropertyNetIdCanonicalizer.Canonicalize(mapClientA, null) == null, "null vanilla set should abort canonicalization");
		Expect(HextechSavedPropertyNetIdCanonicalizer.Canonicalize(null, vanilla) == null, "null map should abort canonicalization");
	}

	private static void SavedPropertyNetIdBitSizeMatchesGameFormula()
	{
		// 必须与游戏 / RitsuLib 的 CeilToInt(Log2(count)) 完全一致。
		Equal(0, HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(1), "count=1 -> 0 bits (matches game)");
		Equal(1, HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(2), "count=2 -> 1 bit");
		Equal(3, HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(7), "count=7 -> ceil(log2 7)=3 bits");
		Equal(7, HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(128), "count=128 -> 7 bits");
		Equal(8, HextechSavedPropertyNetIdCanonicalizer.ComputeNetIdBitSize(129), "count=129 -> 8 bits");
	}

	private static void AssertHarmonyTaskPrefixCanReturnSkippedTask(string methodName)
	{
		MethodInfo? method = typeof(HextechPlayerRuneHooks).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
		if (method == null)
		{
			throw new InvalidOperationException($"{methodName} should exist");
		}

		ParameterInfo? resultParameter = method.GetParameters().SingleOrDefault(static parameter => parameter.Name == "__result");
		if (resultParameter == null)
		{
			throw new InvalidOperationException($"{methodName} should expose Harmony __result");
		}

		Equal(typeof(Task).MakeByRefType(), resultParameter.ParameterType, $"{methodName} __result type");
	}

	private sealed class ExternalRegistrationTestRune : HextechRelicBase
	{
	}

	private sealed class ExternalRegistrationEventRelic : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private sealed class ExternalRegistrationForge : HextechForgeBase
	{
	}

	private sealed class ExternalRegistrationEnchantment : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int PersistentCounter { get; set; } = 7;
	}

	private sealed class RuneSelectionTestRelicA : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private sealed class RuneSelectionTestRelicB : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private sealed class RuneSelectionTestRelicC : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private static (HextechRarityTier Rarity, IReadOnlyList<MonsterHexKind> Pool) GetMonsterHexPoolWithMinimum(int minimumCount)
	{
		foreach (HextechRarityTier rarity in Enum.GetValues<HextechRarityTier>())
		{
			IReadOnlyList<MonsterHexKind> pool = MonsterHexCatalog.GetMonsterHexesForRarity(rarity);
			if (pool.Count >= minimumCount)
			{
				return (rarity, pool);
			}
		}

		throw new InvalidOperationException($"no monster hex rarity pool has at least {minimumCount} entries");
	}

	private static RelicModel[] CreateRuneSelectionTestOptions(int count)
	{
		RelicModel[] options =
		[
			new RuneSelectionTestRelicA(),
			new RuneSelectionTestRelicB(),
			new RuneSelectionTestRelicC()
		];
		return options.Take(count).ToArray();
	}

	private static ModelId TestMonsterHexIconId(MonsterHexKind kind)
	{
		return new ModelId("HEXTECH_TEST", $"MONSTER_HEX_{(int)kind}");
	}

	// (PR#18)Player 的构造函数会触达 SaveManager/PlatformUtil 等只在真实 Godot 运行时下可用的原生绑定,
	// 纯 CLI 测试进程里直接 new 会段错误。这里只需要一个能承载稳定 NetId 的壳子来复用
	// GetPlayerRuneProcKey 的联机计费键,故跳过构造函数,直接反射写入 NetId 的自动属性支持字段。
	private static Player CreateOrdinalTestPlayer(ulong netId)
	{
		Player player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		typeof(Player)
			.GetField("<NetId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
			.SetValue(player, netId);
		return player;
	}

	private static void Expect(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}

	private static void ExpectThrows<TException>(Action action, string message)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException)
		{
			return;
		}

		throw new InvalidOperationException(message);
	}

	private static void Equal<T>(T expected, T actual, string label)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
		}
	}

	private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
	{
		T[] expectedArray = expected.ToArray();
		T[] actualArray = actual.ToArray();
		if (!expectedArray.SequenceEqual(actualArray))
		{
			throw new InvalidOperationException($"{label}: expected [{string.Join(", ", expectedArray)}], got [{string.Join(", ", actualArray)}]");
		}
	}

	private static void SetEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
	{
		HashSet<T> expectedSet = expected.ToHashSet();
		HashSet<T> actualSet = actual.ToHashSet();
		if (!expectedSet.SetEquals(actualSet))
		{
			throw new InvalidOperationException($"{label}: expected [{string.Join(", ", expectedSet)}], got [{string.Join(", ", actualSet)}]");
		}
	}

	private static bool IsModuloStepCycle(IReadOnlyList<int> values, int modulo)
	{
		if (values.Count < 3)
		{
			return false;
		}

		int step = PositiveModulo(values[1] - values[0], modulo);
		for (int i = 2; i < values.Count; i++)
		{
			if (PositiveModulo(values[i] - values[i - 1], modulo) != step)
			{
				return false;
			}
		}

		return true;
	}

	private static int PositiveModulo(int value, int modulo)
	{
		int result = value % modulo;
		return result < 0 ? result + modulo : result;
	}

	private readonly record struct TestCase(string Name, Action Run);
}
