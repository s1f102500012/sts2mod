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
			HextechPatcher.FindPatchMethod(typeof(HextechCombatHooks), "DamageBlockPatch", "Prefix") != null,
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

	private static void ColorlessCardHelperTreatsRegentGeneratedCardsAsColorless()
	{
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<SovereignBlade>()), "sovereign blade should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionStrike>()), "minion strike should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionDiveBomb>()), "minion dive bomb should count as colorless");
		Expect(HextechColorlessCardHelper.IsColorlessCard(UninitializedCard<MinionSacrifice>()), "minion sacrifice should count as colorless");
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
		Expect(HextechPatcher.FindPatchMethod(typeof(DrawYourSwordRune), "DrawYourSwordEvokePatch", "Apply") != null, "Draw Your Sword should install an Orb Evoke replacement hook");
		Expect(hookMethods.Any(method => method.Name == "OrbEvokePrefix"), "Draw Your Sword should intercept Orb Evoke effects");

		IReadOnlyList<MethodInfo> evokeMethods = HextechPlayerRuneHooks.FindLoadedOrbEvokeMethods();
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(OrbModel)), "Orb Evoke replacement should include the base implementation");
		Expect(evokeMethods.Any(method => method.DeclaringType == typeof(LightningOrb)), "Orb Evoke replacement should include concrete Orb implementations");
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
}
