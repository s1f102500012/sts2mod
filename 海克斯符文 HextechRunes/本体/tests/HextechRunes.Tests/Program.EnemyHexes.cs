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
		Expect(HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Player, roundNumber: 3, appliedRound: 2), "temporary Slow should expire at the next player turn start");
		Expect(!HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Player, roundNumber: 3, appliedRound: 3), "temporary Slow applied during this player turn start must survive the same turn (Frost Wraith)");
		Expect(!HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Enemy, roundNumber: 3, appliedRound: 2), "temporary Slow should remain during enemy turn start");
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
			prefix: new HarmonyMethod(HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "PowerTypeForAmountPatch", "Prefix")));
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
}
