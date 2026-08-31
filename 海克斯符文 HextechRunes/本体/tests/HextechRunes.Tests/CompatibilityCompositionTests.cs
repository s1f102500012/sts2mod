using System.Reflection;
using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void StormReplacementRequiresMayhemAndUpgradeRune()
	{
		Expect(
			!HextechCombatHooks.ShouldUseHextechStormHandling(hasMayhemModifier: false, hasStormUpgradeRune: false),
			"Storm replacement should stay disabled without Mayhem or the upgrade rune");
		Expect(
			!HextechCombatHooks.ShouldUseHextechStormHandling(hasMayhemModifier: true, hasStormUpgradeRune: false),
			"Mayhem alone must preserve vanilla Storm callbacks");
		Expect(
			!HextechCombatHooks.ShouldUseHextechStormHandling(hasMayhemModifier: false, hasStormUpgradeRune: true),
			"the upgrade rune alone must preserve vanilla Storm callbacks");
		Expect(
			HextechCombatHooks.ShouldUseHextechStormHandling(hasMayhemModifier: true, hasStormUpgradeRune: true),
			"Storm replacement should run only for the upgraded Mayhem path");
	}

	private static void EntomancerFallbackIsVersionScopedAndMissingHiveOnly()
	{
		MethodInfo? prefix = typeof(HextechEncounterCompatibilityHooks).GetMethod(
			"EntomancerSpitMovePrefix",
			BindingFlags.Static | BindingFlags.NonPublic);

#if STS2_110_OR_NEWER
		Expect(
			HextechEncounterCompatibilityHooks.ShouldRunOriginalEntomancerSpitMove(hasPersonalHive: false),
			"0.110 should always use its corrected official Entomancer move");
		Equal<MethodInfo?>(null, prefix, "0.110 build should not contain the private SpitMove patch");
#else
		Expect(
			!HextechEncounterCompatibilityHooks.ShouldRunOriginalEntomancerSpitMove(hasPersonalHive: false),
			"0.107 missing-hive state should use the official 0.110 Strength fallback");
		Expect(
			HextechEncounterCompatibilityHooks.ShouldRunOriginalEntomancerSpitMove(hasPersonalHive: true),
			"0.107 should preserve the original move when Personal Hive exists");
		Expect(prefix != null, "0.107 build should contain the narrowly scoped SpitMove patch");
		Equal<MethodInfo?>(
			null,
			HextechEncounterCompatibilityHooks.TryResolveEntomancerSpitMove(
				typeof(CompatibilityEntomancerSignatureFixture),
				warnIfMissing: false),
			"0.107 missing private SpitMove target should disable only the compatibility patch");
	#endif
	}

	private static void EnemyPowerScalingDoesNotPatchOfficialModifierPipeline()
	{
		BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
		Expect(
			typeof(HextechEnemyPowerScalingHooks).GetMethod("ModifyPowerAmountGivenHookPrefix", flags) == null,
			"enemy scaling must not skip the official power-given listener aggregator");
		Expect(
			typeof(HextechEnemyPowerScalingHooks).GetMethod("ModifyPowerAmountGivenPrefix", flags) == null,
			"legacy enemy scaling must not replace a model power-given callback");
		Expect(
			typeof(HextechEnemyPowerScalingHooks).GetMethod("TryResolveModifyPowerAmountGivenTarget", flags) == null,
			"enemy scaling must not retain a global power-given target resolver");

		MethodInfo? scaledPrefix = typeof(HextechEnemyPowerScalingHooks).GetMethod(
			"GetScaledAmountForMultiplayerPrefix",
			flags);
		MethodInfo? scaledTargets = typeof(HextechEnemyPowerScalingHooks).GetMethod(
			"ResolveGetScaledAmountForMultiplayerTargets",
			flags);
		Expect(scaledPrefix != null, "exact multiplayer amount prefix should remain installed");
		Expect(scaledTargets != null, "exact multiplayer amount targets should remain resolvable");

		IEnumerable<MethodInfo> targets = (IEnumerable<MethodInfo>)(scaledTargets!.Invoke(null, null)
			?? Array.Empty<MethodInfo>());
		MethodInfo[] resolvedTargets = targets.ToArray();
		Expect(resolvedTargets.Length > 0, "supported target should expose multiplayer amount scaling methods");
		Expect(
			resolvedTargets.All(static method => method.Name == nameof(PowerModel.GetScaledAmountForMultiplayer)),
			"retained enemy scaling targets must be limited to GetScaledAmountForMultiplayer");
	}

	private static void EndlessMonsterPowerNormalizationUsesCapturedBaseAmounts()
	{
		Equal(9, HextechEndlessModeCompatibilityHooks.CalculateEndlessScaledAmount(9m, 1m), "unscaled Exoskeleton base amount");
		Equal(23, HextechEndlessModeCompatibilityHooks.CalculateEndlessScaledAmount(9m, 2.5m), "scaled Exoskeleton base amount");
		Equal(50, HextechEndlessModeCompatibilityHooks.CalculateEndlessScaledAmount(20m, 2.5m), "scaled Hardened Shell base amount");
		Equal(int.MaxValue, HextechEndlessModeCompatibilityHooks.CalculateEndlessScaledAmount(decimal.MaxValue, 2m), "overflowing power amount");

		Harmony harmony = new("Natsuki.HextechRunes.Tests.EndlessPowerOrder");
		try
		{
			HextechEndlessModeCompatibilityHooks.Install(harmony);
			MethodInfo applyPower = typeof(MegaCrit.Sts2.Core.Commands.PowerCmd).GetMethod(
				nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.Apply),
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types:
				[
					typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
					typeof(PowerModel),
					typeof(Creature),
					typeof(decimal),
					typeof(Creature),
					typeof(CardModel),
					typeof(bool)
				],
				modifiers: null)
				?? throw new MissingMethodException(nameof(MegaCrit.Sts2.Core.Commands.PowerCmd), nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.Apply));
			Patch capture = (Harmony.GetPatchInfo(applyPower)?.Prefixes.AsEnumerable() ?? Enumerable.Empty<Patch>())
				.Single(patch => patch.owner == harmony.Id && patch.PatchMethod.Name == "CaptureRawPowerAmountPrefix");
			Equal(Priority.First, capture.priority, "raw power capture priority");
			Expect(
				capture.before.Contains(HextechCombatHooks.EndlessModeHarmonyId),
				"raw power amount must be captured before EndlessMode changes it");

			MethodInfo exoskeletonAfterAdded = typeof(Exoskeleton).GetMethod(nameof(Exoskeleton.AfterAddedToRoom))
				?? throw new MissingMethodException(nameof(Exoskeleton), nameof(Exoskeleton.AfterAddedToRoom));
			Patch normalize = (Harmony.GetPatchInfo(exoskeletonAfterAdded)?.Postfixes.AsEnumerable() ?? Enumerable.Empty<Patch>())
				.Single(patch => patch.owner == harmony.Id && patch.PatchMethod.Name == "ExoskeletonAfterAddedToRoomPostfix");
			Equal(Priority.Last, normalize.priority, "monster power normalization priority");
			Expect(
				normalize.after.Contains(HextechCombatHooks.EndlessModeHarmonyId),
				"monster power normalization must run after EndlessMode's entry hook");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}

	private static void CardPlayAllowancePreservesThirdPartyDenials()
	{
		Equal(
			UnplayableReason.HasUnplayableKeyword,
			HextechCombatHooks.ResolveCardPlayAllowanceReasons(blueCandleAllows: true, grandFinaleAllows: false),
			"Blue Candle allowance reason");
		Equal(
			UnplayableReason.BlockedByCardLogic,
			HextechCombatHooks.ResolveCardPlayAllowanceReasons(blueCandleAllows: false, grandFinaleAllows: true),
			"Grand Finale allowance reason");
		Equal(
			UnplayableReason.HasUnplayableKeyword | UnplayableReason.BlockedByCardLogic,
			HextechCombatHooks.ResolveCardPlayAllowanceReasons(blueCandleAllows: true, grandFinaleAllows: true),
			"combined owned allowance reasons");

		var whiteHole = new WhiteHoleCard();
		Expect(
			!whiteHole.CanonicalKeywords.Contains(CardKeyword.Unplayable),
			"White Hole has no card-owned unplayable keyword to clear");
		PropertyInfo? whiteHoleIsPlayable = typeof(WhiteHoleCard).GetProperty(
			"IsPlayable",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Equal(typeof(CardModel), whiteHoleIsPlayable?.DeclaringType, "White Hole inherits the playable card-logic contract");
		Expect(
			typeof(WhiteHoleCard).GetMethod("AllowsPlaying", BindingFlags.Static | BindingFlags.NonPublic) == null,
			"White Hole must not reopen third-party or resource denials through a redundant compatibility allowance");

		UnplayableReason reason = UnplayableReason.HasUnplayableKeyword
			| UnplayableReason.BlockedByHook
			| UnplayableReason.EnergyCostTooHigh;
		bool result = false;
		HextechCombatHooks.ApplyCardPlayAllowance(
			ref result,
			ref reason,
			hasPreventer: false,
			UnplayableReason.HasUnplayableKeyword);
		Expect(!result, "clearing a card-owned keyword must not bypass third-party or resource denials");
		Equal(
			UnplayableReason.BlockedByHook | UnplayableReason.EnergyCostTooHigh,
			reason,
			"allowance should preserve non-owned denial flags");

		reason = UnplayableReason.BlockedByCardLogic;
		result = false;
		HextechCombatHooks.ApplyCardPlayAllowance(
			ref result,
			ref reason,
			hasPreventer: false,
			UnplayableReason.BlockedByCardLogic);
		Expect(result, "a card-owned logic denial should be clearable by its matching allowance");
		Equal(UnplayableReason.None, reason, "matching allowance should clear only its own card logic flag");

		reason = UnplayableReason.None;
		result = false;
		HextechCombatHooks.ApplyCardPlayAllowance(
			ref result,
			ref reason,
			hasPreventer: false,
			UnplayableReason.HasUnplayableKeyword | UnplayableReason.BlockedByCardLogic);
		Expect(!result, "a false result without an owned denial reason must remain false");

		reason = UnplayableReason.HasUnplayableKeyword;
		result = false;
		HextechCombatHooks.ApplyCardPlayAllowance(
			ref result,
			ref reason,
			hasPreventer: true,
			UnplayableReason.HasUnplayableKeyword);
		Expect(!result, "a third-party preventer must keep the card blocked after owned flags are cleared");
		Equal(UnplayableReason.None, reason, "owned flags may be cleared without discarding the preventer sentinel");
	}

	private static void CardPlayAllowanceAndBlockerUseSeparatePriorities()
	{
		Harmony harmony = new("Natsuki.HextechRunes.Tests.CardPlayComposition");
		MethodInfo install = typeof(HextechCombatHooks).GetMethod(
			"InstallCardPlayHooks",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechCombatHooks), "InstallCardPlayHooks");
		MethodInfo canPlay = typeof(CardModel).GetMethod(nameof(CardModel.CanPlay), Type.EmptyTypes)
			?? throw new MissingMethodException(nameof(CardModel), nameof(CardModel.CanPlay));
		MethodInfo canPlayWithReason = typeof(CardModel).GetMethod(
			nameof(CardModel.CanPlay),
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: [typeof(UnplayableReason).MakeByRefType(), typeof(AbstractModel).MakeByRefType()],
			modifiers: null)
			?? throw new MissingMethodException(nameof(CardModel), nameof(CardModel.CanPlay));

		try
		{
			install.Invoke(null, [harmony]);
			Patch[] simplePostfixes = Harmony.GetPatchInfo(canPlay) is { } simplePatchInfo
				? simplePatchInfo.Postfixes.Where(patch => patch.owner == harmony.Id).ToArray()
				: [];
			Patch[] reasonPostfixes = Harmony.GetPatchInfo(canPlayWithReason) is { } reasonPatchInfo
				? reasonPatchInfo.Postfixes.Where(patch => patch.owner == harmony.Id).ToArray()
				: [];

			Patch simpleBlocker = simplePostfixes.Single(
				patch => patch.PatchMethod.Name == "CardCanPlayBlockerPostfix");
			Patch allowance = reasonPostfixes.Single(
				patch => patch.PatchMethod.Name == "CardCanPlayAllowanceWithReasonPostfix");
			Patch reasonBlocker = reasonPostfixes.Single(
				patch => patch.PatchMethod.Name == "CardCanPlayBlockerWithReasonPostfix");

			Equal(Priority.Last, simpleBlocker.priority, "parameterless CanPlay blocker priority");
			Equal(Priority.First, allowance.priority, "reason-aware CanPlay allowance priority");
			Equal(Priority.Last, reasonBlocker.priority, "reason-aware CanPlay blocker priority");
			Expect(
				simplePostfixes.All(patch => patch.PatchMethod.Name != "CardCanPlayAllowanceWithReasonPostfix"),
				"parameterless CanPlay must inherit the reason-aware allowance instead of reopening arbitrary false results");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}

	private static void HealCompositionUsesActualHpDelta()
	{
		Equal(5m, HextechCombatHooks.CalculateActualHealAmount(20, 25), "uncapped actual heal delta");
		Equal(2m, HextechCombatHooks.CalculateActualHealAmount(28, 30), "max-HP-capped actual heal delta");
		Equal(0m, HextechCombatHooks.CalculateActualHealAmount(20, 20), "suppressed heal delta");
		Equal(0m, HextechCombatHooks.CalculateActualHealAmount(20, 15), "concurrent HP loss must not become healing");
	}

	private static void GlassCannonHealCapRunsAfterHealingMultipliers()
	{
		decimal capBeforeMultiplier = HextechCombatHooks.ClampHealAmountToCap(
			3885,
			5552,
			1m,
			GlassCannonEnemyHex.HealCapPercent);
		Equal(
			1m,
			HextechCombatHooks.ClampHealAmountToCap(
				3885,
				5552,
				capBeforeMultiplier * 2.5m,
				GlassCannonEnemyHex.HealCapPercent),
			"Hextech-first healing should be recapped after an external multiplier");
		Equal(
			1m,
			HextechCombatHooks.ClampHealAmountToCap(
				3885,
				5552,
				1m * 2.5m,
				GlassCannonEnemyHex.HealCapPercent),
			"external-first healing should stop at the same floored 70% cap");
		Equal(
			0m,
			HextechCombatHooks.ClampHealAmountToCap(3886, 5552, 100m, GlassCannonEnemyHex.HealCapPercent),
			"Glass Cannon should reject healing at its floored cap");
		MethodInfo mikaelsThresholdHook = typeof(MikaelsBlessingEnemyHex).GetMethod(
			nameof(MikaelsBlessingEnemyHex.AfterEnemyHealthThreshold),
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(MikaelsBlessingEnemyHex), nameof(MikaelsBlessingEnemyHex.AfterEnemyHealthThreshold));
		Expect(
			PatchProcessor.GetOriginalInstructions(GetAsyncStateMachineMoveNext(mikaelsThresholdHook))
				.Select(static instruction => instruction.operand)
				.OfType<MethodInfo>()
				.Any(static method => method.DeclaringType == typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd)
					&& method.Name == nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal)),
			"Mikael's Blessing healing should pass through the globally capped heal command");

		Harmony harmony = new("Natsuki.HextechRunes.Tests.GlassCannonHealOrder");
		MethodInfo install = typeof(HextechCombatHooks).GetMethod(
			"InstallHealingHooks",
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(HextechCombatHooks), "InstallHealingHooks");
		MethodInfo heal = typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd).GetMethod(
			nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal),
			BindingFlags.Static | BindingFlags.Public,
			binder: null,
			types: [typeof(Creature), typeof(decimal), typeof(bool)],
			modifiers: null)
			?? throw new MissingMethodException(nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd), nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd.Heal));

		try
		{
			install.Invoke(null, [harmony]);
			IEnumerable<Patch> prefixes = Harmony.GetPatchInfo(heal)?.Prefixes.AsEnumerable()
				?? Enumerable.Empty<Patch>();
			Patch finalCap = prefixes
				.Single(patch => patch.owner == harmony.Id && patch.PatchMethod.Name == "FinalizeGlassCannonHealCapPrefix");
			Equal(Priority.Last, finalCap.priority, "Glass Cannon final heal cap priority");
			Expect(
				finalCap.after.Contains(HextechCombatHooks.EndlessModeHarmonyId),
				"Glass Cannon final heal cap must run after EndlessMode's healing multiplier");
		}
		finally
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}

	private sealed class CompatibilityEntomancerSignatureFixture;
}
