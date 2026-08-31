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
	private const int Magic = 0x48585452; // HXTR
	private const int ChoiceKindActRoll = 1;
	private const int ChoiceKindRuneSelection = 2;
	private const int ChoiceKindActSelectionApplied = 3;
	private const int ChoiceKindEnemyHexAdjustment = 4;
	private const int ChoiceKindForgeSelection = 5;
	private const int ChoiceKindRandomRuneGrant = 6;
	private const int ChoiceKindRelicOptionSelection = 7;
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
			new(nameof(EnemyHexAdjustmentRejectsUnexpectedSequence), EnemyHexAdjustmentRejectsUnexpectedSequence),
			new(nameof(EnemyHexAdjustmentRejectsExtremeCounts), EnemyHexAdjustmentRejectsExtremeCounts),
			new(nameof(LegacyEnemyHexAdjustmentIsRejected), LegacyEnemyHexAdjustmentIsRejected),
			new(nameof(RandomRuneGrantRoundTripKeepsStableModelIds), RandomRuneGrantRoundTripKeepsStableModelIds),
			new(nameof(RandomRuneGrantRejectsMalformedStableModelIdList), RandomRuneGrantRejectsMalformedStableModelIdList),
			new(nameof(RelicOptionSelectionRoundTripRequiresMatchingOptions), RelicOptionSelectionRoundTripRequiresMatchingOptions),
			new(nameof(OperationTokensRejectCrossedPayloads), OperationTokensRejectCrossedPayloads),
			new(nameof(StableModelIdListCodecRoundTripsFromNonzeroCursor), StableModelIdListCodecRoundTripsFromNonzeroCursor),
			new(nameof(StableModelIdListCodecRejectsMalformedLength), StableModelIdListCodecRejectsMalformedLength),
			new(nameof(StableModelIdListCodecRejectsEncoderOverflow), StableModelIdListCodecRejectsEncoderOverflow),
			new(nameof(PlayerRuneRarityConfigExcludesFullyDisabledTier), PlayerRuneRarityConfigExcludesFullyDisabledTier),
			new(nameof(PlayerRuneRarityConfigFallsBackWhenAllTiersDisabled), PlayerRuneRarityConfigFallsBackWhenAllTiersDisabled),
			new(nameof(FlyingKickDisableSurvivesNormalizationAndStrictPoolFiltering), FlyingKickDisableSurvivesNormalizationAndStrictPoolFiltering),
			new(nameof(RarityRollResolverFiltersWeightedRarities), RarityRollResolverFiltersWeightedRarities),
			new(nameof(RarityRollResolverUsesOrderedUniformFallback), RarityRollResolverUsesOrderedUniformFallback),
			new(nameof(ConsecutiveSilverRuleExcludesSilverFromEveryLaterAct), ConsecutiveSilverRuleExcludesSilverFromEveryLaterAct),
			new(nameof(GoldenRerollOnlyUpgradesSilverAndGold), GoldenRerollOnlyUpgradesSilverAndGold),
			new(nameof(GoldenRerollUsesExactFivePercentWindow), GoldenRerollUsesExactFivePercentWindow),
			new(nameof(GoldenRerollSeparatesPlayersAndKeepsConsoleLocal), GoldenRerollSeparatesPlayersAndKeepsConsoleLocal),
			new(nameof(GoldenRerollDebugForceIsOneShot), GoldenRerollDebugForceIsOneShot),
			new(nameof(GoldenRerollVisualKeepsAnimatingWhileOverlayIsPaused), GoldenRerollVisualKeepsAnimatingWhileOverlayIsPaused),
			new(nameof(GoldenRerollCardThemeFollowsRerolledRuneRarity), GoldenRerollCardThemeFollowsRerolledRuneRarity),
			new(nameof(WeightedIndexBoundarySelection), WeightedIndexBoundarySelection),
			new(nameof(RuneSelectionCandidateConstraintsReserveCharacterAndLimitUpgrades), RuneSelectionCandidateConstraintsReserveCharacterAndLimitUpgrades),
			new(nameof(UnconfirmedRuneSelectionCancelsInsteadOfDefaultingToFirstOption), UnconfirmedRuneSelectionCancelsInsteadOfDefaultingToFirstOption),
			new(nameof(SelectionUiWaitsForControllerInputBeforeFocusing), SelectionUiWaitsForControllerInputBeforeFocusing),
			new(nameof(EnemyHexRerollPlaysRerollSound), EnemyHexRerollPlaysRerollSound),
			new(nameof(EnemyHexRemovalCanBeUndoneWithoutConsumingTheSlot), EnemyHexRemovalCanBeUndoneWithoutConsumingTheSlot),
			new(nameof(EnemyHexActionButtonsUseTexturesWithoutTooltipText), EnemyHexActionButtonsUseTexturesWithoutTooltipText),
			new(nameof(CollapsedEnemyHexPanelFollowsTopBarButtonLifecycle), CollapsedEnemyHexPanelFollowsTopBarButtonLifecycle),
			new(nameof(DestructivePickupRunesAreExcludedFromRandomRewards), DestructivePickupRunesAreExcludedFromRandomRewards),
			new(nameof(StarterUpgradeCapsTerminateExternalUpgradeToMaxLoops), StarterUpgradeCapsTerminateExternalUpgradeToMaxLoops),
			new(nameof(SearingAttackRuneGrantsUpgradedCard), SearingAttackRuneGrantsUpgradedCard),
			new(nameof(CardUpgradePickupAndAvailabilityRules), CardUpgradePickupAndAvailabilityRules),
			new(nameof(BashUpgradeStrengthMatchesVulnerableApplied), BashUpgradeStrengthMatchesVulnerableApplied),
			new(nameof(CreativeAiUpgradeRuneUpgradesGeneratedPowerCards), CreativeAiUpgradeRuneUpgradesGeneratedPowerCards),
			new(nameof(SubroutineUpgradeCombatMoveGateResetsAcrossCombats), SubroutineUpgradeCombatMoveGateResetsAcrossCombats),
			new(nameof(PactsEndUpgradeDamageScalesWithExhaustPile), PactsEndUpgradeDamageScalesWithExhaustPile),
			new(nameof(BrandUpgradeDamageScalesWithPermanentPlayCount), BrandUpgradeDamageScalesWithPermanentPlayCount),
			new(nameof(BigHammerForgeBonusAvoidsHammerTimeDoubleScaling), BigHammerForgeBonusAvoidsHammerTimeDoubleScaling),
			new(nameof(HundredRefinementsRequiresTwoBodyForges), HundredRefinementsRequiresTwoBodyForges),
			new(nameof(InitialForgeGrantRunesPersistPendingTransaction), InitialForgeGrantRunesPersistPendingTransaction),
			new(nameof(InitialForgeGrantLoadRecoveryPrecedesActRecovery), InitialForgeGrantLoadRecoveryPrecedesActRecovery),
			new(nameof(HappyAccidentUsesExhaustedStatusesAtTurnStart), HappyAccidentUsesExhaustedStatusesAtTurnStart),
			new(nameof(HastyScribbleDrawsToFullHandAtTurnStart), HastyScribbleDrawsToFullHandAtTurnStart),
			new(nameof(BigHandsIncreasesSummonAmountByFiftyPercent), BigHandsIncreasesSummonAmountByFiftyPercent),
			new(nameof(SpinToWinRecognizesSupportedDelayedResources), SpinToWinRecognizesSupportedDelayedResources),
			new(nameof(NewCardUpgradeRunesUseExpectedTriggerRules), NewCardUpgradeRunesUseExpectedTriggerRules),
			new(nameof(HiddenGemUpgradeMovesNewReplayTargetToHand), HiddenGemUpgradeMovesNewReplayTargetToHand),
			new(nameof(PlayerSustainRunesUseExpectedMaxHpRules), PlayerSustainRunesUseExpectedMaxHpRules),
			new(nameof(CollectorUsesStrictExecuteThresholdAndSharesFlyingKickExecutions), CollectorUsesStrictExecuteThresholdAndSharesFlyingKickExecutions),
			new(nameof(NewRuneHookTargetsMatchSupportedGameApis), NewRuneHookTargetsMatchSupportedGameApis),
			new(nameof(FormVfxSafetySkipsMissingHolder), FormVfxSafetySkipsMissingHolder),
			new(nameof(SymphonyOfWarPreservesDemonAndSerpentFormVfx), SymphonyOfWarPreservesDemonAndSerpentFormVfx),
			new(nameof(FormAutoPlayBatchDispatchesOneCardPlayEvent), FormAutoPlayBatchDispatchesOneCardPlayEvent),
			new(nameof(FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay), FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay),
			new(nameof(FormAutoPlayBatchUsesOnePreparedFinalEffect), FormAutoPlayBatchUsesOnePreparedFinalEffect),
			new(nameof(FormAutoPlayBatchCombinesOnlyEffectNeutralEnchantments), FormAutoPlayBatchCombinesOnlyEffectNeutralEnchantments),
			new(nameof(DrawYourSwordReplacesOrbEvokeWithTwoFocus), DrawYourSwordReplacesOrbEvokeWithTwoFocus),
			new(nameof(EnemyOmniDragonSoulUsesPlayerTurnStart), EnemyOmniDragonSoulUsesPlayerTurnStart),
			new(nameof(FortuneForgeRewardScalesByStacks), FortuneForgeRewardScalesByStacks),
			new(nameof(PrismaticEggIsExcludedFromThirdAct), PrismaticEggIsExcludedFromThirdAct),
			new(nameof(MirrorReflectionCopiesCursesButNotBasicCards), MirrorReflectionCopiesCursesButNotBasicCards),
			new(nameof(DrainTargetsFirstEnemyWithHighestCurrentHp), DrainTargetsFirstEnemyWithHighestCurrentHp),
			new(nameof(FeyMagicUsesThreeCostWithoutTurnLimit), FeyMagicUsesThreeCostWithoutTurnLimit),
			new(nameof(GiantSlayerScalesFromEnemyMaxHp), GiantSlayerScalesFromEnemyMaxHp),
			new(nameof(SomethingForNothingDrawsAtZeroAndDiscountsFirstPaidCard), SomethingForNothingDrawsAtZeroAndDiscountsFirstPaidCard),
			new(nameof(MagicMissileUsesThreeTwoPercentHits), MagicMissileUsesThreeTwoPercentHits),
			new(nameof(EchoAddsItsCopyWithoutRecursingThroughGenerationHooks), EchoAddsItsCopyWithoutRecursingThroughGenerationHooks),
			new(nameof(TwinFlamesUsesTwoEnergyScaledHits), TwinFlamesUsesTwoEnergyScaledHits),
			new(nameof(TwinFlamesKeepsMultiplayerDamageInsideCardAction), TwinFlamesKeepsMultiplayerDamageInsideCardAction),
			new(nameof(LightEmUpUsesFiveEnergyScaledTwinFlameMissiles), LightEmUpUsesFiveEnergyScaledTwinFlameMissiles),
			new(nameof(ProjectileRunesKeepMultiplayerDamageInsideCardAction), ProjectileRunesKeepMultiplayerDamageInsideCardAction),
			new(nameof(PiercingThreadSplitsOneDamageEventBeforeBlock), PiercingThreadSplitsOneDamageEventBeforeBlock),
			new(nameof(DualcastUpgradeReturnsBothCastCardsToHand), DualcastUpgradeReturnsBothCastCardsToHand),
			new(nameof(DeathWarrantTriggersPoisonEveryEightDraws), DeathWarrantTriggersPoisonEveryEightDraws),
			new(nameof(MadScientistOrbLayoutOnlyTweensFirstTen), MadScientistOrbLayoutOnlyTweensFirstTen),
			new(nameof(MyriadSwordsUsesShuffleTriggerInsteadOfTurnEnd), MyriadSwordsUsesShuffleTriggerInsteadOfTurnEnd),
			new(nameof(MyriadSwordsExplicitlyClosesAStalePlayPile), MyriadSwordsExplicitlyClosesAStalePlayPile),
			new(nameof(SovereignBladeVfxSyncUsesVanillaForgeScale), SovereignBladeVfxSyncUsesVanillaForgeScale),
			new(nameof(SlowCookVfxUsesDedicatedPressureCookerTextures), SlowCookVfxUsesDedicatedPressureCookerTextures),
			new(nameof(AssetResolverPrefersRawTextureBeforePackedResource), AssetResolverPrefersRawTextureBeforePackedResource),
			new(nameof(AssetResolverRecognizesRawImagePaths), AssetResolverRecognizesRawImagePaths),
			new(nameof(AssetResolverRejectsInvalidTextureObjects), AssetResolverRejectsInvalidTextureObjects),
			new(nameof(AssetResolverPropagatesLoaderExceptionsWithoutCachingDirtyEntries), AssetResolverPropagatesLoaderExceptionsWithoutCachingDirtyEntries),
			new(nameof(AssetResolverRawOnlyMissReturnsNullWithoutCaching), AssetResolverRawOnlyMissReturnsNullWithoutCaching),
			new(nameof(CoefficientRunesStackAdditivelyWithinTheirOwnSector), CoefficientRunesStackAdditivelyWithinTheirOwnSector),
			new(nameof(CoefficientForgesShareOneAdditiveSector), CoefficientForgesShareOneAdditiveSector),
			new(nameof(MaxHpCoefficientSectorsMultiply), MaxHpCoefficientSectorsMultiply),
			new(nameof(EnemyCoefficientAddsWithinHexAndMultipliesAcrossHexes), EnemyCoefficientAddsWithinHexAndMultipliesAcrossHexes),
			new(nameof(EnemyMaxHpCoefficientSectorsUseBaseHp), EnemyMaxHpCoefficientSectorsUseBaseHp),
			new(nameof(EnemyMaxHpLegacyMigrationRecoversMixedSinglePlayerEffects), EnemyMaxHpLegacyMigrationRecoversMixedSinglePlayerEffects),
			new(nameof(EnemyMaxHpLegacyMigrationPreservesMultiplayerScaling), EnemyMaxHpLegacyMigrationPreservesMultiplayerScaling),
			new(nameof(NightmareHooksEveryDarkOrbPassiveTrigger), NightmareHooksEveryDarkOrbPassiveTrigger),
			new(nameof(NightmareEffectRunsOnceAfterEachPassiveTask), NightmareEffectRunsOnceAfterEachPassiveTask),
			new(nameof(DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune), DiceManiacForgeRarityModifierKeepsDefaultWeightsWithoutRune),
			new(nameof(DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights), DiceManiacForgeRarityModifierDoublesGoldAndPrismaticWeights),
			new(nameof(StableRandomPlayerIdentityUsesNetIdBeforeLocalSlot), StableRandomPlayerIdentityUsesNetIdBeforeLocalSlot),
			new(nameof(StableRandomSequentialFloorsAvoidExcessClustering), StableRandomSequentialFloorsAvoidExcessClustering),
			new(nameof(StableRandomPowerOfTwoIndexesAvoidTerminalCounterCycle), StableRandomPowerOfTwoIndexesAvoidTerminalCounterCycle),
			new(nameof(ColorDiscoveryCandidateOrderIsPermutationInvariant), ColorDiscoveryCandidateOrderIsPermutationInvariant),
			new(nameof(RandomForgeShopRelicUpdatesDisplayedPrice), RandomForgeShopRelicUpdatesDisplayedPrice),
			new(nameof(ActSelectionGatePreventsReentryAndClearsCurrentRun), ActSelectionGatePreventsReentryAndClearsCurrentRun),
			new(nameof(ActSelectionGateClearsStaleRun), ActSelectionGateClearsStaleRun),
			new(nameof(RetiredCustomRarityModifiersAreNotInstalledIntoCustomRunUi), RetiredCustomRarityModifiersAreNotInstalledIntoCustomRunUi),
			new(nameof(StuffedToRuinChallengeUsesThreeFixedActPlans), StuffedToRuinChallengeUsesThreeFixedActPlans),
			new(nameof(DefenseCounterMasterChallengeUsesThreeFixedActPlans), DefenseCounterMasterChallengeUsesThreeFixedActPlans),
			new(nameof(BruteForceChallengeUsesThreeFixedActPlans), BruteForceChallengeUsesThreeFixedActPlans),
			new(nameof(EightPennyGateChallengeUsesThreeFixedActPlans), EightPennyGateChallengeUsesThreeFixedActPlans),
			new(nameof(ListlessChallengeUsesThreeFixedActPlans), ListlessChallengeUsesThreeFixedActPlans),
			new(nameof(PresetChallengesArePairwiseMutuallyExclusive), PresetChallengesArePairwiseMutuallyExclusive),
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
				new(nameof(HungryExhaustsZeroOneOrTwoCardsByTier), HungryExhaustsZeroOneOrTwoCardsByTier),
				new(nameof(InspectBlocksOnlyTheConfiguredExtraDrawTriggers), InspectBlocksOnlyTheConfiguredExtraDrawTriggers),
				new(nameof(GripConsumesOnlyTheFirstManualCardTrigger), GripConsumesOnlyTheFirstManualCardTrigger),
				new(nameof(HungryInspectAndGripShareEightPennyGateTexture), HungryInspectAndGripShareEightPennyGateTexture),
				new(nameof(CombatTrackingGlobalProcOrdinalsSerializeAndReset), CombatTrackingGlobalProcOrdinalsSerializeAndReset),
				new(nameof(CombatTrackingPlayerRuneProcOrdinalPeekDoesNotConsume), CombatTrackingPlayerRuneProcOrdinalPeekDoesNotConsume),
			new(nameof(CombatTrackingSerializationIsCultureInvariant), CombatTrackingSerializationIsCultureInvariant),
			new(nameof(SavedPropertyManifestMatchesCheckedInList), SavedPropertyManifestMatchesCheckedInList),
			new(nameof(SavedPropertyPreInitRegistrationLeavesWireTablesUntouched), SavedPropertyPreInitRegistrationLeavesWireTablesUntouched),
			new(nameof(SavedPropertyLateCarrierRegistrationFailsClosed), SavedPropertyLateCarrierRegistrationFailsClosed),
			new(nameof(SavedPropertySameNameCarrierStillRequiresPerTypeCache), SavedPropertySameNameCarrierStillRequiresPerTypeCache),
			new(nameof(SavedPropertyLateExternalRegistrationLeavesNoPartialState), SavedPropertyLateExternalRegistrationLeavesNoPartialState),
			new(nameof(ConfigMigrationForceResetsBelowV15), ConfigMigrationForceResetsBelowV15),
			new(nameof(ConfigMigrationV15BaselineReachesCurrentDefault), ConfigMigrationV15BaselineReachesCurrentDefault),
			new(nameof(ConfigMigrationV26AddsNewPlayerDefaultDisables), ConfigMigrationV26AddsNewPlayerDefaultDisables),
			new(nameof(ConfigMigrationV27KeepsNormalWeightsAndEnablesConsecutiveSilverPrevention), ConfigMigrationV27KeepsNormalWeightsAndEnablesConsecutiveSilverPrevention),
			new(nameof(ConfigMigrationV30EnablesAdvanceToRetreat), ConfigMigrationV30EnablesAdvanceToRetreat),
			new(nameof(ConfigMigrationV31EnablesHappyAccident), ConfigMigrationV31EnablesHappyAccident),
			new(nameof(ConfigMigrationV33ChangesLegacyInfiniteMonsterRerolls), ConfigMigrationV33ChangesLegacyInfiniteMonsterRerolls),
			new(nameof(ConfigMigrationCurrentVersionPreservesCustomDisabledIds), ConfigMigrationCurrentVersionPreservesCustomDisabledIds),
			new(nameof(ConfigShareRoundTripKeepsActRarityWeights), ConfigShareRoundTripKeepsActRarityWeights),
			new(nameof(MayhemRunContextResetForNewRunClearsState), MayhemRunContextResetForNewRunClearsState),
			new(nameof(RuneSelectionJournalRoundTripsInStableOrder), RuneSelectionJournalRoundTripsInStableOrder),
			new(nameof(RuneSelectionJournalRejectsConflictingSelections), RuneSelectionJournalRejectsConflictingSelections),
			new(nameof(AppliedRuneSelectionJournalDoesNotRequireInventoryPresence), AppliedRuneSelectionJournalDoesNotRequireInventoryPresence),
			new(nameof(MayhemRunContextResetForEndlessLoopPreservesStageRows), MayhemRunContextResetForEndlessLoopPreservesStageRows),
			new(nameof(MayhemActStateSupportsExtraActsAndStableExtraStageIds), MayhemActStateSupportsExtraActsAndStableExtraStageIds),
			new(nameof(MayhemRunContextDebugResetSetsOnlyRequestedMonsterHex), MayhemRunContextDebugResetSetsOnlyRequestedMonsterHex),
			new(nameof(PlayerRuneMetadataHasUniqueTypes), PlayerRuneMetadataHasUniqueTypes),
			new(nameof(PlayerRuneMetadataMatchesContentRegistrySlices), PlayerRuneMetadataMatchesContentRegistrySlices),
			new(nameof(PlayerRuneMetadataPreservesCharacterOrder), PlayerRuneMetadataPreservesCharacterOrder),
			new(nameof(PlayerRuneMetadataClassifiesConfigStates), PlayerRuneMetadataClassifiesConfigStates),
			new(nameof(PlayerRuneMetadataCatalogOutputsMatchCatalogQueries), PlayerRuneMetadataCatalogOutputsMatchCatalogQueries),
			new(nameof(PlayerRuneMetadataFallbacksAreStable), PlayerRuneMetadataFallbacksAreStable),
			new(nameof(ForgeMetadataHasUniqueTypes), ForgeMetadataHasUniqueTypes),
			new(nameof(ForgeMetadataMatchesContentRegistrySlices), ForgeMetadataMatchesContentRegistrySlices),
			new(nameof(ForgeMetadataFallbacksAreStable), ForgeMetadataFallbacksAreStable),
			new(nameof(MonsterHexMetadataHasUniqueKinds), MonsterHexMetadataHasUniqueKinds),
			new(nameof(MonsterHexMetadataMatchesContentRegistrySlices), MonsterHexMetadataMatchesContentRegistrySlices),
			new(nameof(MonsterHexMetadataKeepsDisabledKindsOutOfRarityPools), MonsterHexMetadataKeepsDisabledKindsOutOfRarityPools),
			new(nameof(NewEnemyHexesReusePlayerRuneIconsAndRarities), NewEnemyHexesReusePlayerRuneIconsAndRarities),
			new(nameof(EnemyHexHoverTipsUseExpectedPowerModels), EnemyHexHoverTipsUseExpectedPowerModels),
			new(nameof(EnemyFossilStalkerUsesExpectedSuckTiers), EnemyFossilStalkerUsesExpectedSuckTiers),
			new(nameof(EnemyTungstenRodReducesEachHpLossByTier), EnemyTungstenRodReducesEachHpLossByTier),
			new(nameof(EnemySlowHexesUseExpectedBaselinesAndTiers), EnemySlowHexesUseExpectedBaselinesAndTiers),
			new(nameof(EnemyOpeningBuffHexesUseDedicatedReplayableHook), EnemyOpeningBuffHexesUseDedicatedReplayableHook),
			new(nameof(EnemyCorrosionAppliesFrailOnEveryUnblockedPlayerHit), EnemyCorrosionAppliesFrailOnEveryUnblockedPlayerHit),
			new(nameof(EnemyHeavyHitterScalesDamageEveryFifteenMaxHp), EnemyHeavyHitterScalesDamageEveryFifteenMaxHp),
			new(nameof(EnemyVitalitySurgeScalesAllSustainFromMaxHp), EnemyVitalitySurgeScalesAllSustainFromMaxHp),
			new(nameof(EnemyTwilightVeilMirrorsOnlyPositivePlayerBlock), EnemyTwilightVeilMirrorsOnlyPositivePlayerBlock),
			new(nameof(EnemyAttributeBoostsUseExpectedTiersAndCrossHexMultiplication), EnemyAttributeBoostsUseExpectedTiersAndCrossHexMultiplication),
			new(nameof(EnemyMiserableFateUsesMissingHpDivisors), EnemyMiserableFateUsesMissingHpDivisors),
			new(nameof(EnemyMaxHpCoefficientThresholdsScaleWithPlayerCount), EnemyMaxHpCoefficientThresholdsScaleWithPlayerCount),
			new(nameof(EnemyCuttingEdgeAlchemistHalvesSuccessfulPotionRolls), EnemyCuttingEdgeAlchemistHalvesSuccessfulPotionRolls),
			new(nameof(EnemyJeweledGauntletUsesExpectedStrengthTierChances), EnemyJeweledGauntletUsesExpectedStrengthTierChances),
			new(nameof(EnemyJeweledGauntletOnlyRepeatsStandardIntentTypes), EnemyJeweledGauntletOnlyRepeatsStandardIntentTypes),
			new(nameof(EnemyJeweledGauntletDuplicatesWholeIntentGroup), EnemyJeweledGauntletDuplicatesWholeIntentGroup),
			new(nameof(EnemyJeweledGauntletNeverRepeatsIntoFinalKnowledgeDemonCurse), EnemyJeweledGauntletNeverRepeatsIntoFinalKnowledgeDemonCurse),
			new(nameof(EnemyJeweledGauntletSkipsTheInsatiableOpeningMove), EnemyJeweledGauntletSkipsTheInsatiableOpeningMove),
			new(nameof(EnemyJeweledGauntletSkipsMonsterRevivalMoves), EnemyJeweledGauntletSkipsMonsterRevivalMoves),
			new(nameof(MonsterInteractionPolicyPreservesStructuralMonsterBuffs), MonsterInteractionPolicyPreservesStructuralMonsterBuffs),
			new(nameof(BuffRemovalPreservesStolenLootPowers), BuffRemovalPreservesStolenLootPowers),
			new(nameof(PersonalHiveSafetyRejectsPlayerSideCopies), PersonalHiveSafetyRejectsPlayerSideCopies),
			new(nameof(EnemyCompensationDefersHalfDamageRoundedDown), EnemyCompensationDefersHalfDamageRoundedDown),
			new(nameof(PlayerCompensationRequiresActiveCombatContext), PlayerCompensationRequiresActiveCombatContext),
			new(nameof(NextTurnDamageUsesTurnStartSnapshot), NextTurnDamageUsesTurnStartSnapshot),
			new(nameof(NextTurnDamageDoesNotRetriggerCompensation), NextTurnDamageDoesNotRetriggerCompensation),
			new(nameof(EnemyCompensationSkipsOutbreakPoisonResponse), EnemyCompensationSkipsOutbreakPoisonResponse),
			new(nameof(EnemyCompensationSkipsSleightOfFleshResponse), EnemyCompensationSkipsSleightOfFleshResponse),
			new(nameof(WatchOutGrapefruitFoodPoolHonorsCharacterAndUniqueRelics), WatchOutGrapefruitFoodPoolHonorsCharacterAndUniqueRelics),
			new(nameof(UniversalScopeChancesAddBeforeSingleRoll), UniversalScopeChancesAddBeforeSingleRoll),
			new(nameof(UniversalScopeUpgradeRestorationKeepsCapturedLevels), UniversalScopeUpgradeRestorationKeepsCapturedLevels),
			new(nameof(ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless), ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless),
			new(nameof(IllusoryWeaponPenNibPrefixesCanReturnSkippedTask), IllusoryWeaponPenNibPrefixesCanReturnSkippedTask),
			new(nameof(AttackCommandCompatibilityRestoresNullExecuteResult), AttackCommandCompatibilityRestoresNullExecuteResult),
			new(nameof(MultiplayerGameplaySignatureExcludesRuntimeSavedProperties), MultiplayerGameplaySignatureExcludesRuntimeSavedProperties),
			new(nameof(MultiplayerGameplayEntryIncludesReadableProtocolVersion), MultiplayerGameplayEntryIncludesReadableProtocolVersion),
			new(nameof(SavedPropertyNetIdCanonicalizationIsInjectionOrderIndependent), SavedPropertyNetIdCanonicalizationIsInjectionOrderIndependent),
			new(nameof(SavedPropertyNetIdBitSizeMatchesGameFormula), SavedPropertyNetIdBitSizeMatchesGameFormula),
			new(nameof(CompensationReplacementGuardScopesAsyncWork), CompensationReplacementGuardScopesAsyncWork),
			new(nameof(CompensationReplacementSuppressesSleightOfFleshResponse), CompensationReplacementSuppressesSleightOfFleshResponse),
			new(nameof(EventRewardTransactionCommitsSequentially), EventRewardTransactionCommitsSequentially),
			new(nameof(EventRewardTransactionRejectsLateRecordsAndSecondCommit), EventRewardTransactionRejectsLateRecordsAndSecondCommit),
			new(nameof(EventRewardTransactionTryRecordSkipsLateAsyncRewards), EventRewardTransactionTryRecordSkipsLateAsyncRewards),
			new(nameof(DoubleVisionCopiesTrackedCardsWhenMultiSelectEndsWithoutCompletingReward), DoubleVisionCopiesTrackedCardsWhenMultiSelectEndsWithoutCompletingReward),
			new(nameof(DoubleVisionCopiesWaxStateWithoutCopyingMeltedState), DoubleVisionCopiesWaxStateWithoutCopyingMeltedState),
			new(nameof(DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect), DoubleVisionDustyTomeSinglePlayerCopiesRelicWithoutAncientCardEffect),
			new(nameof(DoubleVisionDustyTomeSaveLoadPreservesAncientCard), DoubleVisionDustyTomeSaveLoadPreservesAncientCard),
			new(nameof(DoubleVisionDustyTomeEventMultiplayerRunsOnEveryPeerWithoutBroadcast), DoubleVisionDustyTomeEventMultiplayerRunsOnEveryPeerWithoutBroadcast),
			new(nameof(PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries), PorcupineTemporaryThornsRemovalPlanSkipsInvalidEntries),
			new(nameof(MonsterHexRollerBuildActPoolExcludesKnownAndFallsBack), MonsterHexRollerBuildActPoolExcludesKnownAndFallsBack),
			new(nameof(MonsterHexRollerResolveNewHexesPreservesPrimaryAndAvoidsDuplicates), MonsterHexRollerResolveNewHexesPreservesPrimaryAndAvoidsDuplicates),
			new(nameof(MonsterHexRollerBuildRerollPoolHonorsIconExclusionsThenFallbacks), MonsterHexRollerBuildRerollPoolHonorsIconExclusionsThenFallbacks),
			new(nameof(ExternalConfigDisabledIdsPreserveUnloadedContent), ExternalConfigDisabledIdsPreserveUnloadedContent),
			new(nameof(ExternalModelIdConflictsAreRejectedBeforeRegistration), ExternalModelIdConflictsAreRejectedBeforeRegistration),
			new(nameof(ExternalPlayerRuneRegistrationUpdatesCatalog), ExternalPlayerRuneRegistrationUpdatesCatalog),
			new(nameof(ExternalEventRelicRegistrationUpdatesRegistry), ExternalEventRelicRegistrationUpdatesRegistry),
			new(nameof(ExternalForgeRegistrationUpdatesCatalog), ExternalForgeRegistrationUpdatesCatalog),
			new(nameof(ExternalEnchantmentIconRegistrationTracksPath), ExternalEnchantmentIconRegistrationTracksPath),
			new(nameof(RepeatableEnchantmentsRequireCurrentlyOwnedEnchantmentMasterRune), RepeatableEnchantmentsRequireCurrentlyOwnedEnchantmentMasterRune),
			new(nameof(EnchantmentCompositionAdapterFindsDirectEnchantments), EnchantmentCompositionAdapterFindsDirectEnchantments),
			new(nameof(EnchantmentCompositionAdapterFindsSponsorCompositeEnchantments), EnchantmentCompositionAdapterFindsSponsorCompositeEnchantments),
			new(nameof(EnchantmentCompositionAdapterUsesMultiEnchantmentPublicApi), EnchantmentCompositionAdapterUsesMultiEnchantmentPublicApi),
			new(nameof(SponsorCompositeExpandsInnerHookListeners), SponsorCompositeExpandsInnerHookListeners),
			new(nameof(DollysMirrorRelicPagesStayWithinVanillaViewport), DollysMirrorRelicPagesStayWithinVanillaViewport),
			new(nameof(AbyssalContractChoiceModelsMapToExpectedContracts), AbyssalContractChoiceModelsMapToExpectedContracts),
			new(nameof(AbyssalContractWarriorEliteThresholdGrows), AbyssalContractWarriorEliteThresholdGrows),
			new(nameof(AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters), AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters),
			new(nameof(AbyssalContractWarriorCardFilterRejectsSkillsAndPowers), AbyssalContractWarriorCardFilterRejectsSkillsAndPowers),
			new(nameof(ActualDamageHookCannotSuppressOutOfCombatCalls), ActualDamageHookCannotSuppressOutOfCombatCalls),
			new(nameof(HookReflectionRequiresExactSignatures), HookReflectionRequiresExactSignatures),
			new(nameof(SavedPropertyProtocolClassifierMatchesOnlyOfficialShapes), SavedPropertyProtocolClassifierMatchesOnlyOfficialShapes),
			new(nameof(SavedPropertyLateRegistrationFailsClosedOn0107WithoutPartialState), SavedPropertyLateRegistrationFailsClosedOn0107WithoutPartialState),
			new(nameof(ExternalRegistrationValidationPrecedesAllSideEffects), ExternalRegistrationValidationPrecedesAllSideEffects),
			new(nameof(ExternalResourceOwnershipIsFirstWriterWinsAndIdempotent), ExternalResourceOwnershipIsFirstWriterWinsAndIdempotent),
			new(nameof(SavedForgeRewardRestoreFiltersUnavailableExternalContent), SavedForgeRewardRestoreFiltersUnavailableExternalContent),
			new(nameof(SavedForgeRewardRestoreKeepsGoldFallbackWhenAllOptionsInvalid), SavedForgeRewardRestoreKeepsGoldFallbackWhenAllOptionsInvalid),
			new(nameof(StormReplacementRequiresMayhemAndUpgradeRune), StormReplacementRequiresMayhemAndUpgradeRune),
			new(nameof(EntomancerFallbackIsVersionScopedAndMissingHiveOnly), EntomancerFallbackIsVersionScopedAndMissingHiveOnly),
			new(nameof(EnemyPowerScalingDoesNotPatchOfficialModifierPipeline), EnemyPowerScalingDoesNotPatchOfficialModifierPipeline),
			new(nameof(EndlessMonsterPowerNormalizationUsesCapturedBaseAmounts), EndlessMonsterPowerNormalizationUsesCapturedBaseAmounts),
			new(nameof(CardPlayAllowancePreservesThirdPartyDenials), CardPlayAllowancePreservesThirdPartyDenials),
			new(nameof(CardPlayAllowanceAndBlockerUseSeparatePriorities), CardPlayAllowanceAndBlockerUseSeparatePriorities),
			new(nameof(GlassCannonHealCapRunsAfterHealingMultipliers), GlassCannonHealCapRunsAfterHealingMultipliers),
			new(nameof(HealCompositionUsesActualHpDelta), HealCompositionUsesActualHpDelta),
			new(nameof(ColorDiscoveryRewardUsesPublicCardsAndMissingSpecialFieldKeepsOriginal), ColorDiscoveryRewardUsesPublicCardsAndMissingSpecialFieldKeepsOriginal),
			new(nameof(ColorDiscoveryIncludesThirdPartyCharacterPools), ColorDiscoveryIncludesThirdPartyCharacterPools),
			new(nameof(MapLengthReducerRejectsGoldenPathAndThirdPartyMapTypes), MapLengthReducerRejectsGoldenPathAndThirdPartyMapTypes),
			new(nameof(JeweledGauntletReflectionTargetsFailClosedAsAGroup), JeweledGauntletReflectionTargetsFailClosedAsAGroup),
			new(nameof(TestSubjectRespawnReflectionMissingFallsBackToZero), TestSubjectRespawnReflectionMissingFallsBackToZero),
			new(nameof(InspectOpenScopesToHextechAndPreservesExternalPrefixChanges), InspectOpenScopesToHextechAndPreservesExternalPrefixChanges),
			new(nameof(TurnProcKeysPreserveBuiltInsAndNamespaceExternalDerivatives), TurnProcKeysPreserveBuiltInsAndNamespaceExternalDerivatives)
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
		tracking.InspectExtraDrawsPreventedThisTurn[11] = 2;
		tracking.GripPlayersTriggeredThisTurn.Add(12);
		tracking.MonsterDebuffActionProcKeysThisTurn.Add("debuff-action");

		tracking.PreparePlayerSideTurnEnd();

		Equal(1, tracking.ClownCollegeProcsThisTurn.Count, "player side end should keep clown college round proc count");
		Equal(1, tracking.EnemyPorcupineTriggersThisTurn.Count, "player side end should keep porcupine round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredThisTurn.Count, "player side end should keep eight penny gate first round proc count");
		Equal(1, tracking.EightPennyGatePlayersTriggeredSecondThisTurn.Count, "player side end should keep eight penny gate second round proc count");
		Equal(1, tracking.InspectExtraDrawsPreventedThisTurn.Count, "player side end should keep inspect draw count");
		Equal(1, tracking.GripPlayersTriggeredThisTurn.Count, "player side end should keep grip proc count");

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
		Equal(1, tracking.InspectExtraDrawsPreventedThisTurn.Count, "enemy side start should keep inspect draw count");
		Equal(1, tracking.GripPlayersTriggeredThisTurn.Count, "enemy side start should keep grip proc count");
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
		Equal(0, tracking.InspectExtraDrawsPreventedThisTurn.Count, "player side start should reset inspect draw count");
		Equal(0, tracking.GripPlayersTriggeredThisTurn.Count, "player side start should reset grip proc count");
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

	private static void HungryExhaustsZeroOneOrTwoCardsByTier()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 1, 0), "tier one should exhaust no cards");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 2, 1), "tier two should exhaust the first card");
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 2, 1), "tier two should not exhaust the second card");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should exhaust the first card");
		Expect(EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should exhaust the second card");
		Expect(!EightPennyGateEnemyHex.TryConsumeExhaustSlot(tracking, 3, 2), "tier three should not exhaust the third card");
	}

	private static void InspectBlocksOnlyTheConfiguredExtraDrawTriggers()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 1, 2, fromHandDraw: true), "normal hand draw should never be blocked");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 1, 0, fromHandDraw: false), "tier one should block no extra draws");

		tracking.BeginPlayerTurnStart([2]);
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "turn-start extra draw should never be blocked");
		Equal(0, tracking.InspectExtraDrawsPreventedThisTurn.Count, "turn-start draw should not consume an inspect trigger");
		tracking.EnterPlayerPlayPhase(2);
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "tier two should block the first in-turn extra draw trigger");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 2, 1, fromHandDraw: false), "tier two should allow the second in-turn extra draw trigger");
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should block the first extra draw trigger");
		Expect(IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should block the second extra draw trigger");
		Expect(!IInspectEnemyHex.TryPreventExtraDraw(tracking, 3, 2, fromHandDraw: false), "tier three should allow the third extra draw trigger");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);
		Equal(2, restored.InspectExtraDrawsPreventedThisTurn[3], "inspect draw count should survive a mid-turn save/load");
	}

	private static void GripConsumesOnlyTheFirstManualCardTrigger()
	{
		HextechMayhemCombatTrackingState tracking = new();
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 1, 0), "tier one should consume no trigger");
		Expect(IGripEnemyHex.TryConsumeFirstCard(tracking, 2, 1), "tier two should consume the first card trigger");
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 2, 1), "tier two should ignore later card triggers");
		Expect(IGripEnemyHex.TryConsumeFirstCard(tracking, 3, 2), "tier three should consume the first card trigger");
		Expect(!IGripEnemyHex.TryConsumeFirstCard(tracking, 3, 2), "tier three should ignore later card triggers");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);
		SetEqual(new ulong[] { 2, 3 }, restored.GripPlayersTriggeredThisTurn, "grip guards should survive a mid-turn save/load");
	}

	private static void HungryInspectAndGripShareEightPennyGateTexture()
	{
		const string expected = "res://HextechRunes/images/relics/eightPennyGateRune.png";
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new HungryHex()), "hungry texture");
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new InspectHex()), "inspect texture");
		Equal(expected, HextechAssets.TryGetCustomRelicIconPath(new GripHex()), "grip texture");
	}

	private static void CombatTrackingGlobalProcOrdinalsSerializeAndReset()
	{
		Expect(!HextechEnemyHexContext.IsRoundIntervalDue(1, 3), "round intervals should not trigger on round one");
		Expect(!HextechEnemyHexContext.IsRoundIntervalDue(3, 3), "three-round interval should wait until round four");
		Expect(HextechEnemyHexContext.IsRoundIntervalDue(4, 3), "three-round interval should trigger on round four");
		Expect(HextechEnemyHexContext.IsRoundIntervalDue(8, 3), "three-round interval should trigger again on round eight");
		Expect(HextechEnemyHexContext.IsRoundIntervalDue(3, 2), "two-round interval should trigger on round three");
		Expect(HextechEnemyHexContext.IsRoundIntervalDue(2, 1), "one-round interval should trigger on round two");
		Expect(!HextechEnemyHexContext.IsRoundIntervalDue(4, 0), "nonpositive round intervals should stay disabled");

		HextechMayhemCombatTrackingState tracking = new();
		Equal(0, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy-archmage:net:1"), "first global proc ordinal");
		Equal(1, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, "enemy-archmage:net:1"), "second global proc ordinal");
		const string roundIntervalKey = "round-once:DivineIntervention:4";
		Equal(0, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, roundIntervalKey), "first interval proc in a round");
		Equal(1, HextechCombatProcTracker.ConsumeGlobalProcInCombat(tracking, roundIntervalKey), "extra turn should not repeat an interval proc");

		string serialized = tracking.Serialize();
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(serialized);

		Equal(2, restored.GlobalProcsThisCombat["enemy-archmage:net:1"], "global proc count should restore");
		Equal(2, restored.GlobalProcsThisCombat[roundIntervalKey], "round interval guard should restore");
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
		tracking.MonsterMaxHpCoefficientBase[17] = 143;
		tracking.MonsterMaxHpCoefficientProjected[17] = 187;

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

			HextechMayhemCombatTrackingState restored = new();
			restored.Restore(serialized[0]);
			Equal(143, restored.MonsterMaxHpCoefficientBase[17], "enemy max HP coefficient base should survive combat tracking restore");
			Equal(187, restored.MonsterMaxHpCoefficientProjected[17], "enemy max HP coefficient projection should survive combat tracking restore");
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
		typeof(CuttingEdgeAlchemistRune),
		typeof(DawnbringersResolveRune),
		typeof(EarthAwakensRune),
		typeof(EndlessRecoveryRune),
		typeof(EscapePlanRune),
		typeof(FeelTheBurnRune),
		typeof(HappyAccidentRune),
		typeof(HardBonesRune),
		typeof(MasterOfDualityRune),
		typeof(NeowsGrudgeRune),
		typeof(OkBoomerangRune),
		typeof(PrimitiveMadnessRune),
		typeof(RegenerationSuppressionRune),
		typeof(SuperBrainRune),
		typeof(SwordFlightRune),
		typeof(WarmogsSpiritRune)
	];

	private static void ConfigMigrationForceResetsBelowV15()
	{
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(14, ["some-user-custom-id"]);
		Equal(33, version, "v14 config should land on current version");
		SetEqual(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().ToArray(), disabled, "v14 config should force-reset to factory defaults");
	}

	// 「迁移链终点 == 新用户默认」双真值源守护:v15(0.8.4 出厂)默认禁用集是冻结基线,勿随注册表更新。
	// 若未来翻转某符文默认启停时只改了注册表旗标、忘了加迁移链段,此测试即红。
	private static void ConfigMigrationV15BaselineReachesCurrentDefault()
	{
		IReadOnlySet<string> baseline = HextechPlayerRuneConfigIds.FromTypes(Version15FactoryDisabledRuneTypes);
		(int version, IReadOnlySet<string> migrated) = HextechRuneConfiguration.MigrateDisabledIdsForTests(15, baseline);
		Equal(33, version, "v15 config should land on current version");
		SetEqual(
			HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().ToArray(),
			migrated,
			$"v15 factory defaults + migration chain should equal current factory defaults; migrated:\n{string.Join("\n", migrated.OrderBy(static id => id, StringComparer.Ordinal))}\ncurrent defaults:\n{string.Join("\n", HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().OrderBy(static id => id, StringComparer.Ordinal))}");
	}

	private static void ConfigMigrationV26AddsNewPlayerDefaultDisables()
	{
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(26, []);
		Equal(33, version, "v26 player config should land on current version");
		SetEqual(
			HextechPlayerRuneConfigIds.FromTypes(
			[
				typeof(OmegaRune),
				typeof(OkBoomerangRune),
				typeof(FeyMagicRune),
				typeof(AstralBodyRune)
			]).ToArray(),
			disabled,
			"v26 player config migration should add the newly default-disabled runes");
	}

	private static void ConfigMigrationCurrentVersionPreservesCustomDisabledIds()
	{
		string customId = HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().OrderBy(static id => id, StringComparer.Ordinal).First();
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(33, [customId]);
		Equal(33, version, "current-version config keeps version");
		SetEqual([customId], disabled, "current-version config should pass user selection through unchanged");

		(int monsterVersion, IReadOnlySet<string> disabledMonsters) =
			HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(33, [MonsterHexKind.FrostWraith.ToString()]);
		Equal(33, monsterVersion, "current-version monster config keeps version");
		SetEqual(
			[MonsterHexKind.FrostWraith.ToString()],
			disabledMonsters,
			"current-version monster config should preserve a user-enabled Blank Check");
	}

	private static void ConfigShareRoundTripKeepsActRarityWeights()
	{
		HextechRarityWeights[] expectedWeights =
		[
			new HextechRarityWeights(1, 2, 3),
			new HextechRarityWeights(4, 5, 6),
			new HextechRarityWeights(7, 8, 9)
		];
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot() with
		{
			RuneRarityWeightsByAct = expectedWeights
		};
		string code = HextechConfigShareCodec.Export(snapshot);
		HextechConfigShareCodec.ImportPreview preview = HextechConfigShareCodec.TryParseForTests(code, snapshot)
			?? throw new InvalidOperationException("act rarity share code should decode");
		SequenceEqual(expectedWeights, preview.Snapshot.RuneRarityWeightsByAct, "act rarity weights should survive share-code round trip");
	}

	private static void ConfigMigrationV27KeepsNormalWeightsAndEnablesConsecutiveSilverPrevention()
	{
		(int migratedVersion, HextechRarityWeights migratedWeights, bool ruleEnabledWithZeroLegacySilverWeight) =
			HextechRuneConfiguration.MigrateRarityConfigForTests(
				27,
				new HextechRarityWeights(4, 5, 6),
				new HextechRarityWeights(0, 7, 8));
		Equal(33, migratedVersion, "v27 rarity config should land on current version");
		Equal(new HextechRarityWeights(4, 5, 6), migratedWeights, "v27 normal weights should become rune weights");
		Equal(true, ruleEnabledWithZeroLegacySilverWeight, "legacy rarity config should enable consecutive-Silver prevention by default");

		(_, _, bool ruleEnabledWithPositiveLegacySilverWeight) = HextechRuneConfiguration.MigrateRarityConfigForTests(
			27,
			new HextechRarityWeights(1, 1, 1),
			new HextechRarityWeights(2, 1, 1));
		Equal(true, ruleEnabledWithPositiveLegacySilverWeight, "removed legacy after-Silver weights should not disable the new default-on rule");

		HextechRarityWeights[] migratedByAct = HextechRuneConfiguration.MigrateSingleRarityConfigForTests(
			31,
			new HextechRarityWeights(3, 4, 5));
		SequenceEqual(
			new[]
			{
				new HextechRarityWeights(3, 4, 5),
				new HextechRarityWeights(3, 4, 5),
				new HextechRarityWeights(3, 4, 5)
			},
			migratedByAct,
			"v31 single rarity weights should migrate to every act");
	}

	private static void ConfigMigrationV30EnablesAdvanceToRetreat()
	{
		string id = ModelDb.GetId<AdvanceToRetreatRune>().Entry;
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(29, [id]);
		Equal(33, version, "v29 player config should land on current version");
		Expect(!disabled.Contains(id), "v29 player config migration should enable Advance to Retreat");
	}

	private static void ConfigMigrationV31EnablesHappyAccident()
	{
		string id = ModelDb.GetId<HappyAccidentRune>().Entry;
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(30, [id]);
		Equal(33, version, "v30 player config should land on current version");
		Expect(!disabled.Contains(id), "v30 player config migration should enable Happy Accident");
	}

	private static void ConfigMigrationV33ChangesLegacyInfiniteMonsterRerolls()
	{
		(int migratedVersion, int migratedLimit) =
			HextechRuneConfiguration.MigrateMonsterHexRerollLimitForTests(32, HextechRuneConfiguration.InfiniteRerollLimit);
		Equal(33, migratedVersion, "v32 config should land on current version");
		Equal(1, migratedLimit, "v32 infinite enemy rerolls should migrate to the new one-reroll default");

		(_, int finiteLimit) = HextechRuneConfiguration.MigrateMonsterHexRerollLimitForTests(32, 4);
		Equal(4, finiteLimit, "v32 custom finite enemy rerolls should be preserved");

		(_, int currentInfiniteLimit) = HextechRuneConfiguration.MigrateMonsterHexRerollLimitForTests(
			33,
			HextechRuneConfiguration.InfiniteRerollLimit);
		Equal(
			HextechRuneConfiguration.InfiniteRerollLimit,
			currentInfiniteLimit,
			"v33 explicit infinite enemy rerolls should be preserved");
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

	private static void SavedPropertyPreInitRegistrationLeavesWireTablesUntouched()
	{
#if STS2_109_OR_NEWER
		Type cacheType = typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache);
		FieldInfo initializedField = cacheType.GetField(
			"_initialized",
			BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("0.109 SavedProperty cache initialized field should exist");
		string[] wireFieldNames =
		[
			"_savedPropertyCache",
			"_propertyNameToNetIdMap",
			"_netIdToPropertyNameMap"
		];
		Dictionary<string, (object? Value, int? Count)> before = wireFieldNames.ToDictionary(
			static name => name,
			name => SnapshotStaticCollection(cacheType, name),
			StringComparer.Ordinal);
		bool originalInitialized = initializedField.GetValue(null) is true;
		try
		{
			initializedField.SetValue(null, false);
			HextechSavedPropertyBootstrap.InjectModelType(typeof(PreInitSavedPropertyCarrier));
		}
		finally
		{
			initializedField.SetValue(null, originalInitialized);
		}

		foreach (string fieldName in wireFieldNames)
		{
			(object? afterValue, int? afterCount) = SnapshotStaticCollection(cacheType, fieldName);
			(object? beforeValue, int? beforeCount) = before[fieldName];
			Expect(ReferenceEquals(beforeValue, afterValue), $"{fieldName} instance should not change before official Init");
			Equal(beforeCount, afterCount, $"{fieldName} count before official Init");
		}
#else
		HextechSavedPropertyBootstrap.InjectModelType(typeof(PreInitSavedPropertyCarrier));
		ModelId id = ModelDb.GetId<PreInitSavedPropertyCarrier>();
		SavedProperties? properties = SavedProperties.FromInternal(new PreInitSavedPropertyCarrier(), id);
		Expect(
			properties?.ints?.Any(static property => property.name == "PreInitCounter") == true,
			"0.107 should still inject a SavedProperty carrier explicitly");
#endif
	}

	private static void SavedPropertyLateCarrierRegistrationFailsClosed()
	{
#if STS2_109_OR_NEWER
		ExpectThrows<InvalidOperationException>(
			() => HextechRunesApi.RegisterSavedPropertyCarrier<LateSavedPropertyCarrier>(),
			"0.109 should reject a SavedProperty carrier missing from the initialized per-type cache");
#endif
	}

	private static void SavedPropertySameNameCarrierStillRequiresPerTypeCache()
	{
#if STS2_109_OR_NEWER
		Type cacheType = typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache);
		Action[] restore =
		[
			CaptureStaticCollectionRestore(cacheType, "_savedPropertyCache"),
			CaptureStaticCollectionRestore(cacheType, "_propertyNameToNetIdMap"),
			CaptureStaticCollectionRestore(cacheType, "_netIdToPropertyNameMap")
		];
		try
		{
			MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache
				.CacheSavedPropertiesForTypeDebug(typeof(SameNameSavedPropertyCarrierA));
			HextechSavedPropertyBootstrap.EnsureModelTypeRegistrationAllowed(
				typeof(SameNameSavedPropertyCarrierA));
			ExpectThrows<InvalidOperationException>(
				() => HextechSavedPropertyBootstrap.EnsureModelTypeRegistrationAllowed(
					typeof(SameNameSavedPropertyCarrierB)),
				"a globally known SavedProperty name must not hide a missing per-type carrier cache");
		}
		finally
		{
			foreach (Action restoreCollection in restore.Reverse())
			{
				restoreCollection();
			}
		}
#endif
	}

	private static void SavedPropertyLateExternalRegistrationLeavesNoPartialState()
	{
#if STS2_109_OR_NEWER
		Type runeType = typeof(LateExternalRegistrationRune);
		int registryVersion = HextechExternalContentRegistry.Version;
		int registrationCount = HextechExternalContentRegistry
			.GetPlayerRuneRegistrations()
			.Count;
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool),
				runeType),
			"late test rune should not start in the shared relic pool queue");

		ExpectThrows<InvalidOperationException>(
			() => HextechRunesApi.RegisterPlayerRune<LateExternalRegistrationRune>(
				HextechRarityTier.Silver),
			"late external registration with an uncached SavedProperty should fail before mutation");

		Equal(registryVersion, HextechExternalContentRegistry.Version, "late failure registry version");
		Equal(
			registrationCount,
			HextechExternalContentRegistry.GetPlayerRuneRegistrations().Count,
			"late failure registration count");
		Expect(
			!HextechExternalContentRegistry
				.GetPlayerRuneRegistrations()
				.Any(registration => registration.Type == runeType),
			"late failure should not enter the external player rune registry");
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool),
				runeType),
			"late failure should not enter the shared relic pool queue");
#endif
	}

	private static (object? Value, int? Count) SnapshotStaticCollection(Type type, string fieldName)
	{
		FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException($"{type.FullName}.{fieldName} should exist");
		object? value = field.GetValue(null);
		int? count = value switch
		{
			ICollection collection => collection.Count,
			null => null,
			_ => value.GetType().GetProperty("Count")?.GetValue(value) as int?
		};
		return (value, count);
	}

	private static Action CaptureStaticCollectionRestore(Type type, string fieldName)
	{
		FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException($"{type.FullName}.{fieldName} should exist");
		object value = field.GetValue(null)
			?? throw new InvalidOperationException($"{type.FullName}.{fieldName} should not be null");
		if (value is IDictionary dictionary)
		{
			DictionaryEntry[] entries = dictionary
				.Cast<DictionaryEntry>()
				.ToArray();
			return () =>
			{
				dictionary.Clear();
				foreach (DictionaryEntry entry in entries)
				{
					dictionary.Add(entry.Key, entry.Value);
				}
			};
		}
		if (value is IList list)
		{
			object?[] items = list
				.Cast<object?>()
				.ToArray();
			return () =>
			{
				list.Clear();
				foreach (object? item in items)
				{
					list.Add(item);
				}
			};
		}

		throw new InvalidOperationException(
			$"{type.FullName}.{fieldName} is not a mutable dictionary or list");
	}

	private static void RunBeforeSavedPropertyCacheInitialization(Action action)
	{
#if STS2_109_OR_NEWER
		FieldInfo initializedField = typeof(MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache)
			.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("0.109 SavedProperty cache initialized field should exist");
		bool originalInitialized = initializedField.GetValue(null) is true;
		try
		{
			initializedField.SetValue(null, false);
			action();
		}
		finally
		{
			initializedField.SetValue(null, originalInitialized);
		}
#else
		action();
#endif
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

	private static void SearingAttackRuneGrantsUpgradedCard()
	{
		Expect(typeof(HextechOwnerPoolTokenCard).IsAbstract, "owner-pool token card base should stay abstract");
		Expect(
			!HextechCustomModelRegistry.CustomCardTypes.Contains(typeof(HextechOwnerPoolTokenCard)),
			"owner-pool token card base must not enter the concrete model registry");
		Equal(
			HextechCustomModelRegistry.CustomCardTypes.Count,
			HextechCustomModelRegistry.CustomCardTypes.Count(
				static type => typeof(HextechOwnerPoolTokenCard).IsAssignableFrom(type) && !type.IsAbstract),
			"all registered custom cards should use the owner-pool token card contract");

		SearingAttackCard card = CreateMutableTestModel<SearingAttackCard>();

		SearingAttackRune.UpgradeGrantedCard(card);

		Equal(1, card.CurrentUpgradeLevel, "granted Searing Attack upgrade level");
		Equal(16m, card.DynamicVars.Damage.BaseValue, "granted Searing Attack damage");
	}

	private static void CardUpgradePickupAndAvailabilityRules()
	{
		BloodlettingUpgradeRune singleForm = new();
		Expect(singleForm.GrantsCardOnPickup, "ordinary card upgrade runes should grant their target card");
		Expect(singleForm.HasUponPickupEffect, "ordinary card upgrade runes should advertise their pickup effect");
		Expect(singleForm.MeetsCardAvailabilityRequirement([]), "ordinary card upgrade runes should not require the target card");

		BashUpgradeRune bash = new();
		NeutralizeUpgradeRune neutralize = new();
		FallingStarUpgradeRune fallingStar = new();
		UnleashUpgradeRune unleash = new();
		DualcastUpgradeRune dualcast = new();
		RelicModel[] dualFormRunes = [ bash, neutralize, fallingStar, unleash, dualcast ];
		foreach (RelicModel rune in dualFormRunes)
		{
			Expect(!rune.HasUponPickupEffect, $"{rune.GetType().Name} should not grant a card on pickup");
			Expect(
				rune is IHextechSelectionFooterProvider footerProvider
				&& footerProvider.GetSelectionFooterText() == null,
				$"{rune.GetType().Name} should not show a pickup footer");
		}

		Expect(!bash.MeetsCardAvailabilityRequirement([]), "Bash upgrade should require Bash or Break");
		Expect(bash.MeetsCardAvailabilityRequirement([new Bash()]), "Bash upgrade should accept Bash");
		Expect(bash.MeetsCardAvailabilityRequirement([new Break()]), "Bash upgrade should accept Break");
		Expect(!neutralize.MeetsCardAvailabilityRequirement([]), "Neutralize upgrade should require Neutralize or Suppress");
		Expect(neutralize.MeetsCardAvailabilityRequirement([new Neutralize()]), "Neutralize upgrade should accept Neutralize");
		Expect(neutralize.MeetsCardAvailabilityRequirement([new Suppress()]), "Neutralize upgrade should accept Suppress");
		Expect(!fallingStar.MeetsCardAvailabilityRequirement([]), "Falling Star upgrade should require Falling Star or Meteor Shower");
		Expect(fallingStar.MeetsCardAvailabilityRequirement([new FallingStar()]), "Falling Star upgrade should accept Falling Star");
		Expect(fallingStar.MeetsCardAvailabilityRequirement([new MeteorShower()]), "Falling Star upgrade should accept Meteor Shower");
		Expect(!unleash.MeetsCardAvailabilityRequirement([]), "Unleash upgrade should require Unleash or Protector");
		Expect(unleash.MeetsCardAvailabilityRequirement([new Unleash()]), "Unleash upgrade should accept Unleash");
		Expect(unleash.MeetsCardAvailabilityRequirement([new Protector()]), "Unleash upgrade should accept Protector");
		Expect(!dualcast.MeetsCardAvailabilityRequirement([]), "Dualcast upgrade should require Dualcast or Quadcast");
		Expect(dualcast.MeetsCardAvailabilityRequirement([new Dualcast()]), "Dualcast upgrade should accept Dualcast");
		Expect(dualcast.MeetsCardAvailabilityRequirement([new Quadcast()]), "Dualcast upgrade should accept Quadcast");

		Expect((object)new StrikeUpgradeRune() is not IHextechSelectionFooterProvider, "Strike upgrade should not show a pickup footer");
		Expect((object)new DefendUpgradeRune() is not IHextechSelectionFooterProvider, "Defend upgrade should not show a pickup footer");
		Expect(!StrikeUpgradeRune.HasBasicStrike([]), "Strike upgrade should require a basic Strike");
		Expect(StrikeUpgradeRune.HasBasicStrike([new StrikeIronclad()]), "Strike upgrade should accept a basic Strike");
		Expect(!DefendUpgradeRune.HasBasicDefend([]), "Defend upgrade should require a basic Defend");
		Expect(DefendUpgradeRune.HasBasicDefend([new DefendIronclad()]), "Defend upgrade should accept a basic Defend");
	}

	private static void BashUpgradeStrengthMatchesVulnerableApplied()
	{
		Bash bash = CreateMutableTestModel<Bash>();
		Equal(2m, BashUpgradeRune.CalculateStrengthGain(bash), "base Bash vulnerable and Strength");
		CardCmd.Upgrade(bash);
		Equal(3m, BashUpgradeRune.CalculateStrengthGain(bash), "upgraded Bash vulnerable and Strength");

		Break breakCard = CreateMutableTestModel<Break>();
		Equal(5m, BashUpgradeRune.CalculateStrengthGain(breakCard), "base Break vulnerable and Strength");
		CardCmd.Upgrade(breakCard);
		Equal(7m, BashUpgradeRune.CalculateStrengthGain(breakCard), "upgraded Break vulnerable and Strength");

		Equal(0m, BashUpgradeRune.CalculateStrengthGain(new StrikeIronclad()), "unrelated card Strength");
	}

	private static void StarterUpgradeCapsTerminateExternalUpgradeToMaxLoops()
	{
		Equal(999, HextechStarterUpgradeHooks.UpgradeLevelCap, "starter multi-upgrade cap");
		Equal(
			999,
			HextechStarterUpgradeHooks.ResolveOwnedMaxUpgradeLevel(0),
			"owned basic cards with the matching rune use the +999 cap");
		Equal(
			1001,
			HextechStarterUpgradeHooks.ResolveOwnedMaxUpgradeLevel(1001),
			"owned legacy over-cap cards remain loadable but cannot grow further");
		Equal(
			1,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(0, isDeserializing: false),
			"new unowned cards keep the vanilla cap");
		Equal(
			1,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(998, isDeserializing: false),
			"ordinary unowned cards do not inherit the rune cap");
		Equal(
			1001,
			HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(1000, isDeserializing: true),
			"legacy over-cap saves can replay the next upgrade level");

		int simulatedUpgradeLevel = 0;
		int upgradeCount = 0;
		while (simulatedUpgradeLevel < HextechStarterUpgradeHooks.ResolveUnownedMaxUpgradeLevel(
			simulatedUpgradeLevel,
			isDeserializing: false))
		{
			simulatedUpgradeLevel++;
			upgradeCount++;
			Expect(upgradeCount <= 1, "UpgradeAllCards-style loop must terminate at the vanilla cap");
		}

		Equal(1, simulatedUpgradeLevel, "UpgradeAllCards-style loop final level");
		Equal(1, upgradeCount, "UpgradeAllCards-style loop iteration count");

		SearingAttackCard searingAttack = CreateMutableTestModel<SearingAttackCard>();
		Equal(999, searingAttack.MaxUpgradeLevel, "Searing Attack cap");
	}

	private static void CreativeAiUpgradeRuneUpgradesGeneratedPowerCards()
	{
		CreativeAi card = CreateMutableTestModel<CreativeAi>();

		Expect(CreativeAiUpgradeRune.UpgradeGeneratedCard(card), "Creative AI should generate an upgraded Power card");
		Equal(1, card.CurrentUpgradeLevel, "Creative AI generated card upgrade level");
		Expect(!CreativeAiUpgradeRune.UpgradeGeneratedCard(card), "an already upgraded generated card should not be upgraded twice");

		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(BlankCheckRune).GetMethod(nameof(BlankCheckRune.AfterPlayerTurnStart))!),
			nameof(BlankCheckRune));
		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(MindOverMatterRune).GetMethod(nameof(MindOverMatterRune.BeforeHandDraw))!),
			nameof(MindOverMatterRune));
		ExpectCombatGenerationFilters(
			GetAsyncStateMachineMoveNext(typeof(SingularityAIRune).GetMethod(nameof(SingularityAIRune.BeforeHandDraw))!),
			nameof(SingularityAIRune));
		ExpectCombatGenerationFilters(
			typeof(CorruptedBranchRune).GetMethod("CreateRandomCombatCard", BindingFlags.Instance | BindingFlags.NonPublic)!,
			nameof(CorruptedBranchRune));
		ExpectCombatGenerationFilters(
			typeof(ColorDiscoveryRune).GetMethod("GetOtherCharacterCards", BindingFlags.NonPublic | BindingFlags.Static)!,
			nameof(ColorDiscoveryRune));
	}

	private static void SubroutineUpgradeCombatMoveGateResetsAcrossCombats()
	{
		SubroutineUpgradeRune rune = new();

		Expect(rune.TryConsumeCombatStartMove(), "first combat-start move should be consumed");
		Expect(!rune.TryConsumeCombatStartMove(), "same combat should reject a second move");

		rune.BeforeCombatStart().GetAwaiter().GetResult();
		Expect(rune.TryConsumeCombatStartMove(), "combat start should reset the move gate");

		rune.AfterCombatEnd(null!).GetAwaiter().GetResult();
		Expect(rune.TryConsumeCombatStartMove(), "combat end should clear the move gate");
	}

	private static MethodInfo GetAsyncStateMachineMoveNext(MethodInfo asyncMethod)
	{
		Type stateMachineType = asyncMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
			?? throw new InvalidOperationException($"{asyncMethod.DeclaringType?.Name}.{asyncMethod.Name} is not async");
		return stateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new MissingMethodException(stateMachineType.FullName, "MoveNext");
	}

	private static void ExpectCombatGenerationFilters(MethodBase method, string label)
	{
		List<MethodInfo> calledMethods = [];
		CollectReferencedMethods(method, calledMethods, []);
		Expect(
			calledMethods.Any(static called => called.DeclaringType == typeof(CardFactory)
				&& called.Name == nameof(CardFactory.FilterForCombat)),
			$"{label} should use CardFactory.FilterForCombat");
		Expect(
			calledMethods.Any(static called => called.DeclaringType == typeof(CardModel)
				&& called.Name == "get_CanBeGeneratedByModifiers"),
			$"{label} should reject cards that modifiers cannot generate");
	}

	private static void CollectReferencedMethods(
		MethodBase method,
		List<MethodInfo> referencedMethods,
		HashSet<MethodBase> visited)
	{
		if (!visited.Add(method))
		{
			return;
		}

		foreach (MethodInfo referenced in PatchProcessor.GetOriginalInstructions(method)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>())
		{
			referencedMethods.Add(referenced);
			if (referenced.DeclaringType?.Assembly == typeof(BlankCheckRune).Assembly)
			{
				CollectReferencedMethods(referenced, referencedMethods, visited);
			}
		}
	}

	private static void FortuneForgeRewardScalesByStacks()
	{
		FortuneForge forge = CreateMutableTestModel<FortuneForge>();
		Equal(100, forge.ExtraGoldRewardAmount, "single-stack Fortune Forge reward");

		forge.SavedStackCount = 2;
		Equal(200, forge.ExtraGoldRewardAmount, "two-stack Fortune Forge reward");
	}

	private static void InitialForgeGrantRunesPersistPendingTransaction()
	{
		Type[] initialForgeRunes =
		[
			typeof(StatsRune),
			typeof(StatsOnStatsRune),
			typeof(StatsOnStatsOnStatsRune),
			typeof(HailToTheKingRune)
		];
		foreach (Type type in initialForgeRunes)
		{
			Expect(
				type.IsSubclassOf(typeof(InitialForgeGrantRune)),
				$"{type.Name} should use the resumable initial forge transaction");
		}

		StatsOnStatsRune rune = new();
		Expect(!rune.SavedInitialForgeGrantPending, "initial forge transaction should default to completed");
		rune.SavedInitialForgeGrantPending = true;
		Expect(rune.SavedInitialForgeGrantPending, "pending initial forge transaction should be saveable");

		MethodInfo method = typeof(HextechForgeGrantHelper).GetMethod(
			"TryObtainRandomForges",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechForgeGrantHelper), "TryObtainRandomForges");
		Equal(typeof(Task<bool>), method.ReturnType, "initial forge transaction completion result");
	}

	private static void InitialForgeGrantLoadRecoveryPrecedesActRecovery()
	{
		MethodInfo recovery = typeof(HextechRunLifecycleHooks).GetMethod(
			"ResumePendingSelectionTransactionsAfterLoad",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechRunLifecycleHooks), "ResumePendingSelectionTransactionsAfterLoad");
		MethodInfo moveNext = GetAsyncStateMachineMoveNext(recovery);
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(moveNext)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		int forgeRecoveryIndex = Array.FindIndex(
			calls,
			static method => method.Name == "ResumePendingInitialForgeGrantsAfterLoad");
		int actRecoveryIndex = Array.FindIndex(
			calls,
			static method => method.Name == "ResumePendingActSelectionAfterLoad");

		Expect(forgeRecoveryIndex >= 0, "load continuation should resume pending initial forge grants");
		Expect(
			actRecoveryIndex > forgeRecoveryIndex,
			"load continuation should finish pending initial forge grants before resuming act selection");
	}

	private static void HappyAccidentUsesExhaustedStatusesAtTurnStart()
	{
		CardModel[] exhaustedCards =
		[
			CreateMutableTestModel<Dazed>(),
			CreateMutableTestModel<StrikeIronclad>(),
			CreateMutableTestModel<Slimed>()
		];
		Equal(2, HappyAccidentRune.CountStatusCards(exhaustedCards), "Happy Accident exhausted Status count");
		Equal(0, HappyAccidentRune.ResolveOrbCount(-1, 1), "Happy Accident negative Status fallback");
		Equal(0, HappyAccidentRune.ResolveOrbCount(3, 0), "Happy Accident disabled orb count");
		Equal(3, HappyAccidentRune.ResolveOrbCount(3, 1), "Happy Accident one orb per exhausted Status");

		MethodInfo[] declaredMethods = typeof(HappyAccidentRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(
			declaredMethods.Any(static method => method.Name == nameof(HappyAccidentRune.AfterPlayerTurnStart)),
			"Happy Accident should trigger at player turn start");
		Expect(
			declaredMethods.All(static method => method.Name != "AfterCardGeneratedForCombat"),
			"Happy Accident should no longer trigger when Status cards are generated");
	}

	private static void PrismaticEggIsExcludedFromThirdAct()
	{
		Expect(
			HextechContentRegistry.PlayerRuneMetadata.HasFlag(typeof(PrismaticEggRune), PlayerRuneFlags.ThirdActExcluded),
			"Prismatic Egg should not appear in the third act rune pool");
	}

	private static void MirrorReflectionCopiesCursesButNotBasicCards()
	{
		Expect(MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<Clumsy>()), "Mirror Reflection should duplicate Curse cards");
		Expect(!MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<StrikeIronclad>()), "Mirror Reflection should not duplicate basic Strike cards");
		Expect(!MirrorReflectionRune.ShouldDuplicate(CreateMutableTestModel<DefendIronclad>()), "Mirror Reflection should not duplicate basic Defend cards");
	}

	private static void DrainTargetsFirstEnemyWithHighestCurrentHp()
	{
		Equal(1, DrainRune.FindHighestCurrentHpIndex([ 8, 25, 25, 12 ]), "Drain should target the first enemy tied for highest current HP");
		Equal(0, DrainRune.FindHighestCurrentHpIndex([ 30 ]), "Drain should target the only hittable enemy");
	}

	private static void FeyMagicUsesThreeCostWithoutTurnLimit()
	{
		Equal(3, FeyMagicRune.MinimumCardCost, "Fey Magic minimum card cost");

		MethodInfo[] declaredMethods = typeof(FeyMagicRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(declaredMethods.Any(method => method.Name == "AfterDamageGiven"), "Fey Magic should trigger after each qualifying damage event");
		Expect(declaredMethods.All(method => method.Name != "BeforeSideTurnStart"), "Fey Magic should not keep a per-turn trigger reset");
	}

	private static void GiantSlayerScalesFromEnemyMaxHp()
	{
		Equal(1m, GiantSlayerRune.ResolveDamageMultiplier(0), "zero-HP fallback multiplier");
		Equal(1m, GiantSlayerRune.ResolveDamageMultiplier(7), "below first eight-HP step multiplier");
		Equal(1.01m, GiantSlayerRune.ResolveDamageMultiplier(8), "first eight-HP step multiplier");
		Equal(1.49m, GiantSlayerRune.ResolveDamageMultiplier(399), "multiplier before cap");
		Equal(1.5m, GiantSlayerRune.ResolveDamageMultiplier(400), "fifty-percent cap multiplier");
		Equal(1.5m, GiantSlayerRune.ResolveDamageMultiplier(9999), "multiplier remains capped");
	}

	private static void SomethingForNothingDrawsAtZeroAndDiscountsFirstPaidCard()
	{
		Expect(SomethingForNothingRune.IsZeroCostPlay(0m), "zero-cost cards should draw");
		Expect(SomethingForNothingRune.IsZeroCostPlay(-1m), "negative sentinel costs should remain in the zero-cost branch");
		Expect(!SomethingForNothingRune.IsZeroCostPlay(1m), "positive-cost cards should use the discount branch");
		Equal(0, SomethingForNothingRune.ReduceCost(0, 1), "combat discount should not make costs negative");
		Equal(1, SomethingForNothingRune.ReduceCost(2, 1), "combat discount should reduce the card by one");
		Equal(2, SomethingForNothingRune.ReduceCost(2, -1), "negative reductions should be ignored");

		PlayerRuneRegistration registration = HextechPlayerRuneRegistry.Registrations.Single(
			registration => registration.Type == typeof(SomethingForNothingRune));
		Equal(HextechRarityTier.Prismatic, registration.Rarity, "Something for Nothing rarity");
		Equal("RESOURCE", registration.TagKey, "Something for Nothing tag");
		Expect(
			typeof(SomethingForNothingRune).GetMethod(
				nameof(SomethingForNothingRune.BeforeSideTurnStart),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) != null,
			"Something for Nothing should reset its paid-card trigger each turn");
	}

	private static void MagicMissileUsesThreeTwoPercentHits()
	{
		Equal(3, MagicMissileRune.MissileCount, "Magic Missile hit count");
		Equal(2m, MagicMissileRune.MaxHpDamagePercent, "Magic Missile max-HP damage percent");
		Equal(0.055f, HextechCombatVfx.MagicMissileLaunchIntervalSeconds, "Magic Missile launch interval");
		Equal(0.28f, HextechCombatVfx.MagicMissileBaseFlightSeconds, "Magic Missile base flight duration");
		Equal(0.025f, HextechCombatVfx.MagicMissileFlightStepSeconds, "Magic Missile flight duration step");
		MethodInfo? afterCardPlayed = typeof(MagicMissileRune).GetMethod(
			nameof(MagicMissileRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Magic Missile should not hold the card-play hook open while projectiles resolve");
		Equal(1, MagicMissileRune.CalculateMissileDamage(1), "Magic Missile should deal at least one damage");
		Equal(2, MagicMissileRune.CalculateMissileDamage(100), "Magic Missile should deal two percent of 100 max HP");
		Equal(3, MagicMissileRune.CalculateMissileDamage(199), "Magic Missile should round max-HP damage down");
	}

	private static void TwinFlamesUsesTwoEnergyScaledHits()
	{
		Equal(2, TwinFlamesRune.MissileCount, "Twin Flames hit count");
		Equal(0m, TwinFlamesRune.ResolveMissileDamage(-1m), "Twin Flames should not create negative damage");
		Equal(0m, TwinFlamesRune.ResolveMissileDamage(0m), "zero-cost Skills should resolve to zero missile damage");
		Equal(3m, TwinFlamesRune.ResolveMissileDamage(3m), "Twin Flames damage should equal the played Skill's Energy cost");
		Expect(!TwinFlamesRune.ShouldLaunchMissiles(0m), "zero-cost Skills should not launch Twin Flames missiles");
		Expect(TwinFlamesRune.ShouldLaunchMissiles(1m), "positive-cost Skills should launch Twin Flames missiles");
		MethodInfo? afterCardPlayed = typeof(TwinFlamesRune).GetMethod(
			nameof(TwinFlamesRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Twin Flames should not hold the card-play hook open while projectiles resolve");
		Expect(
			typeof(HextechCombatVfx).GetMethod(
				"PlayTwinFlamesMissile",
				BindingFlags.Static | BindingFlags.NonPublic) != null,
			"Twin Flames should expose its blue-yellow projectile VFX path");
	}

	private static void EchoAddsItsCopyWithoutRecursingThroughGenerationHooks()
	{
		MethodInfo hook = typeof(EchoRune).GetMethod(
			nameof(EchoRune.AfterCardGeneratedForCombat),
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(EchoRune), nameof(EchoRune.AfterCardGeneratedForCombat));
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(GetAsyncStateMachineMoveNext(hook))
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Add)),
			"Echo should add its already-cloned copy directly to the destination pile");
		Expect(
			calls.All(static method => method.DeclaringType != typeof(HextechCardGeneration)),
			"Echo copies must not recursively enter the generated-card hook chain");
	}

	private static void TwinFlamesKeepsMultiplayerDamageInsideCardAction()
	{
		MethodInfo afterCardPlayed = typeof(TwinFlamesRune).GetMethod(
			nameof(TwinFlamesRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(TwinFlamesRune), nameof(TwinFlamesRune.AfterCardPlayed));
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(afterCardPlayed)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(HextechPlayerContextHelper) && method.Name == nameof(HextechPlayerContextHelper.IsNetworkMultiplayerRun)),
			"Twin Flames should use its multiplayer lockstep path in network runs");
		Expect(
			calls.Any(static method => method.Name == "ResolveVolleyDamageInLockstepAsync"),
			"Twin Flames multiplayer damage should be returned to the current card action");
	}

	private static void ProjectileRunesKeepMultiplayerDamageInsideCardAction()
	{
		foreach (Type runeType in new[] { typeof(MagicMissileRune), typeof(TwinFlamesRune), typeof(LightEmUpRune) })
		{
			MethodInfo afterCardPlayed = runeType.GetMethod(
				nameof(HextechRelicBase.AfterCardPlayed),
				BindingFlags.Instance | BindingFlags.Public)
				?? throw new MissingMethodException(runeType.Name, nameof(HextechRelicBase.AfterCardPlayed));
			MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(afterCardPlayed)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.ToArray();
			Expect(
				calls.Any(static method => method.DeclaringType == typeof(HextechPlayerContextHelper) && method.Name == nameof(HextechPlayerContextHelper.IsNetworkMultiplayerRun)),
				$"{runeType.Name} should select a multiplayer lockstep path");
			Expect(
				calls.Any(static method => method.Name == "ResolveVolleyDamageInLockstepAsync"),
				$"{runeType.Name} should return its multiplayer damage task to the card action");
		}
	}

	private static void LightEmUpUsesFiveEnergyScaledTwinFlameMissiles()
	{
		Equal(4, LightEmUpRune.AttacksPerVolley, "Light Em Up attacks per volley");
		Equal(5, LightEmUpRune.MissileCount, "Light Em Up missile count");
		Equal(0m, LightEmUpRune.ResolveMissileDamage(-1m), "Light Em Up should not create negative damage");
		Equal(3m, LightEmUpRune.ResolveMissileDamage(3m), "Light Em Up damage should equal the triggering Attack's Energy cost");

		int progress = 0;
		for (int attackIndex = 0; attackIndex < 3; attackIndex++)
		{
			progress = LightEmUpRune.AdvanceAttackProgress(progress, 1m, out bool launchedEarly);
			Expect(!launchedEarly, "Light Em Up should not launch before the fourth Attack");
		}

		progress = LightEmUpRune.AdvanceAttackProgress(progress, 0m, out bool launchedAtZeroCostThreshold);
		Equal(4, progress, "zero-cost fourth Attack should hold Light Em Up at full progress");
		Expect(!launchedAtZeroCostThreshold, "zero-cost fourth Attack should not launch Light Em Up missiles");
		progress = LightEmUpRune.AdvanceAttackProgress(progress, 0m, out bool launchedWhileStored);
		Equal(4, progress, "additional zero-cost Attacks should preserve stored Light Em Up progress");
		Expect(!launchedWhileStored, "stored Light Em Up progress should wait for a positive-cost Attack");
		progress = LightEmUpRune.AdvanceAttackProgress(progress, 2m, out bool launchedAfterStoredProgress);
		Equal(0, progress, "Light Em Up should reset after launching its stored volley");
		Expect(launchedAfterStoredProgress, "positive-cost Attack should release stored Light Em Up missiles");

		MethodInfo? afterCardPlayed = typeof(LightEmUpRune).GetMethod(
			nameof(LightEmUpRune.AfterCardPlayed),
			BindingFlags.Instance | BindingFlags.Public);
		Equal<AsyncStateMachineAttribute?>(
			null,
			afterCardPlayed?.GetCustomAttribute<AsyncStateMachineAttribute>(),
			"Light Em Up should not hold the card-play hook open while projectiles resolve");
		Expect(
			typeof(LightEmUpRune).GetMethod(
				nameof(LightEmUpRune.ModifyCardPlayCount),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) == null,
			"Light Em Up should no longer replay the fourth Attack");
		Expect(
			typeof(HextechCombatVfx).GetMethod(
				"PlayTwinFlamesMissile",
				BindingFlags.Static | BindingFlags.NonPublic) != null,
			"Light Em Up should reuse the blue-yellow Twin Flames projectile VFX path");
	}

	private static void PiercingThreadSplitsOneDamageEventBeforeBlock()
	{
		Equal(50m, PiercingThreadRune.PiercingPercent, "Piercing Thread percentage");
		Equal(0, PiercingThreadRune.CalculatePiercingDamage(-1m), "negative damage should not pierce");
		Equal(0, PiercingThreadRune.CalculatePiercingDamage(1m), "one damage should round its piercing half down");
		Equal(2, PiercingThreadRune.CalculatePiercingDamage(5m), "odd piercing damage should round down");
		Equal(5, PiercingThreadRune.CalculatePiercingDamage(10m), "even piercing damage should split evenly");
		Equal(3m, PiercingThreadRune.CalculateBlockableDamage(5m), "the non-piercing remainder should still hit Block");
		Equal(5m, PiercingThreadRune.CalculateBlockableDamage(10m), "half of even damage should remain blockable");
		Equal(5m, 10m - Math.Min(100m, PiercingThreadRune.CalculateBlockableDamage(10m)), "full Block should still take five piercing damage");
		Equal(7m, 11m - Math.Min(4m, PiercingThreadRune.CalculateBlockableDamage(11m)), "piercing damage and block overflow should remain one damage result");

		PlayerRuneRegistration registration = HextechPlayerRuneRegistry.Registrations.Single(
			registration => registration.Type == typeof(PiercingThreadRune));
		Equal(HextechRarityTier.Gold, registration.Rarity, "Piercing Thread rarity");
		Equal("OUTPUT", registration.TagKey, "Piercing Thread tag");
		Expect(
			typeof(HextechCombatHooks).GetMethod(
				"PiercingThreadDamageBlockPrefix",
				BindingFlags.Static | BindingFlags.NonPublic) != null,
			"Piercing Thread should alter the blockable amount at the original block-consumption boundary");
	}

	private static void DualcastUpgradeReturnsBothCastCardsToHand()
	{
		Expect(
			DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Dualcast>()),
			"Dualcast Upgrade should return Dualcast to hand");
		Expect(
			DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Quadcast>()),
			"Dualcast Upgrade should return Quadcast to hand");
		Expect(
			!DualcastUpgradeRune.IsSupportedCard(CreateMutableTestModel<Zap>()),
			"Dualcast Upgrade should ignore unrelated cards");
		Expect(
			DualcastUpgradeRune.CanReturnFromResultPile(PileType.Discard),
			"normal result piles should be redirected to hand");
		Expect(
			!DualcastUpgradeRune.CanReturnFromResultPile(PileType.None),
			"temporary copies with no result pile should still disappear");
		DualcastUpgradeRune rune = new();
		Expect(!rune.GrantsCardOnPickup, "Dualcast Upgrade should not grant a card when obtained");
		Expect(!rune.HasUponPickupEffect, "Dualcast Upgrade should not advertise a pickup effect");
	}

	private static void DeathWarrantTriggersPoisonEveryEightDraws()
	{
		MethodInfo availability = typeof(DeathWarrantRune).GetMethod(nameof(HextechRelicBase.IsAvailableForPlayer))
			?? throw new MissingMethodException(nameof(DeathWarrantRune), nameof(HextechRelicBase.IsAvailableForPlayer));
		Equal(typeof(DeathWarrantRune), availability.DeclaringType, "Death Warrant should override the player availability gate");
		Expect(
			PatchProcessor.GetOriginalInstructions(availability)
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.Name == "IsSilentPlayer"),
			"Death Warrant availability should use the Silent character gate");
		Equal(8, DeathWarrantRune.CardsNeeded, "Death Warrant draw threshold");
		Equal(0, DeathWarrantRune.ResolveThresholdCrossings(0, 7), "Death Warrant should wait for eight draws");
		Equal(1, DeathWarrantRune.ResolveThresholdCrossings(7, 8), "Death Warrant should trigger on the eighth draw");
		Equal(0, DeathWarrantRune.ResolveThresholdCrossings(8, 15), "Death Warrant should preserve progress after triggering");
		Equal(2, DeathWarrantRune.ResolveThresholdCrossings(8, 24), "Death Warrant should recover every missed threshold after load or network delay");

		MethodInfo trigger = typeof(DeathWarrantRune).GetMethod(
			"TriggerPoisonCompat",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(DeathWarrantRune), "TriggerPoisonCompat");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(trigger)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Equal(typeof(PoisonPower), trigger.GetParameters()[0].ParameterType, "Death Warrant poison trigger target type");
		Expect(
			calls.Any(static method => method.Name == nameof(PoisonPower.AfterSideTurnStart)),
			"Death Warrant should use the Poison turn-start path shared by both supported game versions");
	}

	private static void MadScientistOrbLayoutOnlyTweensFirstTen()
	{
		Equal(10, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 11, 11), "the eleventh Mad Scientist orb should skip layout tweening");
		Equal(10, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 40, 40), "Mad Scientist tween work should stay capped as slots grow");
		Equal(7, HextechPlayerRuneHooks.ResolveTweenedOrbCount(true, 40, 7), "the first ten visible slots should keep their normal tween");
		Equal(11, HextechPlayerRuneHooks.ResolveTweenedOrbCount(false, 11, 11), "non-Mad Scientist large layouts should keep existing tween behavior");

		MethodInfo layout = typeof(HextechPlayerRuneHooks).GetMethod(
			"OrbTweenLayoutPrefixCore",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechPlayerRuneHooks), "OrbTweenLayoutPrefixCore");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(layout)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(Engine) && method.Name == nameof(Engine.GetProcessFrames)),
			"Mad Scientist orb layout should coalesce duplicate work within one process frame");
		Expect(
			calls.Any(static method => method.Name == "set_Position"),
			"overflow orbs should move directly to their unchanged layout target");
	}

	private static void MyriadSwordsUsesShuffleTriggerInsteadOfTurnEnd()
	{
		MethodInfo[] declaredMethods = typeof(MyriadSwordsRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

		Expect(declaredMethods.Any(method => method.Name == "AfterShuffle"), "Myriad Swords should trigger after the owner's draw pile is shuffled");
		Expect(declaredMethods.All(method => method.Name != "BeforeTurnEnd"), "Myriad Swords should no longer trigger at turn end");
	}

	private static void MyriadSwordsExplicitlyClosesAStalePlayPile()
	{
		MethodInfo afterShuffle = typeof(MyriadSwordsRune).GetMethod(
			"AfterShuffle",
			BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(MyriadSwordsRune), "AfterShuffle");
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(GetAsyncStateMachineMoveNext(afterShuffle))
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.Any(static method => method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Add)),
			"Myriad Swords should explicitly move a lethal autoplay card out of the Play pile");
	}

	private static void SovereignBladeVfxSyncUsesVanillaForgeScale()
	{
		Expect(Math.Abs(0.9f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(0)) < 0.0001f, "zero-damage blade scale");
		Expect(Math.Abs(0.955f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(10)) < 0.0001f, "base blade scale");
		Expect(Math.Abs(2f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(200)) < 0.0001f, "fully scaled blade");
		Expect(Math.Abs(2f - HextechSovereignBladeVfxSync.GetNormalScaleForDamage(999)) < 0.0001f, "blade scale cap");
	}

	private static void SlowCookVfxUsesDedicatedPressureCookerTextures()
	{
		string[] slowCookPaths =
		[
			HextechAssets.SlowCookHeatGlowPath,
			HextechAssets.SlowCookAoeGradientPath,
			HextechAssets.SlowCookAoeGradientSubtlePath,
			HextechAssets.SlowCookAoeEdgePath,
			HextechAssets.SlowCookAoePolarPath,
			HextechAssets.SlowCookEdgeAccentPath,
			HextechAssets.SlowCookGroundRingPath,
			HextechAssets.SlowCookFlameNoisePath,
			HextechAssets.SlowCookInnerFirePath,
			HextechAssets.SlowCookInnerFireBPath,
			HextechAssets.SlowCookFlarePath
		];

		Expect(
			slowCookPaths.All(static path => path.StartsWith("res://HextechRunes/images/effects/slow_cook/", StringComparison.Ordinal)),
			"Slow Cook VFX should load only its dedicated Pressure Cooker textures");
		Expect(
			slowCookPaths.All(static path => path != HextechAssets.MikaelsBlessingAoeRunePath),
			"Slow Cook VFX must not reuse Mikael's Blessing texture");
		Equal(slowCookPaths.Length, slowCookPaths.Distinct(StringComparer.Ordinal).Count(), "Slow Cook VFX texture paths");
		Equal(800f, SlowCookAuraVisual.ResolveWidth(160f), "Slow Cook aura width for a normal player hitbox");
		Equal(800f, SlowCookAuraVisual.ResolveWidth(500f), "Slow Cook aura width should not be reduced by hitbox scaling");
		Expect(
			SlowCookAuraVisual.FlowShaderCode.Contains("anchored_gradient", StringComparison.Ordinal),
			"Slow Cook aura should retain a stationary coverage sample while its texture details move");
		Expect(
			SlowCookAuraVisual.FlowShaderCode.Contains("intensity = min(intensity, 0.90)", StringComparison.Ordinal),
			"Slow Cook aura should cap per-layer brightness spikes");
	}

	private static void CoefficientRunesStackAdditivelyWithinTheirOwnSector()
	{
		TankEngineRune tankEngine = CreateMutableTestModel<TankEngineRune>();
		tankEngine.SavedStacks = 3;
		Equal(1.18m, tankEngine.MaxHpScale, "three Tank Engine stacks should be 6% + 6% + 6%");

		FeedUpgradeRune feedUpgrade = CreateMutableTestModel<FeedUpgradeRune>();
		feedUpgrade.SavedStacks = 3;
		Equal(1.45m, feedUpgrade.MaxHpScale, "three Feed upgrade triggers should be 15% + 15% + 15%");

		NineDragonPowerRune nineDragon = CreateMutableTestModel<NineDragonPowerRune>();
		nineDragon.SavedStacks = 3;
		Equal(1.09m, nineDragon.MaxHpScale, "three Nine Dragon stacks should be 3% + 3% + 3%");
	}

	private static void CoefficientForgesShareOneAdditiveSector()
	{
		SilverAttackForge silver = CreateMutableTestModel<SilverAttackForge>();
		silver.SavedStackCount = 2;
		GoldAttackForge gold = CreateMutableTestModel<GoldAttackForge>();
		AttackForge prismatic = CreateMutableTestModel<AttackForge>();

		decimal multiplier = HextechForgeCoefficientHelper.CombineBonusFractions(
		[
			silver.DamageBonusFractionTotal,
			gold.DamageBonusFractionTotal,
			prismatic.DamageBonusFractionTotal
		]);

		Equal(1.4m, multiplier, "two silver, one gold and one prismatic attack forge should share a 40% sector");
	}

	private static void MaxHpCoefficientSectorsMultiply()
	{
		decimal multiplier = HextechMaxHpScaling.CombineScales(
			[1.35m, 1.5m, 1.18m, 1.3m],
			[7.5m, 15m, 30m]);

		Equal(4.73718375m, multiplier, "rune sectors should multiply after HP forge bonuses are added into one sector");
	}

	private static void EnemyCoefficientAddsWithinHexAndMultipliesAcrossHexes()
	{
		decimal oneHex = HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
		[
			(MonsterHexKind.TankEngine, 0.05m),
			(MonsterHexKind.TankEngine, 0.05m),
			(MonsterHexKind.TankEngine, 0.05m),
			(MonsterHexKind.TankEngine, 0.05m),
			(MonsterHexKind.TankEngine, 0.05m)
		]);
		Equal(1.25m, oneHex, "five Tank Engine contributions should add inside one enemy hex sector");

		decimal crossHex = HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
		[
			(MonsterHexKind.Goliath, 0.20m),
			(MonsterHexKind.AstralBody, 0.30m)
		]);
		Equal(1.56m, crossHex, "different enemy hex sectors should multiply");
	}

	private static void EnemyMaxHpCoefficientSectorsUseBaseHp()
	{
		decimal scale = HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
		[
			(MonsterHexKind.Goliath, 0.20m),
			(MonsterHexKind.AstralBody, 0.20m),
			(MonsterHexKind.GoldenSpatula, 0.25m),
			(MonsterHexKind.MadScientist, -0.30m)
		]);

		Equal(1.26m, scale, "enemy max HP hex sectors");
		Equal(126m, Math.Floor(100m * scale), "enemy max HP should derive once from the tracked base HP");
	}

	private static void EnemyMaxHpLegacyMigrationRecoversMixedSinglePlayerEffects()
	{
		Equal(
			100,
			HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
				currentMaxHp: 113,
				rawMonsterMaxHp: 100,
				appliedFixedBonusFractions: [0.20m, 0.30m, 0.25m],
				madScientistLossFraction: 0.30m,
				tankEngineStacks: 5),
			"legacy max HP migration should reverse fixed targets, Mad Scientist and compounded Tank Engine stacks");
		Equal(
			100,
			HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
				currentMaxHp: 130,
				rawMonsterMaxHp: 100,
				appliedFixedBonusFractions: [0.20m, 0.30m],
				madScientistLossFraction: 0m,
				tankEngineStacks: 0),
			"an old fixed target masks smaller unknown scaling, so migration should use the raw monster base");
		Equal(
			100,
			HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
				currentMaxHp: 156,
				rawMonsterMaxHp: null,
				appliedFixedBonusFractions: [0.20m, 0.30m],
				madScientistLossFraction: 0m,
				tankEngineStacks: 0),
			"legacy max HP migration should reverse chained fixed bonuses when the raw monster base is unavailable");

		int rawlessMixedBase = HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
			currentMaxHp: 110,
			rawMonsterMaxHp: null,
			appliedFixedBonusFractions: [0.20m, 0.30m],
			madScientistLossFraction: 0.30m,
			tankEngineStacks: 0);
		Equal(
			110m,
			Math.Floor(rawlessMixedBase * 1.20m * 1.30m * 0.70m),
			"rawless legacy migration should preserve the observed max HP after the new coefficient projection");
	}

	private static void EnemyMaxHpLegacyMigrationPreservesMultiplayerScaling()
	{
		Equal(
			200,
			HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
				currentMaxHp: 161,
				rawMonsterMaxHp: 100,
				appliedFixedBonusFractions: [0.20m, 0.30m],
				madScientistLossFraction: 0.30m,
				tankEngineStacks: 3),
			"legacy max HP migration should retain a multiplayer-scaled base above every old fixed target");
		Equal(
			200,
			HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
				currentMaxHp: 200,
				rawMonsterMaxHp: 100,
				appliedFixedBonusFractions: [],
				madScientistLossFraction: 0m,
				tankEngineStacks: 0),
			"a fresh externally-scaled enemy should keep its current max HP as the coefficient base");
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
		Equal(1, snapshot.MonsterHexRerollLimit, "default monster reroll limit");
		SequenceEqual(
			new[]
			{
				new HextechRarityWeights(1, 1, 1),
				new HextechRarityWeights(1, 1, 1),
				new HextechRarityWeights(1, 1, 1)
			},
			snapshot.RuneRarityWeightsByAct,
			"default rune rarity weights by act");
		Equal(snapshot.RuneRarityWeightsByAct[2], snapshot.GetRuneRarityWeightsForAct(5), "extra acts should use third-act-plus weights");
		Equal(true, snapshot.PreventConsecutiveSilverRunes, "default prevent consecutive Silver toggle");
		Equal(5, snapshot.GoldenRerollChancePercent, "default golden reroll chance");
		Equal(0, HextechRuneConfiguration.ClampGoldenRerollChancePercent(-1), "golden reroll chance lower clamp");
		Equal(100, HextechRuneConfiguration.ClampGoldenRerollChancePercent(101), "golden reroll chance upper clamp");
	}

	private static void RetiredCustomRarityModifiersAreNotInstalledIntoCustomRunUi()
	{
		SequenceEqual(
			new[]
			{
				typeof(HextechSilverRunModifier),
				typeof(HextechGoldRunModifier),
				typeof(HextechPrismaticRunModifier)
			},
			HextechCustomModelRegistry.CustomRarityModifierTypes,
			"retired custom rarity modifier models should remain registered for old runs");
		Expect(
			typeof(HextechCustomRunModifierCompatibility).GetMethod(
				"Install",
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null,
			"retired rarity modifiers should expose no custom-run UI installer");

		MethodInfo initialize = typeof(ModEntry).GetMethod(nameof(ModEntry.Initialize), BindingFlags.Static | BindingFlags.Public)
			?? throw new MissingMethodException(nameof(ModEntry), nameof(ModEntry.Initialize));
		MethodInfo[] calls = PatchProcessor.GetOriginalInstructions(initialize)
			.Select(static instruction => instruction.operand)
			.OfType<MethodInfo>()
			.ToArray();
		Expect(
			calls.All(static method => method.DeclaringType != typeof(HextechCustomRunModifierCompatibility)),
			"mod initialization should not install retired custom rarity modifier UI hooks");
	}

	private static void StuffedToRuinChallengeUsesThreeFixedActPlans()
	{
		SequenceEqual(
			new[] { typeof(StuffedToRuinChallengeModifier), typeof(DefenseCounterMasterChallengeModifier), typeof(BruteForceChallengeModifier), typeof(EightPennyGateChallengeModifier), typeof(ListlessChallengeModifier) },
			HextechCustomModelRegistry.CustomChallengeModifierTypes,
			"custom-run challenge registry");
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(StuffedToRuinChallengeModifier)),
			"challenge modifier should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.ForgottenSoul ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.PhrogParasite, MonsterHexKind.ManipulateReality ]),
			new(HextechRarityTier.Silver, [ MonsterHexKind.LeafSlime, MonsterHexKind.DizzySpinning ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(StuffedToRuinChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(StuffedToRuinChallengeModifier), 3, out _),
			"challenge should not schedule a fourth acquisition");
		HextechRunConfigurationSnapshot defaultSnapshot = HextechRuneConfiguration.GetDefaultSnapshot();
		SequenceEqual(new[] { 1, 1, 1 }, defaultSnapshot.PlayerHexCountsByAct, "challenge default player counts");
		SequenceEqual(new[] { 1, 2, 3 }, defaultSnapshot.EnemyHexCountsByAct, "challenge default enemy counts");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "challenge fixed enemy counts");
		Expect(
			defaultSnapshot.RuneRarityWeightsByAct.All(static weights => weights == new HextechRarityWeights(1, 1, 1)),
			"challenge default rarity weights should be 1:1:1 in every act");
	}

	private static void DefenseCounterMasterChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(DefenseCounterMasterChallengeModifier)),
			"defense counter challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.Exoskeleton ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.HundredRefinements, MonsterHexKind.Porcupine ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.ProteinShake, MonsterHexKind.UnmovableMountain ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(DefenseCounterMasterChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"defense counter challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"defense counter challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"defense counter challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(DefenseCounterMasterChallengeModifier), 3, out _),
			"defense counter challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "defense counter challenge fixed enemy counts");
	}

	private static void BruteForceChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(BruteForceChallengeModifier)),
			"brute force challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.Goliath ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.AstralBody, MonsterHexKind.VitalitySurge ]),
			new(HextechRarityTier.Gold, [ MonsterHexKind.StatsOnStats, MonsterHexKind.TankEngine ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(BruteForceChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"brute force challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"brute force challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"brute force challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(BruteForceChallengeModifier), 3, out _),
			"brute force challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "brute force challenge fixed enemy counts");
	}

	private static void EightPennyGateChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(EightPennyGateChallengeModifier)),
			"eight-penny gate challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.EightPennyGate ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.IGrip ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.IInspect ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(EightPennyGateChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"eight-penny gate challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"eight-penny gate challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"eight-penny gate challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(EightPennyGateChallengeModifier), 3, out _),
			"eight-penny gate challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 1, 1 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "eight-penny gate challenge fixed enemy counts");
	}

	private static void ListlessChallengeUsesThreeFixedActPlans()
	{
		Expect(
			HextechCustomModelRegistry.AllCustomModifierTypes.Contains(typeof(ListlessChallengeModifier)),
			"listless challenge should be included in saved-property model registration");

		HextechPresetChallengeActPlan[] expectedPlans =
		[
			new(HextechRarityTier.Gold, [ MonsterHexKind.MonarchsGaze ]),
			new(HextechRarityTier.Silver, [ MonsterHexKind.TheLost, MonsterHexKind.TheForgotten ]),
			new(HextechRarityTier.Prismatic, [ MonsterHexKind.LagavulinMatriarch, MonsterHexKind.MasterOfDuality ])
		];
		for (int actIndex = 0; actIndex < expectedPlans.Length; actIndex++)
		{
			Expect(
				HextechPresetChallengeRegistry.TryGetActPlan(typeof(ListlessChallengeModifier), actIndex, out HextechPresetChallengeActPlan actualPlan),
				$"listless challenge act {actIndex + 1} should exist");
			Equal(expectedPlans[actIndex].PlayerRarity, actualPlan.PlayerRarity, $"listless challenge act {actIndex + 1} player rarity");
			SequenceEqual(expectedPlans[actIndex].EnemyHexes, actualPlan.EnemyHexes, $"listless challenge act {actIndex + 1} enemy hexes");
		}

		Expect(
			!HextechPresetChallengeRegistry.TryGetActPlan(typeof(ListlessChallengeModifier), 3, out _),
			"listless challenge should not schedule a fourth acquisition");
		SequenceEqual(new[] { 1, 2, 2 }, expectedPlans.Select(static plan => plan.EnemyHexes.Count), "listless challenge fixed enemy counts");
	}

	private static void PresetChallengesArePairwiseMutuallyExclusive()
	{
		foreach (Type selectedType in HextechCustomModelRegistry.CustomChallengeModifierTypes)
		{
			foreach (Type candidateType in HextechCustomModelRegistry.CustomChallengeModifierTypes)
			{
				Equal(
					selectedType != candidateType,
					HextechPresetChallengeRegistry.AreMutuallyExclusiveChallengeTypes(selectedType, candidateType),
					$"challenge exclusivity {selectedType.Name} -> {candidateType.Name}");
			}
		}

		Expect(
			!HextechPresetChallengeRegistry.AreMutuallyExclusiveChallengeTypes(
				typeof(StuffedToRuinChallengeModifier),
				typeof(HextechSilverRunModifier)),
			"preset challenges should not untick ordinary custom-run modifiers");
	}

	private static void RunConfigurationDefaultSnapshotDisablesRiskyContent()
	{
		// 腐化树枝自配置 v16 起转为默认启用;改用长期默认禁用的逃跑计划做代表。
		string escapePlanId = ModelDb.GetId<EscapePlanRune>().Entry;
		string corruptedBranchId = ModelDb.GetId<CorruptedBranchRune>().Entry;
		string advanceToRetreatId = ModelDb.GetId<AdvanceToRetreatRune>().Entry;
		string happyAccidentId = ModelDb.GetId<HappyAccidentRune>().Entry;
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot();

		Expect(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().Contains(escapePlanId), "default player rune ids should disable escape plan");
		Expect(snapshot.DisabledPlayerRuneIds.Contains(escapePlanId), "default snapshot should disable escape plan");
		Expect(!snapshot.DisabledPlayerRuneIds.Contains(advanceToRetreatId), "default snapshot should enable Advance to Retreat");
		Expect(!snapshot.DisabledPlayerRuneIds.Contains(happyAccidentId), "default snapshot should enable Happy Accident");
		foreach (Type runeType in new[] { typeof(OmegaRune), typeof(OkBoomerangRune), typeof(FeyMagicRune), typeof(PorcupineRune), typeof(AstralBodyRune) })
		{
			Expect(
				snapshot.DisabledPlayerRuneIds.Contains(ModelDb.GetId(runeType).Entry),
				$"default snapshot should disable {runeType.Name}");
		}
		Expect(!snapshot.DisabledPlayerRuneIds.Contains(corruptedBranchId), "corrupted branch should be enabled by default since config v16");
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

	private static void ColorDiscoveryCandidateOrderIsPermutationInvariant()
	{
		CardModel[] candidates =
		[
			CreateMutableTestModel<SearingAttackCard>(),
			CreateMutableTestModel<FeelTheBurnCard>(),
			CreateMutableTestModel<WhiteHoleCard>()
		];
		string[] forward = ColorDiscoveryRune.OrderCandidatesForStableSelection(candidates)
			.Select(HextechStableRandom.CardKey)
			.ToArray();
		string[] reversed = ColorDiscoveryRune.OrderCandidatesForStableSelection(candidates.Reverse())
			.Select(HextechStableRandom.CardKey)
			.ToArray();

		SequenceEqual(forward, reversed, "Color Discovery candidates should ignore source enumeration order");
		SequenceEqual(
			forward.OrderBy(static key => key, StringComparer.Ordinal),
			forward,
			"Color Discovery candidates should use ordinal CardKey order");
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
		context.RuneSelectionJournal.RecordSelected(
			0,
			0,
			11,
			new ModelId("HEXTECH_TEST", "RESET_ME"));

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
		Expect(
			!context.RuneSelectionJournal.TryGet(0, 0, 11, out _),
			"new-run rune selection journal should reset");
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

	private static void MayhemRunContextResetForEndlessLoopPreservesStageRows()
	{
		HextechMayhemRunContext context = new();
		context.EnemyHexCounts.Set([ 1, 2, 3 ]);
		context.ActState.SetMonsterHexes(0, [ MonsterHexKind.ShrinkRay ]);
		context.ActState.SetResolved(0, true);
		context.ActState.SetMonsterHexes(1, [ MonsterHexKind.ShrinkRay, MonsterHexKind.PandorasBox ]);
		context.ActState.SetResolved(1, true);
		context.ChoiceHistory.SavedSeenPlayerRuneIdsJson = "{\"0\":[\"A\"]}";
		context.CombatTracking.EnemyProtectiveVeilTurnCounter = 9;

		context.ResetForEndlessLoop(6);

		SequenceEqual(new[] { 1, 2, 3 }, context.EnemyHexCounts.Snapshot, "endless reset should keep enemy count snapshot");
		Equal(6, context.HexCountRecoveryBaseline, "endless recovery baseline");
		Equal(3, context.MonsterHexStrengthTierFloor, "endless strength floor");
		Expect(context.IsEndlessLoopActive, "endless flag");
		Equal(3, context.ActSelectionIndexOffset, "endless reset should advance the monotonic stage index");
		Expect(context.ActState.IsResolved(1), "endless reset should preserve resolved stage history");
		IReadOnlyList<IReadOnlyList<MonsterHexKind>> existingRows = context.ActState.GetMonsterHexRows();
		Equal(2, existingRows.Count, "endless reset should preserve previous acquisition rows");
		SequenceEqual(new[] { MonsterHexKind.ShrinkRay }, existingRows[0], "first enemy-hex acquisition row");
		SequenceEqual(new[] { MonsterHexKind.PandorasBox }, existingRows[1], "second enemy-hex acquisition row");

		context.ActState.SetMonsterHexes(3, [ MonsterHexKind.ShrinkRay, MonsterHexKind.PandorasBox, MonsterHexKind.FrostWraith ]);
		context.ActState.SetResolved(3, true);
		IReadOnlyList<IReadOnlyList<MonsterHexKind>> rowsAfterNextLoop = context.ActState.GetMonsterHexRows();
		Equal(3, rowsAfterNextLoop.Count, "fourth acquisition should create a new collapse row");
		SequenceEqual(new[] { MonsterHexKind.FrostWraith }, rowsAfterNextLoop[2], "next-loop acquisition row");
		Equal("", context.ChoiceHistory.SavedSeenPlayerRuneIdsJson, "endless reset should clear seen runes");
		Equal(0, context.CombatTracking.EnemyProtectiveVeilTurnCounter, "endless reset should clear combat tracking");
	}

	private static void MayhemActStateSupportsExtraActsAndStableExtraStageIds()
	{
		HextechMayhemActState state = new();
		state.SetRarity(4, HextechRarityTier.Prismatic);
		state.SetMonsterHexes(4, [ MonsterHexKind.ShrinkRay ]);
		state.SetResolved(4, true);
		int finaleIndex = state.GetOrCreateExtraStageIndex("0:IntegratedStrategyEvents:Finale:EternalDust", 5);
		Equal(5, finaleIndex, "extra finale should be placed after real acts");
		Equal(finaleIndex, state.GetOrCreateExtraStageIndex("0:IntegratedStrategyEvents:Finale:EternalDust", 99), "extra finale identity should be stable");

		HextechMayhemActState restored = new();
		restored.SavedRarityByAct = state.SavedRarityByAct;
		restored.SavedResolvedActs = state.SavedResolvedActs;
		restored.SavedMonsterHexesByActJson = state.SavedMonsterHexesByActJson;
		restored.SavedExtraStageIndexesJson = state.SavedExtraStageIndexesJson;
		Equal(HextechRarityTier.Prismatic, restored.GetRarity(4), "extra-act rarity should round-trip");
		Expect(restored.IsResolved(4), "extra-act resolved state should round-trip");
		SequenceEqual(new[] { MonsterHexKind.ShrinkRay }, restored.GetMonsterHexes(4), "extra-act enemy hexes should round-trip");
		Equal(finaleIndex, restored.GetOrCreateExtraStageIndex("0:IntegratedStrategyEvents:Finale:EternalDust", 99), "extra finale mapping should round-trip");
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
		Equal(HextechRarityTier.Silver, metadata.GetRegistration(typeof(TerminalIllnessRune)).Rarity, "Terminal Illness rarity");
		Equal(HextechRarityTier.Silver, metadata.GetRegistration(typeof(TrickLicenseRune)).Rarity, "Trick License rarity");
		Equal(PlayerRuneCharacterPool.Silent, metadata.GetRegistration(typeof(DeathWarrantRune)).CharacterPool, "Death Warrant character pool");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.Disabled], HextechContentRegistry.DisabledPlayerRuneTypes, "default disabled runes");
		SetEqual(metadata.TypesByFlag[PlayerRuneFlags.SelectionExcluded], HextechContentRegistry.SelectionExcludedPlayerRuneTypes, "selection excluded runes");
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

	private static void NewEnemyHexesReusePlayerRuneIconsAndRarities()
	{
		MonsterHexMetadataCatalog metadata = HextechContentRegistry.MonsterHexMetadata;
		(MonsterHexKind Kind, int Value, HextechRarityTier Rarity, Type IconType)[] expected =
		[
			(MonsterHexKind.TwilightVeil, 130, HextechRarityTier.Gold, typeof(TwilightVeilRune)),
			(MonsterHexKind.Stats, 131, HextechRarityTier.Silver, typeof(StatsRune)),
			(MonsterHexKind.StatsOnStats, 132, HextechRarityTier.Gold, typeof(StatsOnStatsRune)),
			(MonsterHexKind.StatsOnStatsOnStats, 133, HextechRarityTier.Prismatic, typeof(StatsOnStatsOnStatsRune)),
			(MonsterHexKind.MiserableFate, 134, HextechRarityTier.Prismatic, typeof(MiserableFateRune))
		];

		foreach ((MonsterHexKind kind, int value, HextechRarityTier rarity, Type iconType) in expected)
		{
			Equal(value, (int)kind, $"{kind} append-only enum value");
			Expect(metadata.TryGetRegistration(kind, out MonsterHexRegistration registration), $"{kind} registration should exist");
			Equal(rarity, registration.Rarity, $"{kind} rarity");
			Equal(iconType, registration.IconRelicType, $"{kind} icon relic type");
			Expect(!registration.Disabled, $"{kind} should be enabled by default");
		}
	}

	private static void MonsterInteractionPolicyPreservesStructuralMonsterBuffs()
	{
		PowerModel[] structuralEnemyPowers =
		[
			new AdaptablePower(),
			new AsleepPower(),
			new SlumberPower(),
			new SandpitPower(),
			new BattlewornDummyTimeLimitPower(),
			new MinionPower(),
			new InfestedPower(),
		];
		foreach (PowerModel power in structuralEnemyPowers)
		{
			string name = power.GetType().Name;
			Expect(HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(power), $"{name} should be structural");
			Expect(HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power), $"{name} should survive buff removal");
			Expect(HextechMonsterInteractionPolicy.ShouldIgnoreMonsterSelfBuff(power), $"{name} should not trigger monster self-buff effects");
			Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(power), $"{name} should not be mirrored to players");
		}

		PowerModel[] enemyHostedPlayerRelations = [new BackAttackLeftPower(), new BackAttackRightPower()];
		foreach (PowerModel power in enemyHostedPlayerRelations)
		{
			string name = power.GetType().Name;
			Expect(!HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(power), $"{name} should be classified as a player relation rather than structural");
			Expect(HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power), $"{name} should survive enemy buff removal for Surrounded");
			Expect(HextechMonsterInteractionPolicy.ShouldIgnoreMonsterSelfBuff(power), $"{name} should not trigger monster self-buff effects");
			Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(power), $"{name} should not be mirrored to players");
		}

		PowerModel[] removableMonsterMechanisms =
		[
			new HatchPower(),
			new ReattachPower(),
			new HardToKillPower(),
			new WitheringPresencePower(),
			new NemesisPower(),
			new IllusionPower(),
			new SteamEruptionPower(),
		];
		foreach (PowerModel power in removableMonsterMechanisms)
		{
			string name = power.GetType().Name;
			Expect(!HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(power), $"{name} should be removable while its owner is alive");
			Expect(!HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power), $"{name} should be removable by Feel the Burn and upgraded Expose");
			Expect(HextechMonsterInteractionPolicy.ShouldIgnoreMonsterSelfBuff(power), $"{name} should keep its monster self-buff trigger restriction");
			Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(power), $"{name} should not be mirrored to players");
		}

		PowerModel[] nonEnemyPowers =
		[
			new MonologuePower(),
			new CountdownPower(),
			new TheSealedThronePower(),
			new PillarOfCreationPower(),
			new ChildOfTheStarsPower(),
			new PaleBlueDotPower(),
			new TheHuntPower(),
			new DemesnePower(),
			new UnmovablePower(),
			new OrbitPower(),
			new GuardedPower(),
			new InterceptPower(),
			new DieForYouPower(),
			new FastenPower(),
			new HauntPower(),
			new SummonNextTurnPower(),
			new StarNextTurnPower(),
#if STS2_108_OR_NEWER
			new SoulboundPower(),
#endif
		];
		foreach (PowerModel power in nonEnemyPowers)
		{
			string name = power.GetType().Name;
			Expect(!HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(power), $"{name} should not be classified as an enemy structural power");
			Expect(!HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(power), $"{name} should not be protected by enemy buff removal policy");
			Expect(!HextechMonsterInteractionPolicy.ShouldIgnoreMonsterSelfBuff(power), $"{name} should not be classified as a monster self-buff");
			Expect(!HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(power), $"{name} should not be classified as a monster mechanism");
		}

		Expect(!HextechMonsterInteractionPolicy.IsStructuralMonsterBuff(new StrengthPower()), "ordinary strength should not be structural");
		Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(new PersonalHivePower()), "personal hive should not be mirrored to players");
		Expect(HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(new HextechPlayerSlowPower()), "custom Slow should not be mirrored to players");
		Expect(!HextechMonsterInteractionPolicy.IsMonsterMechanismBuff(new StrengthPower()), "ordinary strength should remain mirrorable");
	}

	private static void BuffRemovalPreservesStolenLootPowers()
	{
		Expect(HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(new HeistPower()), "Heist should survive Feel the Burn and upgraded Expose");
		Expect(HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(new SwipePower()), "Swipe should survive Feel the Burn and upgraded Expose");
		Expect(!HextechMonsterInteractionPolicy.ShouldPreserveFromBuffRemoval(new StrengthPower()), "ordinary Strength should remain removable");
	}

	private static void EnemyHexHoverTipsUseExpectedPowerModels()
	{
		SequenceEqual(
			new[] { typeof(DisintegrationPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.Doomsday),
			"enemy Doomsday should explain Disintegration");
		SequenceEqual(
			new[] { typeof(DisintegrationPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.Omega),
			"enemy Omega should explain Disintegration");
		SequenceEqual(
			new[] { typeof(DoomPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.OminousPact),
			"enemy Ominous Pact should explain Doom");
		SequenceEqual(
			new[] { typeof(SkittishPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.PhantasmalGardener),
			"enemy Phantasmal Gardener should explain Skittish");
		SequenceEqual(
			new[] { typeof(ChainsOfBindingPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.Queen),
			"enemy Queen should explain Chains of Binding");
		SequenceEqual(
			new[] { typeof(ArtifactPower), typeof(PlatingPower), typeof(RegenPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.HailToTheKing),
			"enemy Hail to the King should explain all three powers");
		SequenceEqual(
			new[] { typeof(WeakPower), typeof(FrailPower), typeof(VulnerablePower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.OmniDragonSoul),
			"enemy Omni Dragon Soul should explain all three debuffs");
		SequenceEqual(
			new[] { typeof(TaintedPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.ArcanePunch),
			"enemy Arcane Punch should explain Tainted");
		SequenceEqual(
			new[] { typeof(HextechPlayerSlowPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.FrostWraith),
			"enemy Frost Wraith should explain Hextech Slow");

		foreach (MonsterHexKind hex in HextechContentRegistry.AllMonsterHexKinds)
		{
			IReadOnlyList<Type> powerTypes = MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(hex);
			Equal(powerTypes.Count, powerTypes.Distinct().Count(), $"enemy {hex} hover-tip power types should be unique");
			foreach (Type powerType in powerTypes)
			{
				Expect(typeof(PowerModel).IsAssignableFrom(powerType), $"enemy {hex} hover-tip type should be a power model: {powerType}");
			}
		}
	}

	private static void EnemyJeweledGauntletUsesExpectedStrengthTierChances()
	{
		Equal(10, HextechCombatHooks.GetJeweledGauntletRepeatPercent(0), "enemy Jeweled Gauntlet tier zero fallback chance");
		Equal(10, HextechCombatHooks.GetJeweledGauntletRepeatPercent(1), "enemy Jeweled Gauntlet tier one chance");
		Equal(20, HextechCombatHooks.GetJeweledGauntletRepeatPercent(2), "enemy Jeweled Gauntlet tier two chance");
		Equal(30, HextechCombatHooks.GetJeweledGauntletRepeatPercent(3), "enemy Jeweled Gauntlet tier three chance");
		Equal(30, HextechCombatHooks.GetJeweledGauntletRepeatPercent(99), "enemy Jeweled Gauntlet high-tier clamp chance");
	}

	private static void EnemyFossilStalkerUsesExpectedSuckTiers()
	{
		Equal(1, FossilStalkerEnemyHex.ResolveSuckAmount(0), "enemy Fossil Stalker tier zero fallback Suck");
		Equal(1, FossilStalkerEnemyHex.ResolveSuckAmount(1), "enemy Fossil Stalker tier one Suck");
		Equal(2, FossilStalkerEnemyHex.ResolveSuckAmount(2), "enemy Fossil Stalker tier two Suck");
		Equal(3, FossilStalkerEnemyHex.ResolveSuckAmount(3), "enemy Fossil Stalker tier three Suck");
		Equal(3, FossilStalkerEnemyHex.ResolveSuckAmount(99), "enemy Fossil Stalker high-tier clamp Suck");
	}

	private static void EnemyTungstenRodReducesEachHpLossByTier()
	{
		Equal(4m, TungstenRodEnemyHex.ReduceHpLoss(5m, 1), "enemy Tungsten Rod tier one HP loss");
		Equal(3m, TungstenRodEnemyHex.ReduceHpLoss(5m, 2), "enemy Tungsten Rod tier two HP loss");
		Equal(2m, TungstenRodEnemyHex.ReduceHpLoss(5m, 3), "enemy Tungsten Rod tier three HP loss");
		Equal(0m, TungstenRodEnemyHex.ReduceHpLoss(2m, 3), "enemy Tungsten Rod should floor HP loss at zero");
		Equal(0m, TungstenRodEnemyHex.ReduceHpLoss(0m, 3), "enemy Tungsten Rod should preserve zero HP loss");
	}

	private static void EnemySlowHexesUseExpectedBaselinesAndTiers()
	{
		HextechPlayerSlowPower slow = new();
		Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, slow.Type, "custom Slow should be neither a buff nor a debuff");
		HextechTemporarySlowPower temporarySlow =
			(HextechTemporarySlowPower)RuntimeHelpers.GetUninitializedObject(typeof(HextechTemporarySlowPower));
		Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, temporarySlow.Type, "temporary custom Slow should be neither a buff nor a debuff");
		Expect(temporarySlow.AllowNegative, "temporary custom Slow should support damage reduction stacks");
		Expect(HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Player), "temporary Slow should expire at player turn start");
		Expect(!HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Enemy), "temporary Slow should remain during enemy turn start");
		Expect(
			HextechCombatHooks.TryResolveNeutralPowerType(slow, out MegaCrit.Sts2.Core.Entities.Powers.PowerType neutralType),
			"custom Slow should bypass vanilla signed Counter classification");
		Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, neutralType, "custom Slow signed amount type");
		Expect(
			!HextechCombatHooks.TryResolveNeutralPowerType(new StrengthPower(), out _),
			"neutral classification override should not affect vanilla powers");
		Harmony neutralTypeHarmony = new("Natsuki.HextechRunes.Tests.SlowPowerType");
		neutralTypeHarmony.Patch(
			AccessTools.Method(typeof(PowerModel), nameof(PowerModel.GetTypeForAmount), [typeof(decimal)]),
			prefix: new HarmonyMethod(AccessTools.Method(typeof(HextechCombatHooks), "PowerModelGetTypeForAmountPrefix")));
		try
		{
			Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, slow.GetTypeForAmount(8m), "positive custom Slow should remain neutral");
			Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, slow.GetTypeForAmount(-8m), "negative custom Slow should remain neutral");
			Equal(MegaCrit.Sts2.Core.Entities.Powers.PowerType.None, temporarySlow.GetTypeForAmount(-8m), "negative temporary custom Slow should remain neutral");
			Equal(
				MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff,
				new StrengthPower().GetTypeForAmount(-8m),
				"neutral classification patch should preserve vanilla signed Counter behavior");
		}
		finally
		{
			neutralTypeHarmony.UnpatchAll(neutralTypeHarmony.Id);
		}
		Equal(1.08m, HextechPlayerSlowPower.ResolveDamageMultiplier(8m), "positive Slow should increase damage taken on either side");
		Equal(0.92m, HextechPlayerSlowPower.ResolveDamageMultiplier(-8m), "negative Slow should reduce damage taken on either side");
		Equal(0m, HextechPlayerSlowPower.ResolveDamageMultiplier(-120m), "negative Slow damage multiplier should floor at zero");
		Equal(3, FrostWraithEnemyHex.TurnsNeeded, "enemy Frost Wraith trigger interval");
		Equal(50, FrostWraithEnemyHex.TemporarySlowAmount, "enemy Frost Wraith temporary Slow amount");
		Expect(!FrostWraithEnemyHex.ShouldTriggerForRound(1), "enemy Frost Wraith should not trigger on round one");
		Expect(!FrostWraithEnemyHex.ShouldTriggerForRound(2), "enemy Frost Wraith should wait for three player turns");
		Expect(FrostWraithEnemyHex.ShouldTriggerForRound(3), "enemy Frost Wraith should trigger before the third enemy turn");
		Expect(FrostWraithEnemyHex.ShouldTriggerForRound(6), "enemy Frost Wraith should trigger every three rounds afterward");
		Equal(2, FrostWraithRune.TurnsNeeded, "Frost Wraith trigger interval");
		Equal(50, FrostWraithRune.TemporarySlowAmount, "Frost Wraith temporary Slow amount");
		Expect(!FrostWraithRune.ShouldTriggerForRound(1, FrostWraithRune.TurnsNeeded), "Frost Wraith should not trigger at combat start");
		Expect(!FrostWraithRune.ShouldTriggerForRound(2, FrostWraithRune.TurnsNeeded), "Frost Wraith should wait for two completed rounds");
		Expect(FrostWraithRune.ShouldTriggerForRound(3, FrostWraithRune.TurnsNeeded), "Frost Wraith should trigger after two completed rounds");
		Expect(FrostWraithRune.ShouldTriggerForRound(5, FrostWraithRune.TurnsNeeded), "Frost Wraith should trigger every two rounds afterward");
		Equal(6, CorrosionRune.TemporarySlowAmount, "Corrosion temporary Slow amount per damage event");
		Equal(3, AncientStatueEnemyHex.ResolveCardSlowGain(0), "Ancient Statue tier zero fallback Slow gain");
		Equal(3, AncientStatueEnemyHex.ResolveCardSlowGain(1), "Ancient Statue tier one Slow gain");
		Equal(5, AncientStatueEnemyHex.ResolveCardSlowGain(2), "Ancient Statue tier two Slow gain");
		Equal(8, AncientStatueEnemyHex.ResolveCardSlowGain(3), "Ancient Statue tier three Slow gain");
		Equal(8, AncientStatueEnemyHex.ResolveCardSlowGain(99), "Ancient Statue high-tier Slow gain clamp");
		Equal(-3, HundredRefinementsEnemyHex.ResolveSlowReduction(0), "Hundred Refinements tier zero fallback Slow reduction");
		Equal(-3, HundredRefinementsEnemyHex.ResolveSlowReduction(1), "Hundred Refinements tier one Slow reduction");
		Equal(-5, HundredRefinementsEnemyHex.ResolveSlowReduction(2), "Hundred Refinements tier two Slow reduction");
		Equal(-8, HundredRefinementsEnemyHex.ResolveSlowReduction(3), "Hundred Refinements tier three Slow reduction");
		Equal(-8, HundredRefinementsEnemyHex.ResolveSlowReduction(99), "Hundred Refinements high-tier Slow reduction clamp");
		MethodInfo[] ancientStatueMethods = typeof(AncientStatueEnemyHex).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		Expect(
			ancientStatueMethods.All(static method => method.Name is not nameof(AncientStatueEnemyHex.ApplyCombatStartPlayerDebuffs) and not nameof(AncientStatueEnemyHex.BeforePlayerSideTurnStart)),
			"Ancient Statue should not seed or manually reset persistent Slow");
		MethodInfo[] hundredRefinementsMethods = typeof(HundredRefinementsEnemyHex).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		Expect(
			hundredRefinementsMethods.All(static method => method.Name is not nameof(HundredRefinementsEnemyHex.ApplyCombatStartToEnemy) and not nameof(HundredRefinementsEnemyHex.BeforePlayerSideTurnStart)),
			"Hundred Refinements should not seed or manually reset persistent Slow");
	}

	private static void EnemyOpeningBuffHexesUseDedicatedReplayableHook()
	{
		Type[] openingBuffHexTypes =
		[
			typeof(ProtectiveVeilEnemyHex),
			typeof(ThornmailEnemyHex),
			typeof(SuperBrainEnemyHex),
			typeof(SkulkingColonyEnemyHex),
			typeof(UnmovableMountainEnemyHex)
		];

		foreach (Type effectType in openingBuffHexTypes)
		{
			MethodInfo[] declaredMethods = effectType.GetMethods(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			MethodInfo? openingHook = declaredMethods.SingleOrDefault(
				static method => method.Name == nameof(HextechEnemyHexEffect.ApplyOpeningCombatStartToEnemy));
			Expect(
				openingHook != null,
				$"{effectType.Name} should apply through the replayable opening combat-start hook");
			Equal(
				typeof(bool),
				openingHook!.GetParameters()[3].ParameterType,
				$"{effectType.Name} opening hook replay flag type");
			Expect(
				declaredMethods.All(static method => method.Name != nameof(HextechEnemyHexEffect.ApplyPersistentToEnemy)),
				$"{effectType.Name} should not apply to enemies added after combat start");
			Expect(
				declaredMethods.All(static method => method.Name != nameof(HextechEnemyHexEffect.ApplyCombatStartToEnemy)),
				$"{effectType.Name} should not use the generic spawned-enemy combat-start hook");
		}
	}

	private static void EnemyCorrosionAppliesFrailOnEveryUnblockedPlayerHit()
	{
		Equal(1, CorrosionEnemyHex.FrailAmount, "enemy Corrosion Frail amount");
		Expect(CorrosionEnemyHex.ShouldApplyFrail(1m, targetIsPlayer: true), "enemy Corrosion should trigger on unblocked player damage");
		Expect(!CorrosionEnemyHex.ShouldApplyFrail(0m, targetIsPlayer: true), "enemy Corrosion should ignore fully blocked damage");
		Expect(!CorrosionEnemyHex.ShouldApplyFrail(1m, targetIsPlayer: false), "enemy Corrosion should ignore non-player targets");
		SequenceEqual(
			new[] { typeof(FrailPower) },
			MonsterHexCatalog.GetEnemyHexPowerHoverTipTypes(MonsterHexKind.Corrosion),
			"enemy Corrosion should explain Frail");
		Expect(
			typeof(HextechMayhemCombatTrackingState).GetField("CorrosionProcsThisTurn") == null,
			"enemy Corrosion should not retain a per-turn proc gate");
		Expect(
			typeof(CombatTrackingSnapshot).GetProperty("CorrosionProcsThisTurn") == null,
			"enemy Corrosion snapshot should not retain the obsolete proc gate");
	}

	private static void EnemyVitalitySurgeScalesAllSustainFromMaxHp()
	{
		Equal(1m, VitalitySurgeEnemyHex.ResolveMultiplier(0m), "Vitality Surge zero-HP multiplier");
		Equal(1m, VitalitySurgeEnemyHex.ResolveMultiplier(19m), "Vitality Surge below first threshold multiplier");
		Equal(1.01m, VitalitySurgeEnemyHex.ResolveMultiplier(20m), "Vitality Surge first threshold multiplier");
		Equal(1.05m, VitalitySurgeEnemyHex.ResolveMultiplier(119m), "Vitality Surge floors partial twenty-HP steps");
		Equal(1.06m, VitalitySurgeEnemyHex.ResolveMultiplier(120m), "Vitality Surge sixth threshold multiplier");
		Equal(1.29m, VitalitySurgeEnemyHex.ResolveMultiplier(599m), "Vitality Surge multiplier below cap");
		Equal(1.30m, VitalitySurgeEnemyHex.ResolveMultiplier(600m), "Vitality Surge multiplier at cap");
		Equal(1.30m, VitalitySurgeEnemyHex.ResolveMultiplier(6000m), "Vitality Surge multiplier above cap");
	}

	private static void EnemyAttributeBoostsUseExpectedTiersAndCrossHexMultiplication()
	{
		Equal(0m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.Stats, 1), "Stats tier one bonus");
		Equal(0.05m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.Stats, 2), "Stats tier two bonus");
		Equal(0.10m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.Stats, 3), "Stats tier three bonus");
		Equal(0.05m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStats, 1), "Stats on Stats tier one bonus");
		Equal(0.10m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStats, 2), "Stats on Stats tier two bonus");
		Equal(0.15m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStats, 3), "Stats on Stats tier three bonus");
		Equal(0.10m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStatsOnStats, 1), "Stats on Stats on Stats tier one bonus");
		Equal(0.20m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStatsOnStats, 2), "Stats on Stats on Stats tier two bonus");
		Equal(0.30m, EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStatsOnStats, 3), "Stats on Stats on Stats tier three bonus");

		decimal combined = HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
		[
			(MonsterHexKind.Stats, 0.05m),
			(MonsterHexKind.Stats, 0.05m),
			(MonsterHexKind.StatsOnStats, 0.10m)
		]);
		Equal(1.21m, combined, "attribute bonuses should add within one hex and multiply across hexes");
	}

	private static void EnemyTwilightVeilMirrorsOnlyPositivePlayerBlock()
	{
		Expect(TwilightVeilEnemyHex.ShouldMirrorBlock(CombatSide.Player, 1m), "Twilight Veil should mirror positive player Block");
		Expect(!TwilightVeilEnemyHex.ShouldMirrorBlock(CombatSide.Player, 0m), "Twilight Veil should ignore zero player Block");
		Expect(!TwilightVeilEnemyHex.ShouldMirrorBlock(CombatSide.Enemy, 1m), "Twilight Veil should not recurse from enemy Block");
	}

	private static void EnemyMiserableFateUsesMissingHpDivisors()
	{
		Equal(0, MiserableFateEnemyHex.ResolveBlock(100, 97, 4), "tier one should floor fewer than four missing HP");
		Equal(1, MiserableFateEnemyHex.ResolveBlock(100, 96, 4), "tier one first block threshold");
		Equal(3, MiserableFateEnemyHex.ResolveBlock(100, 90, 3), "tier two should floor missing HP thirds");
		Equal(5, MiserableFateEnemyHex.ResolveBlock(100, 90, 2), "tier three should use two missing HP per block");
		Equal(0, MiserableFateEnemyHex.ResolveBlock(100, 120, 2), "overhealing should not grant block");
		Equal(55, MiserableFateEnemyHex.ResolveBlock(100, -10, 2), "negative HP should count as additional missing HP");
	}

	private static void EnemyHeavyHitterScalesDamageEveryFifteenMaxHp()
	{
		Equal(1m, HeavyHitterEnemyHex.ResolveMultiplier(0m), "Heavy Hitter zero-HP multiplier");
		Equal(1m, HeavyHitterEnemyHex.ResolveMultiplier(14m), "Heavy Hitter below first threshold multiplier");
		Equal(1.01m, HeavyHitterEnemyHex.ResolveMultiplier(15m), "Heavy Hitter first threshold multiplier");
		Equal(1.29m, HeavyHitterEnemyHex.ResolveMultiplier(449m), "Heavy Hitter multiplier below cap");
		Equal(1.30m, HeavyHitterEnemyHex.ResolveMultiplier(450m), "Heavy Hitter multiplier at cap");
		Equal(1.30m, HeavyHitterEnemyHex.ResolveMultiplier(4500m), "Heavy Hitter multiplier above cap");
	}

	private static void EnemyMaxHpCoefficientThresholdsScaleWithPlayerCount()
	{
		Equal(1m, HeavyHitterEnemyHex.ResolveMultiplier(29m, 2), "two-player Heavy Hitter below 30-HP threshold");
		Equal(1.01m, HeavyHitterEnemyHex.ResolveMultiplier(30m, 2), "two-player Heavy Hitter first threshold");
		Equal(1.30m, HeavyHitterEnemyHex.ResolveMultiplier(900m, 2), "two-player Heavy Hitter cap");

		Equal(1m, VitalitySurgeEnemyHex.ResolveMultiplier(39m, 2), "two-player Vitality Surge below 40-HP threshold");
		Equal(1.01m, VitalitySurgeEnemyHex.ResolveMultiplier(40m, 2), "two-player Vitality Surge first threshold");
		Equal(1.30m, VitalitySurgeEnemyHex.ResolveMultiplier(1200m, 2), "two-player Vitality Surge cap");

		Equal(1m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(9m, 2), "two-player Protein Shake below 10-HP threshold");
		Equal(1.01m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(10m, 2), "two-player Protein Shake first threshold");
		Equal(2m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(1000m, 2), "two-player Protein Shake cap");
	}

	private static void EnemyCuttingEdgeAlchemistHalvesSuccessfulPotionRolls()
	{
		Expect(HextechEnemyCuttingEdgeAlchemistHooks.ShouldKeepRolledPotion(wasForced: false, 0f), "successful potion roll should be kept below fifty percent");
		Expect(HextechEnemyCuttingEdgeAlchemistHooks.ShouldKeepRolledPotion(wasForced: false, 0.499999f), "successful potion roll should be kept just below fifty percent");
		Expect(!HextechEnemyCuttingEdgeAlchemistHooks.ShouldKeepRolledPotion(wasForced: false, 0.5f), "successful potion roll should be removed at fifty percent boundary");
		Expect(!HextechEnemyCuttingEdgeAlchemistHooks.ShouldKeepRolledPotion(wasForced: false, 0.999999f), "successful potion roll should be removed above fifty percent");
		Expect(HextechEnemyCuttingEdgeAlchemistHooks.ShouldKeepRolledPotion(wasForced: true, 0.999999f), "forced potion reward should remain guaranteed");
	}

	private static void EnemyJeweledGauntletOnlyRepeatsStandardIntentTypes()
	{
		IntentType[] repeatable =
		[
			IntentType.Attack,
			IntentType.Buff,
			IntentType.CardDebuff,
			IntentType.Debuff,
			IntentType.DebuffStrong,
			IntentType.Defend,
			IntentType.Heal,
			IntentType.StatusCard
		];
		foreach (IntentType intentType in repeatable)
		{
			Expect(
				HextechCombatHooks.IsJeweledGauntletIntentTypeRepeatable(intentType),
				$"enemy Jeweled Gauntlet should repeat {intentType}");
		}

		IntentType[] excluded =
		[
			IntentType.DeathBlow,
			IntentType.Escape,
			IntentType.Hidden,
			IntentType.Sleep,
			IntentType.Stun,
			IntentType.Summon,
			IntentType.Unknown
		];
		foreach (IntentType intentType in excluded)
		{
			Expect(
				!HextechCombatHooks.IsJeweledGauntletIntentTypeRepeatable(intentType),
				$"enemy Jeweled Gauntlet should exclude {intentType}");
		}
	}

	private static void EnemyJeweledGauntletDuplicatesWholeIntentGroup()
	{
		BuffIntent buff = new();
		DebuffIntent debuff = new();
		IReadOnlyList<AbstractIntent> duplicated =
			HextechCombatHooks.DuplicateJeweledGauntletIntentGroup([buff, debuff]);

		Equal(4, duplicated.Count, "enemy Jeweled Gauntlet duplicated intent count");
		Expect(ReferenceEquals(buff, duplicated[0]), "first intent group should retain buff");
		Expect(ReferenceEquals(debuff, duplicated[1]), "first intent group should retain debuff");
		Expect(ReferenceEquals(buff, duplicated[2]), "second intent group should repeat buff");
		Expect(ReferenceEquals(debuff, duplicated[3]), "second intent group should repeat debuff");
		Expect(
			HextechCombatHooks.AreJeweledGauntletIntentsRepeatable([buff, debuff]),
			"ordinary multi-intent move should be repeatable");
		Expect(
			!HextechCombatHooks.AreJeweledGauntletIntentsRepeatable([buff, new SummonIntent()]),
			"a special intent should exclude the whole move from repetition");
		Expect(
			!HextechCombatHooks.AreJeweledGauntletIntentsRepeatable([]),
			"an empty intent group should not be repeatable");
	}

	private static void EnemyJeweledGauntletNeverRepeatsIntoFinalKnowledgeDemonCurse()
	{
		const string curseMove = "CURSE_OF_KNOWLEDGE_MOVE";
		Expect(
			!HextechCombatHooks.WouldRepeatFinalKnowledgeDemonCurse(curseMove, 0),
			"first Knowledge Demon curse may repeat into its second stage");
		Expect(
			HextechCombatHooks.WouldRepeatFinalKnowledgeDemonCurse(curseMove, 1),
			"second-stage Knowledge Demon curse must not repeat into its third stage");
		Expect(
			HextechCombatHooks.WouldRepeatFinalKnowledgeDemonCurse(curseMove, 2),
			"third-stage Knowledge Demon curse should never repeat");
		Expect(
			HextechCombatHooks.WouldRepeatFinalKnowledgeDemonCurse(curseMove, 3),
			"out-of-range Knowledge Demon curse should remain guarded");
		Expect(
			!HextechCombatHooks.WouldRepeatFinalKnowledgeDemonCurse("SLAP_MOVE", 2),
			"other Knowledge Demon moves should remain repeatable");
	}

	private static void EnemyJeweledGauntletSkipsTheInsatiableOpeningMove()
	{
		Expect(
			HextechCombatHooks.IsTheInsatiableOpeningMove("LIQUIFY_GROUND_MOVE"),
			"The Insatiable opening move should never repeat");
		Expect(
			!HextechCombatHooks.IsTheInsatiableOpeningMove("THRASH_MOVE"),
			"later The Insatiable moves should remain repeatable");
	}

	private static void EnemyJeweledGauntletSkipsMonsterRevivalMoves()
	{
		Expect(
			HextechCombatHooks.IsMonsterRevivalMove("RESPAWN_MOVE"),
			"Test Subject respawn should never repeat");
		Expect(
			HextechCombatHooks.IsMonsterRevivalMove("REVIVE_MOVE"),
			"Illusion revive should never repeat");
		Expect(
			!HextechCombatHooks.IsMonsterRevivalMove("HEAL_MOVE"),
			"ordinary healing moves should remain repeatable");
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

	private static void EnemyCompensationDefersHalfDamageRoundedDown()
	{
		Equal((0m, 0), CompensationEnemyHex.SplitDamage(0m), "zero damage split");
		Equal((1m, 0), CompensationEnemyHex.SplitDamage(1m), "one damage stays immediate");
		Equal((1m, 1), CompensationEnemyHex.SplitDamage(2m), "even damage splits evenly");
		Equal((2m, 1), CompensationEnemyHex.SplitDamage(3m), "odd damage rounds the deferred half down");
		Equal((3m, 2), CompensationEnemyHex.SplitDamage(5m), "five damage preserves total after split");
		Equal((3.5m, 2), CompensationEnemyHex.SplitDamage(5.5m), "fractional damage preserves its immediate remainder");
		Equal((500m, 499), CompensationEnemyHex.SplitDamage(999m), "large odd damage split");
	}

	private static void PlayerCompensationRequiresActiveCombatContext()
	{
		Expect(
			CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: true, combatStateMatchesRun: true),
			"Compensation should replace damage during the active combat it belongs to");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: false, currentRoomIsCombat: true, combatStateMatchesRun: true),
			"Compensation should not replace event or other out-of-combat damage");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: false, combatStateMatchesRun: true),
			"Compensation should require the current room to be a combat room");
		Expect(
			!CompensationRune.IsActiveCombatContext(combatInProgress: true, currentRoomIsCombat: true, combatStateMatchesRun: false),
			"Compensation should reject stale combat state from another run");
	}

	private static void NextTurnDamageUsesTurnStartSnapshot()
	{
		Equal(0, HextechNextTurnDamagePower.GetDamageToResolve(5, 0), "new stacks should not resolve during the turn they are applied");
		Equal(5, HextechNextTurnDamagePower.GetDamageToResolve(5, 5), "all stacks present at turn start should resolve");
		Equal(5, HextechNextTurnDamagePower.GetDamageToResolve(8, 5), "stacks added during turn-start hooks should wait for the following turn");
		Equal(3, HextechNextTurnDamagePower.GetDamageToResolve(3, 5), "resolution should never exceed the current amount");
		Equal(0, HextechNextTurnDamagePower.GetDamageToResolve(-1, 5), "negative amounts should never deal damage");
	}

	private static void NextTurnDamageDoesNotRetriggerCompensation()
	{
		Expect(!HextechNextTurnDamagePower.IsResolvingDamage, "next-turn damage guard should start inactive");
		Expect(!CompensationEnemyHex.ShouldSkipDamageReplacement(), "ordinary damage should remain eligible for compensation");

		bool skippedDuringResolution = false;
		HextechNextTurnDamagePower.RunWithDamageResolutionGuard(() =>
		{
			skippedDuringResolution = CompensationEnemyHex.ShouldSkipDamageReplacement();
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedDuringResolution, "next-turn damage must bypass compensation instead of being delayed again");
		Expect(!HextechNextTurnDamagePower.IsResolvingDamage, "next-turn damage guard should reset after guarded work");
	}

	private static void UniversalScopeChancesAddBeforeSingleRoll()
	{
		Equal(15, UniversalScopeRuneBase.CombineChancePercent([ 15 ]), "one scope keeps its own chance");
		Equal(45, UniversalScopeRuneBase.CombineChancePercent([ 15, 30 ]), "two scope chances add directly");
		Equal(95, UniversalScopeRuneBase.CombineChancePercent([ 15, 30, 50 ]), "all scope chances add directly");
		Equal(100, UniversalScopeRuneBase.CombineChancePercent([ 50, 50, 30 ]), "combined chance is capped at certainty");
	}

	private static void WatchOutGrapefruitFoodPoolHonorsCharacterAndUniqueRelics()
	{
		IReadOnlyList<Type> commonPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: false,
			hasIceCream: false,
			hasNutritiousSoup: false);
		Type[] requestedCommonRelics =
		[
			typeof(ChosenCheese),
			typeof(LastingCandy),
			typeof(NutritiousSoup),
			typeof(BoneTea),
			typeof(EmberTea)
		];
		foreach (Type relicType in requestedCommonRelics)
		{
			Expect(commonPool.Contains(relicType), $"common food pool should contain {relicType.Name}");
		}
		Expect(!commonPool.Contains(typeof(LunarPastry)), "non-Regent food pool should exclude Lunar Pastry");
		Equal(commonPool.Count, commonPool.Distinct().Count(), "common food pool should not contain duplicate relic types");

		IReadOnlyList<Type> regentPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: false,
			hasNutritiousSoup: false);
		Expect(regentPool.Contains(typeof(LunarPastry)), "Regent food pool should contain Lunar Pastry");
		Equal(commonPool.Count + 1, regentPool.Count, "Regent food pool should add only Lunar Pastry");

		IReadOnlyList<Type> iceCreamOwnedPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: true,
			hasNutritiousSoup: false);
		Expect(!iceCreamOwnedPool.Contains(typeof(IceCream)), "owned Ice Cream should stay excluded");
		Expect(iceCreamOwnedPool.Contains(typeof(LunarPastry)), "Ice Cream exclusion should keep Regent Lunar Pastry");
		Equal(regentPool.Count - 1, iceCreamOwnedPool.Count, "owning Ice Cream should remove exactly one candidate");

		IReadOnlyList<Type> nutritiousSoupOwnedPool = WatchOutGrapefruitRune.BuildFoodRelicCandidates(
			isRegent: true,
			hasIceCream: false,
			hasNutritiousSoup: true);
		Expect(!nutritiousSoupOwnedPool.Contains(typeof(NutritiousSoup)), "owned Nutritious Soup should stay excluded");
		Expect(nutritiousSoupOwnedPool.Contains(typeof(IceCream)), "Nutritious Soup exclusion should keep Ice Cream");
		Expect(nutritiousSoupOwnedPool.Contains(typeof(LunarPastry)), "Nutritious Soup exclusion should keep Regent Lunar Pastry");
		Equal(regentPool.Count - 1, nutritiousSoupOwnedPool.Count, "owning Nutritious Soup should remove exactly one candidate");
	}

	private static void UniversalScopeUpgradeRestorationKeepsCapturedLevels()
	{
		Equal(3, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(0, 3, 30), "restore all lost multi-upgrade levels");
		Equal(2, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(1, 3, 30), "restore only missing levels");
		Equal(0, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(3, 3, 30), "preserve an unchanged card");
		Equal(0, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(4, 3, 30), "never downgrade a card that gained levels while moving");
		Equal(1, CardTransformUpgradeHelper.GetUpgradeRestorationSteps(0, 3, 1), "respect the card max upgrade level");
	}

	private static void EnemyCompensationSkipsOutbreakPoisonResponse()
	{
		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithOutbreakPowerPoisonResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement();
			return Task.CompletedTask;
		}).GetAwaiter().GetResult();

		Expect(skippedInsideGuard, "outbreak poison response damage should skip compensation replacement");
		Expect(!HextechCombatHooks.IsResolvingOutbreakPowerPoisonResponse, "outbreak response guard should reset after guarded work");
	}

	private static void EnemyCompensationSkipsSleightOfFleshResponse()
	{
		Expect(!HextechCombatHooks.IsResolvingSleightOfFleshPowerDebuffResponse, "sleight response guard should start inactive");
		Expect(
			!CompensationEnemyHex.ShouldSkipDamageReplacement(),
			"ordinary unpowered damage with dealer should still be eligible for compensation replacement");

		bool skippedInsideGuard = false;
		HextechCombatHooks.RunWithSleightOfFleshPowerDebuffResponseGuard(() =>
		{
			skippedInsideGuard = CompensationEnemyHex.ShouldSkipDamageReplacement();
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

	private static void PactsEndUpgradeDamageScalesWithExhaustPile()
	{
		Equal(0m, PactsEndUpgradeRune.CalculateBonusDamage(0, 6m), "empty exhaust pile bonus");
		Equal(30m, PactsEndUpgradeRune.CalculateBonusDamage(5, 6m), "five-card exhaust pile bonus");
		Equal(0m, PactsEndUpgradeRune.CalculateBonusDamage(-1, 6m), "negative exhaust count clamps");
	}

	private static void BrandUpgradeDamageScalesWithPermanentPlayCount()
	{
		Equal(3, BrandUpgradeRune.DamagePercentPerBrand, "Brand damage percent per play");
		Equal(1m, BrandUpgradeRune.CalculateDamageMultiplier(0, BrandUpgradeRune.DamagePercentPerBrand), "zero brand plays");
		Equal(1.03m, BrandUpgradeRune.CalculateDamageMultiplier(1, BrandUpgradeRune.DamagePercentPerBrand), "one brand play");
		Equal(1.30m, BrandUpgradeRune.CalculateDamageMultiplier(10, BrandUpgradeRune.DamagePercentPerBrand), "ten brand plays");
	}

	private static void BigHammerForgeBonusAvoidsHammerTimeDoubleScaling()
	{
		Equal(15m, BigHammerRune.CalculateForgeAmount(10m, 50m, sourceAlreadyIncludesBonus: false), "direct forge bonus");
		Equal(15m, BigHammerRune.CalculateForgeAmount(15m, 50m, sourceAlreadyIncludesBonus: true), "hammer time propagated forge");
	}

	private static void HundredRefinementsRequiresTwoBodyForges()
	{
		var rune = new HundredRefinementsRune();
		Equal(2, rune.DynamicVars["BodyForges"].IntValue, "Hundred Refinements body forge requirement");
	}

	private static void HastyScribbleDrawsToFullHandAtTurnStart()
	{
		Equal(CardPile.MaxCardsInHand, HastyScribbleRune.CalculateCardsToDraw(0), "empty hand draw");
		Equal(6, HastyScribbleRune.CalculateCardsToDraw(4), "partially filled hand draw");
		Equal(0, HastyScribbleRune.CalculateCardsToDraw(CardPile.MaxCardsInHand), "full hand draw");
		Equal(0, HastyScribbleRune.CalculateCardsToDraw(CardPile.MaxCardsInHand + 1), "overfull hand draw");
	}

	private static void BigHandsIncreasesSummonAmountByFiftyPercent()
	{
		Equal(1.5m, BigHandsRune.SummonMultiplier, "Big Hands summon multiplier");
		Equal(15m, BigHandsRune.CalculateSummonAmount(10m), "Big Hands summon amount");
	}

	private static void SpinToWinRecognizesSupportedDelayedResources()
	{
		Expect(SpinToWinRune.IsConvertiblePower(new DrawCardsNextTurnPower()), "next-turn draw should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new EnergyNextTurnPower()), "next-turn energy should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new SummonNextTurnPower()), "next-turn summon should convert");
		Expect(SpinToWinRune.IsConvertiblePower(new StarNextTurnPower()), "next-turn stars should convert");
		Expect(!SpinToWinRune.IsConvertiblePower(new StrengthPower()), "unrelated powers should remain unchanged");
	}

	private static void NewCardUpgradeRunesUseExpectedTriggerRules()
	{
		Equal(0, ThornmailRune.CalculateThorns(19m), "Thornmail should floor partial Max HP steps");
		Equal(1, ThornmailRune.CalculateThorns(20m), "Thornmail should grant one Thorns per twenty Max HP");
		Equal(4, ThornmailRune.CalculateThorns(99m), "Thornmail should have no legacy bonus cap");
		Expect(CorrosiveWaveUpgradeRune.ShouldExhaust(new CorrosiveWave(), PileType.Discard), "Corrosive Wave should move to the Exhaust pile after play");
		Expect(!CorrosiveWaveUpgradeRune.ShouldExhaust(new CorrosiveWave(), PileType.None), "ephemeral Corrosive Wave copies should keep the None result pile");
		Expect(!CorrosiveWaveUpgradeRune.ShouldExhaust(new StrikeIronclad(), PileType.Discard), "Corrosive Wave upgrade should not exhaust other cards");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Power, hasUpgradeRune: false), "vanilla Storm should still trigger for Power cards");
		Expect(!StormUpgradeRune.ShouldTrigger(CardType.Attack, hasUpgradeRune: false), "vanilla Storm should ignore Attacks");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Attack, hasUpgradeRune: true), "upgraded Storm should trigger for Attacks");
		Expect(StormUpgradeRune.ShouldTrigger(CardType.Skill, hasUpgradeRune: true), "upgraded Storm should trigger for Skills");
		Expect(ReanimateUpgradeRune.ShouldCountDeath(wasRemovalPrevented: false), "Reanimate should count Minion and Small Hand deaths like Melancholy");
		Expect(!ReanimateUpgradeRune.ShouldCountDeath(wasRemovalPrevented: true), "Reanimate should ignore a death that was prevented");
		Equal(7, BodySlamUpgradeRune.CalculateFisticuffsBlock(7, 0), "Body Slam should count total damage like Fisticuffs");
		Equal(10, BodySlamUpgradeRune.CalculateFisticuffsBlock(7, 3), "Body Slam should add overkill damage like Fisticuffs");
		Equal(7, WroughtInWarUpgradeRune.CalculateFisticuffsBlock(7, 0), "Wrought in War should count total damage like Fisticuffs");
		Equal(10, WroughtInWarUpgradeRune.CalculateFisticuffsBlock(7, 3), "Wrought in War should add overkill damage like Fisticuffs");
		Expect(DecisionsDecisionsUpgradeRune.CanSelectCard(isUnplayable: false), "Decisions should allow playable cards of any type");
		Expect(!DecisionsDecisionsUpgradeRune.CanSelectCard(isUnplayable: true), "Decisions should still reject Unplayable cards");
		Equal(3, DecisionsDecisionsUpgradeRune.AddRequestedPlayCount(1, 3), "Decisions should resolve all three plays inside one card-play wrapper");
		Equal(4, DecisionsDecisionsUpgradeRune.AddRequestedPlayCount(2, 3), "Decisions replay count should combine additively with another replay");
	}

	private static void HiddenGemUpgradeMovesNewReplayTargetToHand()
	{
		StrikeIronclad target = CreateMutableTestModel<StrikeIronclad>();
		Expect(
			HiddenGemUpgradeRune.IsEligibleReplayTarget(target),
			"Hidden Gem should accept a playable card without Replay");

		target.BaseReplayCount = 1;
		Expect(
			!HiddenGemUpgradeRune.IsEligibleReplayTarget(target),
			"Hidden Gem should retain the vanilla restriction against cards that already have Replay");
		Equal(PileType.Hand, HiddenGemUpgradeRune.ReplayTargetPile, "Hidden Gem upgraded replay target pile");
	}

	private static void PlayerSustainRunesUseExpectedMaxHpRules()
	{
		Equal(0, DevilsDanceRune.CountMaxHpTriggers(0, 2, 3), "Devil's Dance should wait for three Attacks");
		Equal(1, DevilsDanceRune.CountMaxHpTriggers(2, 3, 3), "Devil's Dance should trigger on the third Attack");
		Equal(2, DevilsDanceRune.CountMaxHpTriggers(2, 7, 3), "Devil's Dance should preserve thresholds across turns");
		Equal(1, AncientWineRune.CalculateHealAmount(99, 1m), "Ancient Wine should floor one-percent healing with a minimum of one");
		Equal(2, AncientWineRune.CalculateHealAmount(250, 1m), "Ancient Wine should heal one percent of Max HP");
		Equal(2, SturdyRune.CalculateHealAmount(100, 50, 2m, 50m, 5m), "Sturdy should use two percent at exactly half HP");
		Equal(5, SturdyRune.CalculateHealAmount(100, 49, 2m, 50m, 5m), "Sturdy should use five percent below half HP");
	}

	private static void CollectorUsesStrictExecuteThresholdAndSharesFlyingKickExecutions()
	{
		Equal(10m, CollectorRune.ExecutePercent, "Collector execute percent");
		Equal(20, CollectorRune.CountPerExecute, "Collector count per execute");
		Expect(
			CollectorRune.IsBelowExecuteThreshold(9.99m, 100m, CollectorRune.ExecutePercent),
			"Collector should execute below ten percent max HP");
		Expect(
			!CollectorRune.IsBelowExecuteThreshold(10m, 100m, CollectorRune.ExecutePercent),
			"Collector should not execute at exactly ten percent max HP");
		Expect(
			!CollectorRune.IsBelowExecuteThreshold(1m, 0m, CollectorRune.ExecutePercent),
			"Collector should reject invalid max HP thresholds");

		MethodInfo[] declaredMethods = typeof(CollectorRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
		Expect(
			declaredMethods.Any(static method => method.Name == nameof(CollectorRune.AfterDamageGiven)),
			"Collector should execute from owner damage events");
		Expect(
			declaredMethods.All(static method => method.Name != nameof(CollectorRune.AfterDeath)),
			"Collector should not count unrelated enemy deaths");
		Expect(
			declaredMethods.All(static method => method.Name != nameof(CollectorRune.ModifyDamageMultiplicativeCompat)),
			"Collector should not retain its old damage multiplier");
	}

	private static void NewRuneHookTargetsMatchSupportedGameApis()
	{
#if STS2_110_OR_NEWER
		Expect(typeof(Outbreak).GetMethod(
			"OnPlay",
			BindingFlags.Instance | BindingFlags.NonPublic,
			[
				typeof(PlayerChoiceContext),
				typeof(CardPlay)
			]) != null,
			"0.110 outbreak card response guard target");
		Expect(
			HextechFormVfxSafetyHooks.ResolveAddFormVfxTarget().GetParameters()
				.Select(static parameter => parameter.ParameterType)
				.SequenceEqual([typeof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NFormVfx)]),
			"0.110 form VFX add safety target");
		Equal(
			0,
			HextechFormVfxSafetyHooks.ResolveRemoveFormVfxTarget().GetParameters().Length,
			"0.110 form VFX removal safety target arity");
#else
		Expect(typeof(OutbreakPower).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Any(method => method.Name == "AfterPowerAmountChanged"),
			"legacy outbreak power response guard target");
#endif
		Expect(typeof(PactsEnd).GetMethod("get_CanDealDamage", BindingFlags.Instance | BindingFlags.NonPublic) != null, "pacts end private condition hook target");
		Expect(typeof(CorrosiveWavePower).GetMethod(nameof(CorrosiveWavePower.AfterSideTurnEnd), BindingFlags.Instance | BindingFlags.Public) != null, "corrosive wave turn-end hook target");
		Expect(typeof(PoisonPower).GetMethod(nameof(PoisonPower.CalculateTotalDamageNextTurn), BindingFlags.Instance | BindingFlags.Public) != null, "poison preview hook target");
		Expect(typeof(OblivionPower).GetMethod(nameof(OblivionPower.AfterSideTurnEnd), BindingFlags.Instance | BindingFlags.Public) != null, "oblivion turn-end hook target");
		Expect(typeof(BodySlam).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic) != null, "body slam play hook target");
		Expect(typeof(WroughtInWar).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic) != null, "wrought in war play hook target");
		Expect(typeof(DecisionsDecisions).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic) != null, "decisions play hook target");
		Expect(typeof(CardSelectCmd).GetMethod(
			nameof(CardSelectCmd.FromHand),
			BindingFlags.Static | BindingFlags.Public,
			[
				typeof(PlayerChoiceContext),
				typeof(Player),
				typeof(CardSelectorPrefs),
				typeof(Func<CardModel, bool>),
				typeof(AbstractModel)
			]) != null,
			"decisions hand-selection hook target");
		Expect(typeof(MegaCrit.Sts2.Core.Hooks.Hook).GetMethod(
			nameof(MegaCrit.Sts2.Core.Hooks.Hook.BeforeCardPlayed),
			BindingFlags.Static | BindingFlags.Public,
			[
				typeof(ICombatState),
				typeof(CardPlay)
			]) != null,
			"form batch before-card-played hook target");
		Expect(typeof(MegaCrit.Sts2.Core.Hooks.Hook).GetMethod(
			nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed),
			BindingFlags.Static | BindingFlags.Public,
			[
				typeof(ICombatState),
				typeof(PlayerChoiceContext),
				typeof(CardPlay)
			]) != null,
			"form batch after-card-played hook target");
		Expect(typeof(MegaCrit.Sts2.Core.Hooks.Hook).GetMethod(
			nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardChangedPiles),
			BindingFlags.Static | BindingFlags.Public,
			[
				typeof(IRunState),
				typeof(ICombatState),
				typeof(CardModel),
				typeof(PileType),
				typeof(AbstractModel)
			]) != null,
			"form batch changed-piles hook target");
		Expect(typeof(CardModel).GetMethod(
			"PlayPowerCardFlyVfx",
			BindingFlags.Instance | BindingFlags.NonPublic) != null,
			"form batch power-card VFX hook target");
		Expect(typeof(PileTypeExtensions).GetMethod(
			nameof(PileTypeExtensions.GetTargetPosition),
			BindingFlags.Static | BindingFlags.Public,
			[
				typeof(PileType),
				typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard)
			]) != null,
			"form batch entry target-position hook target");
		Expect(typeof(CardModel).GetMethod(
			"GeneratePlayCount",
			BindingFlags.Instance | BindingFlags.NonPublic,
			[
				typeof(ICombatState),
				typeof(Creature)
			]) != null,
			"form batch play-count generation target");
		foreach (Type formType in new[] { typeof(DemonForm), typeof(EchoForm), typeof(ReaperForm), typeof(SerpentForm), typeof(VoidForm) })
		{
			Expect(formType.GetMethod(
				"OnPlay",
				BindingFlags.Instance | BindingFlags.NonPublic,
				[
					typeof(PlayerChoiceContext),
					typeof(CardPlay)
				]) != null,
				$"combined {formType.Name} play hook target");
		}
	}

	private static void FormVfxSafetySkipsMissingHolder()
	{
		Expect(
			!HextechFormVfxSafetyHooks.ShouldRunOriginal(hasFormVfxHolder: false),
			"form VFX should be skipped when a custom character has no holder");
		Expect(
			HextechFormVfxSafetyHooks.ShouldRunOriginal(hasFormVfxHolder: true),
			"form VFX should retain vanilla behavior when the holder exists");
	}

	private static void SymphonyOfWarPreservesDemonAndSerpentFormVfx()
	{
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Demon,
				FormVfxKind.Serpent),
			"Symphony of War should preserve Serpent Form VFX when Demon Form is added");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Serpent,
				FormVfxKind.Demon),
			"Symphony of War should preserve Demon Form VFX when Serpent Form is added");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Demon),
			"later non-Symphony forms should not erase Demon Form VFX");
		Expect(
			HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Serpent),
			"later non-Symphony forms should not erase Serpent Form VFX");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Other,
				FormVfxKind.Other),
			"non-Symphony forms should retain vanilla last-form-wins behavior");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: false,
				FormVfxKind.Demon,
				FormVfxKind.Serpent),
			"players without Symphony of War should keep vanilla replacement behavior");
		Expect(
			!HextechFormVfxSafetyHooks.ShouldPreserveExistingForSymphony(
				hasSymphonyOfWar: true,
				FormVfxKind.Demon,
				FormVfxKind.Demon),
			"reapplying a form should replace its stale same-type VFX");
	}

	private static void FormAutoPlayBatchDispatchesOneCardPlayEvent()
	{
		DemonForm firstCard = new();
		DemonForm secondCard = new();
		DemonForm outsideCard = new();
		HextechFormAutoPlayBatchState batch = new([firstCard, secondCard]);
		CardPlay firstPlay = CreateCardPlay(firstCard, playIndex: 0, playCount: 2);
		CardPlay firstReplay = CreateCardPlay(firstCard, playIndex: 1, playCount: 2);
		CardPlay secondPlay = CreateCardPlay(secondCard);
		CardPlay outsidePlay = CreateCardPlay(outsideCard);

		Expect(batch.ShouldDispatchCardPlayedHook(firstPlay), "form batch should dispatch BeforeCardPlayed for the first real play");
		Expect(batch.ShouldDispatchCardPlayedHook(firstPlay), "form batch should dispatch AfterCardPlayed for the same first play");
		Expect(!batch.ShouldDispatchCardPlayedHook(firstReplay), "form batch should suppress replay hooks after its first event");
		Expect(!batch.ShouldDispatchCardPlayedHook(secondPlay), "form batch should suppress hooks for later form cards");
		Expect(batch.ShouldDispatchCardPlayedHook(outsidePlay), "form batch should not suppress nested non-batch cards");

		using (batch.BeginPowerCardFlyVfxPreview([firstCard, secondCard]))
		{
			Expect(batch.ShouldPlayPowerCardFlyVfx(firstCard), "form batch should show the first card in its group VFX");
			Expect(batch.ShouldPlayPowerCardFlyVfx(secondCard), "form batch should show later cards in its group VFX");
		}
		Expect(!batch.ShouldPlayPowerCardFlyVfx(firstCard), "form batch should suppress the first card's built-in duplicate VFX");
		Expect(!batch.ShouldPlayPowerCardFlyVfx(secondCard), "form batch should suppress later cards' built-in duplicate VFX");
		Expect(batch.ShouldPlayPowerCardFlyVfx(outsideCard), "form batch should not suppress VFX for non-batch cards");
		Expect(!batch.ShouldDispatchCardChangedPilesHook(firstCard, PileType.Play, PileType.Play), "form batch should suppress its synthetic Play-to-Play pile event");
		Expect(batch.ShouldDispatchCardChangedPilesHook(firstCard, PileType.Hand, PileType.Play), "form batch should keep the real move into the Play pile");
		Expect(batch.ShouldDispatchCardChangedPilesHook(outsideCard, PileType.Play, PileType.Play), "form batch should not suppress pile hooks for non-batch cards");
	}

	private static void FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay()
	{
		DemonForm firstCard = new();
		DemonForm middleCard = new();
		DemonForm lastCard = new();
		DemonForm outsideCard = new();
		HextechFormAutoPlayBatchState batch = new([firstCard, middleCard, lastCard]);

		Expect(batch.TryGetHorizontalOffset(firstCard, out float firstOffset), "first form should have an entry offset");
		Expect(batch.TryGetHorizontalOffset(middleCard, out float middleOffset), "middle form should have an entry offset");
		Expect(batch.TryGetHorizontalOffset(lastCard, out float lastOffset), "last form should have an entry offset");
		Equal(-190f, firstOffset, "first form should enter left of center");
		Equal(0f, middleOffset, "middle form should enter at center");
		Equal(190f, lastOffset, "last form should enter right of center");
		Expect(!batch.TryGetHorizontalOffset(outsideCard, out _), "non-batch cards should keep the vanilla play target");
	}

	private static void FormAutoPlayBatchUsesOnePreparedFinalEffect()
	{
		DemonForm primary = new();
		DemonForm secondary = new();
		HextechFormAutoPlayBatchState batch = new([primary, secondary]);
		HextechFormCardResult result = new(null!, PileType.None, CardPilePosition.Bottom);

		batch.PrepareCombinedResolution(primary, 8m, 1, result);
		Expect(batch.ShouldUsePreparedPlayCount(primary), "combined primary should bypass a second play-count query");
		Equal(1, batch.PreparedPlayCount, "combined primary should execute its summed effect once");
		Expect(!batch.ShouldUsePreparedPlayCount(secondary), "combined secondary should not intercept unrelated play-count queries");
		Expect(batch.TryGetPreparedResult(primary, out HextechFormCardResult preparedResult), "combined primary should reuse its prepared result");
		Equal(PileType.None, preparedResult.PileType, "combined primary should preserve its prepared result pile");
		Expect(batch.TryGetCombinedAmount(primary, out decimal amount), "combined primary should expose one final effect amount");
		Equal(8m, amount, "combined final effect should use the summed form amount");
		Expect(!batch.TryGetCombinedAmount(secondary, out _), "combined effect should only replace the representative card OnPlay");

		batch.FinishCombinedResolution();
		Expect(!batch.ShouldUsePreparedPlayCount(primary), "combined state should clear after the representative finishes");
		Expect(!batch.TryGetCombinedAmount(primary, out _), "combined amount should not leak past the batch");
	}

	private static void FormAutoPlayBatchCombinesOnlyEffectNeutralEnchantments()
	{
		Expect(HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(null), "unenchanted forms should combine");
		Expect(
			HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new MegaCrit.Sts2.Core.Models.Enchantments.Clone()),
			"forms enchanted only with Clone should combine");
		Expect(
			!HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new MegaCrit.Sts2.Core.Models.Enchantments.Sharp()),
			"forms with effect-changing enchantments should keep the per-card path");
		Expect(
			!HextechFormAutoPlayHooks.IsCombinedEffectSafeEnchantment(new UniversalSpiral()),
			"forms with replay enchantments should keep the per-card path");
	}

	private static CardPlay CreateCardPlay(CardModel card, int playIndex = 0, int playCount = 1)
	{
		return new CardPlay
		{
			Card = card,
#if STS2_109_OR_NEWER
			Player = null!,
#endif
			Target = null,
			ResultPile = PileType.Discard,
			Resources = new ResourceInfo
			{
				EnergySpent = 0,
				EnergyValue = 0,
				StarsSpent = 0,
				StarValue = 0
			},
			IsAutoPlay = true,
			PlayIndex = playIndex,
			PlayCount = playCount
		};
	}

	private static void DrawYourSwordReplacesOrbEvokeWithTwoFocus()
	{
		var rune = new DrawYourSwordRune();
		Equal(2m, rune.DynamicVars["FocusPower"].BaseValue, "Draw Your Sword Focus per Evoke");

		MethodInfo[] runeMethods = typeof(DrawYourSwordRune).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		Expect(runeMethods.All(method => method.Name != nameof(DrawYourSwordRune.BeforeSideTurnStart)), "Draw Your Sword should no longer remove Orbs at enemy turn start");
		Expect(runeMethods.Any(method => method.Name == nameof(DrawYourSwordRune.ReplaceOrbEvoke)), "Draw Your Sword should replace each Orb's Evoke effect");

		MethodInfo[] hookMethods = typeof(HextechPlayerRuneHooks).GetMethods(
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Expect(hookMethods.All(method => method.Name != "OrbChannelPrefix"), "Draw Your Sword should no longer intercept Orb channeling");
		Expect(hookMethods.Any(method => method.Name == "InstallDrawYourSwordHooks"), "Draw Your Sword should install an Orb Evoke replacement hook");
		Expect(hookMethods.Any(method => method.Name == "OrbEvokePrefix"), "Draw Your Sword should intercept Orb Evoke effects");

		IReadOnlyList<MethodInfo> evokeMethods = HextechPlayerRuneHooks.FindLoadedOrbEvokeMethods();
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(OrbModel)), "Orb Evoke replacement should include the base implementation");
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(LightningOrb)), "Orb Evoke replacement should include concrete Orb implementations");
	}

	private static void EnemyOmniDragonSoulUsesPlayerTurnStart()
	{
		MethodInfo[] declaredMethods = typeof(OmniDragonSoulEnemyHex).GetMethods(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
		Expect(declaredMethods.Any(method => method.Name == "BeforePlayerSideTurnStart"), "enemy Omni Dragon Soul should apply its debuff at player turn start");
		Expect(declaredMethods.All(method => method.Name != "BeforeEnemySideTurnStart"), "enemy Omni Dragon Soul should no longer apply its debuff at enemy turn start");
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

		HextechScopedDepthGuard enteredTaskGuard = new();
		TaskCompletionSource enteredTaskGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
		bool enteredTaskActiveBeforeAwait = false;
		bool enteredTaskActiveAfterAwait = false;
		bool afterCompletionSawInactiveGuard = false;

		async Task ObserveEnteredTask()
		{
			enteredTaskActiveBeforeAwait = enteredTaskGuard.IsActive;
			await enteredTaskGate.Task;
			enteredTaskActiveAfterAwait = enteredTaskGuard.IsActive;
		}

		enteredTaskGuard.Enter();
		Task enteredTask = ObserveEnteredTask();
		Task wrappedEnteredTask = enteredTaskGuard.WrapEnteredTask(
			enteredTask,
			() =>
			{
				afterCompletionSawInactiveGuard = !enteredTaskGuard.IsActive;
				return Task.CompletedTask;
			});

		Expect(enteredTaskActiveBeforeAwait, "entered task guard should be active before the original task awaits");
		Expect(!enteredTaskGuard.IsActive, "wrapping an entered task should immediately unwind the caller context");
		enteredTaskGate.SetResult();
		wrappedEnteredTask.GetAwaiter().GetResult();
		Expect(enteredTaskActiveAfterAwait, "entered task guard should remain active after await inside the original task");
		Expect(afterCompletionSawInactiveGuard, "entered task completion callback should run after the guarded context exits");
		Expect(!enteredTaskGuard.IsActive, "entered task guard should remain inactive in the caller after completion");

		enteredTaskGuard.Enter();
		enteredTaskGuard.Enter();
		Task nestedSynchronousTask = enteredTaskGuard.WrapEnteredTask(Task.CompletedTask);
		Expect(enteredTaskGuard.IsActive, "wrapping a completed nested task should preserve the parent guard scope");
		nestedSynchronousTask.GetAwaiter().GetResult();
		enteredTaskGuard.Exit();
		Expect(!enteredTaskGuard.IsActive, "nested completed task guard should unwind exactly one depth");
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

	private static void ExternalModelIdConflictsAreRejectedBeforeRegistration()
	{
		Type playerCollisionType = typeof(BurningBlood);
		Type forgeCollisionType = typeof(Anchor);
		Equal(
			ModelDb.GetId<MegaCrit.Sts2.Core.Models.Relics.BurningBlood>(),
			ModelDb.GetId(playerCollisionType),
			"test player rune should collide with the vanilla Burning Blood ModelId");
		Equal(
			ModelDb.GetId<MegaCrit.Sts2.Core.Models.Relics.Anchor>(),
			ModelDb.GetId(forgeCollisionType),
			"test forge should collide with the vanilla Anchor ModelId");

		int registryVersion = HextechExternalContentRegistry.Version;
		int playerRuneCount = HextechExternalContentRegistry.GetPlayerRuneRegistrations().Count;
		int forgeCount = HextechExternalContentRegistry.GetForgeRegistrations().Count;
		int eventRelicCount = HextechExternalContentRegistry.GetEventRelicTypes().Count;
		InvalidOperationException universeConflict = ExpectThrows<InvalidOperationException>(
			() => HextechCatalog.EnsureExternalModelIdAvailable(playerCollisionType),
			"external ModelId validation should include vanilla model types");
		Expect(
			universeConflict.Message.Contains("same ModelId", StringComparison.Ordinal),
			"vanilla collision should come from the ModelId validator");
		ExpectThrows<InvalidOperationException>(
			() => RunBeforeSavedPropertyCacheInitialization(() =>
				HextechRunesApi.RegisterPlayerRune<BurningBlood>(HextechRarityTier.Silver)),
			"player rune API should reject a vanilla ModelId collision before registration");
		ExpectThrows<InvalidOperationException>(
			() => RunBeforeSavedPropertyCacheInitialization(() =>
				HextechRunesApi.RegisterEventRelic<BurningBlood>()),
			"event relic API should reject a vanilla ModelId collision before registration");
		ExpectThrows<InvalidOperationException>(
			() => RunBeforeSavedPropertyCacheInitialization(() =>
				HextechRunesApi.RegisterForge<Anchor>(HextechRarityTier.Gold)),
			"forge API should reject a vanilla ModelId collision before registration");
		Equal(registryVersion, HextechExternalContentRegistry.Version, "ModelId collision registry version");
		Equal(playerRuneCount, HextechExternalContentRegistry.GetPlayerRuneRegistrations().Count, "ModelId collision player rune count");
		Equal(forgeCount, HextechExternalContentRegistry.GetForgeRegistrations().Count, "ModelId collision forge count");
		Equal(eventRelicCount, HextechExternalContentRegistry.GetEventRelicTypes().Count, "ModelId collision event relic count");
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool),
				playerCollisionType),
			"colliding player rune should not enter the shared relic pool queue");
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool),
				forgeCollisionType),
			"colliding forge should not enter the shared relic pool queue");
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.EventRelicPool),
				playerCollisionType),
			"colliding event relic should not enter the event relic pool queue");

		Type existingType = typeof(ExternalRegistrationEventRelic);
		Type incomingType = typeof(ExternalRegistrationTestRune);
		Dictionary<Type, ModelId> duplicateIds = new()
		{
			[existingType] = new ModelId("HEXTECH_TEST", "DUPLICATE"),
			[incomingType] = new ModelId("HEXTECH_TEST", "DUPLICATE")
		};
		ExpectThrows<InvalidOperationException>(
			() => HextechCatalog.EnsureUniqueModelIds(
				[ existingType, incomingType ],
				type => duplicateIds[type]),
			"different external model types must not share a full ModelId");

		Dictionary<Type, ModelId> duplicateEntries = new()
		{
			[existingType] = new ModelId("EXTERNAL_A", "SAME_ENTRY"),
			[incomingType] = new ModelId("EXTERNAL_B", "SAME_ENTRY")
		};
		ExpectThrows<InvalidOperationException>(
			() => HextechCatalog.EnsureConfigurablePlayerRuneIdEntryAvailable(
				incomingType,
				[ existingType ],
				type => duplicateEntries[type]),
			"configurable external runes must reject duplicate Entry values across categories");
	}

	private static void ExternalPlayerRuneRegistrationUpdatesCatalog()
	{
		Type runeType = typeof(ExternalRegistrationTestRune);
		Expect(!HextechCatalog.IsPlayerRuneTypeVisible(runeType), "external rune should not be visible before registration");
		RunBeforeSavedPropertyCacheInitialization(() =>
			HextechRunesApi.RegisterPlayerRune<ExternalRegistrationTestRune>(
				HextechRarityTier.Gold,
				tagKey: "COMPREHENSIVE",
				assetModId: "HextechRunes.Tests"));
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
		RunBeforeSavedPropertyCacheInitialization(() =>
			HextechRunesApi.RegisterEventRelic<ExternalRegistrationEventRelic>("HextechRunes.Tests"));
		Expect(HextechContentRegistry.EventRelicTypes.Contains(relicType), "external event relic should be registered");
		RunBeforeSavedPropertyCacheInitialization(() =>
			HextechRunesApi.RegisterEventRelic<ExternalRegistrationEventRelic>("HextechRunes.Tests"));
		Equal(1, HextechExternalContentRegistry.GetEventRelicTypes().Count(type => type == relicType), "idempotent event relic registration count");
	}

	private static void ExternalForgeRegistrationUpdatesCatalog()
	{
		Type forgeType = typeof(ExternalRegistrationForge);
		Expect(!HextechContentRegistry.AllForgeTypes.Contains(forgeType), "external forge should not be registered initially");
		RunBeforeSavedPropertyCacheInitialization(() =>
			HextechRunesApi.RegisterForge<ExternalRegistrationForge>(
				HextechRarityTier.Prismatic,
				"HextechRunes.Tests"));
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
		RunBeforeSavedPropertyCacheInitialization(() =>
		{
			HextechRunesApi.RegisterSavedPropertyCarrier<ExternalRegistrationEnchantment>();
			HextechRunesApi.RegisterEnchantmentIcon<ExternalRegistrationEnchantment>(iconPath);
		});
		Equal(iconPath, HextechExternalContentRegistry.GetEnchantmentIconPath(id), "external enchantment icon path");

#if !STS2_109_OR_NEWER
		SavedProperties? props = SavedProperties.FromInternal(new ExternalRegistrationEnchantment(), id);
		Expect(
			props?.ints?.Any(static property => property.name == "PersistentCounter" && property.value == 7) == true,
			"0.107 explicit SavedProperty carrier registration should inject the property");
#endif
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

	private static void MultiplayerGameplayEntryIncludesReadableProtocolVersion()
	{
		Equal(
			"HextechRunes-0.8.1-net1",
			HextechMultiplayerCompatibilityHooks.BuildGameplayCompatibilityEntry("HextechRunes", "0.8.1"),
			"gameplay compatibility entry should expose the short network protocol version");

		string diagnosticSignature = HextechMultiplayerCompatibilityHooks.BuildModNetworkSignature(
			"HextechRunes",
			"0.8.1",
			null,
			"",
			"",
			includeSavedProperties: false);
		Expect(
			diagnosticSignature.Contains("protocol=net1", StringComparison.Ordinal),
			"diagnostic signature should expose the same network protocol version");
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

	private sealed class BurningBlood : HextechRelicBase
	{
	}

	private sealed class Anchor : HextechForgeBase
	{
	}

	private sealed class ExternalRegistrationEnchantment : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int PersistentCounter { get; set; } = 7;
	}

	private sealed class PreInitSavedPropertyCarrier : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int PreInitCounter { get; set; } = 3;
	}

	private sealed class LateSavedPropertyCarrier : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int LateCounter { get; set; } = 5;
	}

	private sealed class SameNameSavedPropertyCarrierA : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int SharedCounter { get; set; } = 1;
	}

	private sealed class SameNameSavedPropertyCarrierB : EnchantmentModel
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int SharedCounter { get; set; } = 2;
	}

	private sealed class LateExternalRegistrationRune : HextechRelicBase
	{
		[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
		private int LateExternalCounter { get; set; } = 1;
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

	private static TException ExpectThrows<TException>(Action action, string message)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException ex)
		{
			return ex;
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
