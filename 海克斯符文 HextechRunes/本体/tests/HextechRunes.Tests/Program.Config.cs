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
	private static void ConfigMigrationForceResetsBelowV15()
	{
		(int version, IReadOnlySet<string> disabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(14, ["some-user-custom-id"]);
		Equal(33, version, "v14 config should land on current version");
		SetEqual(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().ToArray(), disabled, "v14 config should force-reset to factory defaults");
	}

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
}
