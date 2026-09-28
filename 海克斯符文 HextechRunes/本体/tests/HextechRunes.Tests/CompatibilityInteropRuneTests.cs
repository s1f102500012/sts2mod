using System.Reflection;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void InteropPlayerRuneRegistrationValidatesBeforeSideEffects()
	{
		int registryVersion = HextechExternalContentRegistry.Version;
		int playerRuneCount = HextechExternalContentRegistry.GetPlayerRuneRegistrations().Count;

		Equal(1, HextechRunesInterop.ApiVersion, "interop contract version");
		Equal(HextechRarityTier.Gold, HextechRunesInterop.ParseEnumName<HextechRarityTier>(" gold ", "rarity"), "rarity names are case-insensitive");
		Equal(
			PlayerRuneFlags.FirstActExcluded | PlayerRuneFlags.ThirdActExcluded,
			HextechRunesInterop.ParsePlayerRuneFlags("FirstActExcluded, thirdactexcluded"),
			"flags are comma-separated names");
		Equal(PlayerRuneFlags.None, HextechRunesInterop.ParsePlayerRuneFlags("  "), "blank flags are None");

		ExpectThrows<ArgumentException>(
			() => RegisterInteropTestRune(typeof(CompatibilityInteropStarterRune), rarity: "Bronze"),
			"unknown rarity name");
		ExpectThrows<ArgumentException>(
			() => RegisterInteropTestRune(typeof(CompatibilityInteropStarterRune), rarity: "1"),
			"numeric rarity must not bind to an internal ordinal");
		ExpectThrows<ArgumentException>(
			() => RegisterInteropTestRune(typeof(CompatibilityInteropStarterRune), flags: "Disabled,Nope"),
			"unknown flag name");
		ExpectThrows<ArgumentException>(
			() => RegisterInteropTestRune(typeof(CompatibilityInteropStarterRune), characterPool: "Watcher"),
			"unknown character pool name");
		ExpectThrows<ArgumentException>(
			() => RegisterInteropTestRune(typeof(string)),
			"interop still requires a RelicModel");
		ExpectThrows<ArgumentException>(
			() => HextechRunesApi.RegisterPlayerRune(typeof(CompatibilityInteropStarterRune), HextechRarityTier.Silver),
			"typed API keeps its HextechRelicBase contract");

		Equal(registryVersion, HextechExternalContentRegistry.Version, "registry version after invalid interop registrations");
		Equal(playerRuneCount, HextechExternalContentRegistry.GetPlayerRuneRegistrations().Count, "player rune count after invalid interop registrations");
		Expect(
			!HextechModelPoolRegistrar.IsModelAlreadyQueuedForPool(
				typeof(MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool),
				typeof(CompatibilityInteropStarterRune)),
			"invalid interop registration must not queue the rune");
	}

	private static void InteropRelicModelRuneIsGovernedByRegistry()
	{
		Action[] restore =
		[
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneRegistrations"),
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "AssetModIdsByModelId"),
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneAvailabilityByModelId"),
			CaptureExternalRegistryVersionRestore(),
			SuppressCompatibilityWarnings(
				"external-content.non-starter-rune",
				"external-content.availability-failed",
				"external-content.availability-conflict")
		];
		try
		{
			Player first = CreateOrdinalTestPlayer(1);
			Player second = CreateOrdinalTestPlayer(2);
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropStarterRune), HextechRarityTier.Silver),
				"Compatibility.Interop",
				player => player.NetId == 1);
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropCommonRune), HextechRarityTier.Silver),
				"Compatibility.Interop");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropThrowingRune), HextechRarityTier.Silver),
				"Compatibility.Interop",
				static _ => throw new InvalidOperationException("predicate failure"));

			CompatibilityInteropStarterRune starter = new();
			CompatibilityInteropCommonRune common = new();
			CompatibilityInteropThrowingRune throwing = new();
			Expect(HextechCatalog.IsHextechCustomRelic(starter), "registry owns the RelicModel rune (natural pool and double vision)");

			RelicModel[] vanilla = [new MegaCrit.Sts2.Core.Models.Relics.Anchor(), new Vajra()];
			RelicModel[] pool = [vanilla[0], starter, common, vanilla[1], throwing];
			SequenceEqual(vanilla, HextechNaturalRelicPoolHooks.FilterNaturalRelics(pool), "natural pool drops registered runes without the base class");

			Expect(HextechCatalog.IsAvailableForPlayer(starter, first), "predicate allows the first player");
			Expect(!HextechCatalog.IsAvailableForPlayer(starter, second), "predicate excludes the second player");
			Expect(!HextechCatalog.IsAvailableForPlayer(common, first), "non-Starter external rune is never granted");
			Expect(!HextechCatalog.IsAvailableForPlayer(throwing, first), "throwing predicate excludes the rune");
			Expect(HextechCatalog.IsAvailableForPlayer(new Vajra(), second), "unregistered relics keep the old default");

			int version = HextechExternalContentRegistry.Version;
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropStarterRune), HextechRarityTier.Silver),
				"Compatibility.Interop",
				static _ => true);
			Equal(version, HextechExternalContentRegistry.Version, "second availability predicate is ignored");
			Expect(!HextechCatalog.IsAvailableForPlayer(starter, second), "first availability predicate wins");
		}
		finally
		{
			foreach (Action action in restore.Reverse())
			{
				action();
			}
		}
	}

	private static void InteropDisplayLabelsAreFirstWriterWinsAndValidated()
	{
		Action[] restore =
		[
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRuneRegistrations"),
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "AssetModIdsByModelId"),
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "PlayerRunePoolLabelKeysByModelId"),
			CaptureCompatibilityCollectionRestore(typeof(HextechExternalContentRegistry), "ConfigSectionTitleKeysByAssetModId"),
			CaptureExternalRegistryVersionRestore(),
			SuppressCompatibilityWarnings(
				"external-content.pool-label-conflict",
				"external-content.config-section-conflict")
		];
		try
		{
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropStarterRune), HextechRarityTier.Silver),
				"Compatibility.Interop");
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(typeof(CompatibilityInteropSponsorRune), HextechRarityTier.Silver),
				HextechExternalContentRegistry.SponsorPackModId);
			HextechExternalContentRegistry.RegisterPlayerRune(
				new PlayerRuneRegistration(
					typeof(CompatibilityInteropCharacterRune),
					HextechRarityTier.Silver,
					CharacterPool: PlayerRuneCharacterPool.Silent),
				"Compatibility.Interop");
			CompatibilityInteropStarterRune starter = new();
			CompatibilityInteropCharacterRune character = new();

			Equal("GENERIC", HextechCatalog.GetPlayerRunePoolKey(starter), "unlabelled external rune is generic, not the expansion pack");
			Equal("SPONSOR_PACK", HextechCatalog.GetPlayerRunePoolKey(new CompatibilityInteropSponsorRune()), "expansion pack keeps its built-in label");
			Equal("SILENT", HextechCatalog.GetPlayerRunePoolKey(character), "character runes default to the character label");

			HextechRunesInterop.SetPlayerRunePoolLabel(typeof(CompatibilityInteropStarterRune), " MY_MOD ");
			HextechRunesInterop.SetPlayerRunePoolLabel(typeof(CompatibilityInteropStarterRune), "OTHER");
			HextechRunesInterop.SetPlayerRunePoolLabel(typeof(CompatibilityInteropCharacterRune), "MY_HERO");
			Equal("MY_MOD", HextechCatalog.GetPlayerRunePoolKey(starter), "explicit label wins and the first writer is kept");
			Equal("MY_HERO", HextechCatalog.GetPlayerRunePoolKey(character), "explicit label overrides the character label");

			HextechRunesInterop.RegisterConfigSectionTitle("Compatibility.Interop", "MY_MOD_SECTION");
			HextechRunesInterop.RegisterConfigSectionTitle("Compatibility.Interop", "OTHER_SECTION");
			Equal("MY_MOD_SECTION", HextechExternalContentRegistry.GetConfigSectionTitleKey("Compatibility.Interop"), "first section title is kept");
			Equal(null, HextechExternalContentRegistry.GetConfigSectionTitleKey("Compatibility.Unknown"), "unregistered source has no custom title");

			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.SetPlayerRunePoolLabel(typeof(CompatibilityInteropThrowingRune), " "),
				"blank pool label");
			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.SetPlayerRunePoolLabel(typeof(string), "MY_MOD"),
				"pool label requires a RelicModel");
			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.RegisterConfigSectionTitle(" ", "MY_MOD_SECTION"),
				"blank section source");
			ExpectThrows<ArgumentException>(
				() => HextechRunesInterop.RegisterConfigSectionTitle("Compatibility.Other", null!),
				"missing section title key");
			Equal(null, HextechExternalContentRegistry.GetPlayerRunePoolLabel(ModelDb.GetId(typeof(CompatibilityInteropThrowingRune))), "invalid label leaves no state");
		}
		finally
		{
			foreach (Action action in restore.Reverse())
			{
				action();
			}
		}
	}

	private static void RegisterInteropTestRune(
		Type runeType,
		string rarity = "Silver",
		string? flags = null,
		string? characterPool = null)
	{
		HextechRunesInterop.RegisterPlayerRune(
			runeType,
			rarity,
			flags,
			characterPool,
			characterOrder: 0,
			tagKey: null,
			assetModId: "Compatibility.Interop",
			isAvailableForPlayer: null);
	}

	private static Action CaptureExternalRegistryVersionRestore()
	{
		FieldInfo field = typeof(HextechExternalContentRegistry).GetField("_version", BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("external registry version field should exist");
		// 版本只前进不回退：集合回滚后再递增一次，让按版本缓存的目录全部重建。
		return () => field.SetValue(null, (int)field.GetValue(null)! + 1);
	}

	private sealed class CompatibilityInteropStarterRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class CompatibilityInteropCommonRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Common;
	}

	private sealed class CompatibilityInteropThrowingRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class CompatibilityInteropSponsorRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}

	private sealed class CompatibilityInteropCharacterRune : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Starter;
	}
}
