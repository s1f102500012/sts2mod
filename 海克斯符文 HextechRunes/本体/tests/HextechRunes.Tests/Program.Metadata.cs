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

	private static void IllusoryWeaponPenNibPrefixesCanReturnSkippedTask()
	{
		AssertHarmonyTaskPrefixCanReturnSkippedTask("PenNibBeforeCardPlayedPatch");
		AssertHarmonyTaskPrefixCanReturnSkippedTask("PenNibAfterCardPlayedPatch");
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
		string gameplaySignature = HextechMultiplayerDiagnostics.BuildModNetworkSignature(
			"HextechRunes",
			"0.8.1",
			null,
			"",
			"",
			includeSavedProperties: false);
		string diagnosticSignature = HextechMultiplayerDiagnostics.BuildModNetworkSignature(
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
}
