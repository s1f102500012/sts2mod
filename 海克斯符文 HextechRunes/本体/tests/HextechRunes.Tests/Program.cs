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
			new(nameof(FormAutoPlayBatchOnlySuppressesDuplicateFlyVfx), FormAutoPlayBatchOnlySuppressesDuplicateFlyVfx),
			new(nameof(FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay), FormAutoPlayBatchOffsetsCardsBeforeTheyEnterPlay),
			new(nameof(FormAutoPlaySecondaryContributionSumsAmountTimesPlayCount), FormAutoPlaySecondaryContributionSumsAmountTimesPlayCount),
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
			new(nameof(ForgeDropChanceAdjustsLikePotionOdds), ForgeDropChanceAdjustsLikePotionOdds),
			new(nameof(PocketForgeKeepsPotionSlotsWithinFourBitSlotIndex), PocketForgeKeepsPotionSlotsWithinFourBitSlotIndex),
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
			new(nameof(PatchManifestMatchesCheckedInList), PatchManifestMatchesCheckedInList),
			new(nameof(PatchDeclarationsResolveToRealTargets), PatchDeclarationsResolveToRealTargets),
			new(nameof(StaticStateManifestMatchesCheckedInList), StaticStateManifestMatchesCheckedInList),
			new(nameof(CardPlayBlockersUseOfficialShouldPlayHook), CardPlayBlockersUseOfficialShouldPlayHook),
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
			new(nameof(SponsorStableRandomHashIsDeterministicAndSaltSensitive), SponsorStableRandomHashIsDeterministicAndSaltSensitive),
			new(nameof(RandomEnchantmentPoolExcludesDeprecatedNegativeAndMarkerTypes), RandomEnchantmentPoolExcludesDeprecatedNegativeAndMarkerTypes),
			new(nameof(RandomEnchantmentPoolLegalEnchantmentsPreserveOrderAndCanEnchant), RandomEnchantmentPoolLegalEnchantmentsPreserveOrderAndCanEnchant),
			new(nameof(SponsorCompositeEnchantmentIsReadOnlyMigrationShell), SponsorCompositeEnchantmentIsReadOnlyMigrationShell),
			new(nameof(EntropyDecreaseCollectsOnlyCardsMarkedForRemoval), EntropyDecreaseCollectsOnlyCardsMarkedForRemoval),
			new(nameof(SponsorCatalogDependencyTableIsConsistent), SponsorCatalogDependencyTableIsConsistent),
			new(nameof(DollysMirrorRelicPagesStayWithinVanillaViewport), DollysMirrorRelicPagesStayWithinVanillaViewport),
			new(nameof(AbyssalContractChoiceModelsMapToExpectedContracts), AbyssalContractChoiceModelsMapToExpectedContracts),
			new(nameof(AbyssalContractWarriorEliteThresholdGrows), AbyssalContractWarriorEliteThresholdGrows),
			new(nameof(AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters), AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters),
			new(nameof(AbyssalContractWarriorCardFilterRejectsSkillsAndPowers), AbyssalContractWarriorCardFilterRejectsSkillsAndPowers),
			new(nameof(ActualDamageHookCannotSuppressOutOfCombatCalls), ActualDamageHookCannotSuppressOutOfCombatCalls),
			new(nameof(HookReflectionRequiresExactSignatures), HookReflectionRequiresExactSignatures),
			new(nameof(SavedPropertyLateRegistrationFailsClosedOn0107WithoutPartialState), SavedPropertyLateRegistrationFailsClosedOn0107WithoutPartialState),
			new(nameof(ExternalRegistrationValidationPrecedesAllSideEffects), ExternalRegistrationValidationPrecedesAllSideEffects),
			new(nameof(ExternalResourceOwnershipIsFirstWriterWinsAndIdempotent), ExternalResourceOwnershipIsFirstWriterWinsAndIdempotent),
			new(nameof(SavedForgeRewardRestoreFiltersUnavailableExternalContent), SavedForgeRewardRestoreFiltersUnavailableExternalContent),
			new(nameof(SavedForgeRewardRestoreKeepsGoldFallbackWhenAllOptionsInvalid), SavedForgeRewardRestoreKeepsGoldFallbackWhenAllOptionsInvalid),
			new(nameof(StormReplacementRequiresMayhemAndUpgradeRune), StormReplacementRequiresMayhemAndUpgradeRune),
			new(nameof(EntomancerFallbackIsVersionScopedAndMissingHiveOnly), EntomancerFallbackIsVersionScopedAndMissingHiveOnly),
			new(nameof(EnemyPowerScalingDoesNotPatchOfficialModifierPipeline), EnemyPowerScalingDoesNotPatchOfficialModifierPipeline),
			new(nameof(EndlessMonsterPowerNormalizationUsesCapturedBaseAmounts), EndlessMonsterPowerNormalizationUsesCapturedBaseAmounts),
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

	// (PR#18)回归测试:镶宝铁拳符文曾在 ModifyCardPlayCount(引擎/UI 可能对同一次出牌重复求值)里直接调用
	// 会推进计数的 ConsumePlayerRuneProcInCombat,导致联机各端序号推进次数不一致、稳定随机结果分叉出即时断线。
	// 修复后 ModifyCardPlayCount 只应 peek(GetPlayerRuneProcsInCombat),真正消费放在每次真实出牌只触发一次的钩子里。
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

	// 「迁移链终点 == 新用户默认」双真值源守护:v15(0.8.4 出厂)默认禁用集是冻结基线,勿随注册表更新。
	// 若未来翻转某符文默认启停时只改了注册表旗标、忘了加迁移链段,此测试即红。
	// SavedProperty 属性名集合直接决定联机 net-id 布局(规范化按名排序):任何新增/改名/删除都必须是
	// 有意为之并同步更新清单文件,否则与线上旧版联机会 1014。此测试把该风险面从线上提前到 CI。
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

	private static HashSet<string> GetConfigurableRuneEntries(params HextechRarityTier[] rarities)
	{
		return rarities
			.SelectMany(HextechCatalog.GetConfigurablePlayerRuneTypesForRarity)
			.Select(static type => ModelDb.GetId(type).Entry)
			.ToHashSet(StringComparer.Ordinal);
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

	private static void AssertHarmonyTaskPrefixCanReturnSkippedTask(string patchClassName)
	{
		string methodName = $"{patchClassName}.Prefix";
		MethodInfo? method = typeof(IllusoryWeaponRune)
			.GetNestedType(patchClassName, BindingFlags.NonPublic | BindingFlags.Public)
			?.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
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
