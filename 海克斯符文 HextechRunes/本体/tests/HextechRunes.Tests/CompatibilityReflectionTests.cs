using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void ColorDiscoveryRewardUsesPublicCardsAndMissingSpecialFieldKeepsOriginal()
	{
		PropertyInfo? cardsProperty = typeof(CardReward).GetProperty(
			nameof(CardReward.Cards),
			BindingFlags.Instance | BindingFlags.Public);
		Expect(cardsProperty?.GetMethod?.IsPublic == true, "CardReward.Cards must remain a public reward contract.");
		Expect(
			typeof(ColorDiscoveryCardReward).GetField("CardRewardCardsField", BindingFlags.Static | BindingFlags.NonPublic) == null,
			"Color Discovery must not cache CardReward._cards.");

		CardModel card = new StrikeIronclad();
		Equal(card, ColorDiscoveryCardReward.GetFirstOfferedCard([card]), "first public reward card");
		Equal<CardModel?>(null, ColorDiscoveryCardReward.GetFirstOfferedCard([]), "empty public reward cards");
		Equal<CardModel?>(
			null,
			ColorDiscoveryCardReward.TryGetRestoredSpecialCard(restoredReward: null, cardField: null),
			"missing SpecialCardReward field fallback");

		Reward original = (Reward)RuntimeHelpers.GetUninitializedObject(typeof(SpecialCardReward));
		Player player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		bool replaced = ColorDiscoveryCardReward.TryFromSavedSpecialCardReward(
			new SerializableReward(),
			original,
			player,
			out ColorDiscoveryCardReward? replacement,
			logFailure: false);
		Expect(!replaced, "a missing restored card must keep the already materialized SpecialCardReward");
		Equal<ColorDiscoveryCardReward?>(null, replacement, "missing restored card replacement");
		Reward finalReward = replaced ? replacement! : original;
		Expect(ReferenceEquals(original, finalReward), "failed conversion must preserve the original reward instance");
	}

	private static void ColorDiscoveryIncludesThirdPartyCharacterPools()
	{
		var ownerPool = new CompatibilityOwnerCardPool();
		var externalPool = new CompatibilityExternalCardPool();

		CardPoolModel[] pools = ColorDiscoveryRune.GetOtherCharacterPools(
			[ownerPool, externalPool],
			ownerPool.Id).ToArray();

		SequenceEqual([externalPool], pools, "third-party character pool should remain eligible");

		MethodInfo productionMethod = typeof(ColorDiscoveryRune).GetMethod(
			"GetOtherCharacterCards",
			BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new MissingMethodException(nameof(ColorDiscoveryRune), "GetOtherCharacterCards");
		List<MethodInfo> referencedMethods = [];
		CollectReferencedMethods(productionMethod, referencedMethods, []);
		Expect(
			referencedMethods.Any(static method => method.DeclaringType == typeof(ModelDb)
				&& method.Name == "get_AllCharacters"),
			"production Color Discovery should enumerate every registered character, including locked and external characters");
	}

	private static void MapLengthReducerRejectsGoldenPathAndThirdPartyMapTypes()
	{
		Expect(HextechMapLengthReducer.IsSupportedMapType(typeof(ActMap)), "vanilla ActMap hierarchy should be recognized");
		Expect(
			!HextechMapLengthReducer.IsSupportedMapType(typeof(GoldenPathActMap)),
			"Golden Path must be excluded through its public type.");
		Expect(
			!HextechMapLengthReducer.IsSupportedMapType(typeof(CompatibilityExternalActMap)),
			"third-party ActMap subclasses must fail open without rewriting.");
	}

	private static void JeweledGauntletReflectionTargetsFailClosedAsAGroup()
	{
		FieldInfo? intents = typeof(MoveState).GetField(
			"<Intents>k__BackingField",
			BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo? performingMove = typeof(MonsterModel).GetField(
			"_isPerformingMove",
			BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo? curseCounter = typeof(KnowledgeDemon).GetField(
			"_curseOfKnowledgeCounter",
			BindingFlags.Instance | BindingFlags.NonPublic);

		Expect(
			HextechCombatHooks.HasJeweledGauntletPrivateFieldContracts(intents, performingMove, curseCounter),
			"current-version Jeweled Gauntlet field contracts");
		Expect(
			!HextechCombatHooks.HasJeweledGauntletPrivateFieldContracts(null, performingMove, curseCounter),
			"one missing field must disable the whole Jeweled Gauntlet hook group");
		Expect(
			!HextechCombatHooks.HasJeweledGauntletPrivateFieldContracts(curseCounter, performingMove, intents),
			"changed field signatures must disable the whole Jeweled Gauntlet hook group");
	}

	private static void TestSubjectRespawnReflectionMissingFallsBackToZero()
	{
		Equal(0, HextechMayhemModifier.NormalizeTestSubjectRespawns(null), "missing TestSubject respawn field");
		Equal(0, HextechMayhemModifier.NormalizeTestSubjectRespawns("2"), "changed TestSubject respawn field type");
		Equal(0, HextechMayhemModifier.NormalizeTestSubjectRespawns(-1), "negative TestSubject respawn field");
		Equal(2, HextechMayhemModifier.NormalizeTestSubjectRespawns(2), "valid TestSubject respawn field");
	}

	private static void InspectOpenScopesToHextechAndPreservesExternalPrefixChanges()
	{
		RelicModel requested = new ColorDiscoveryRune();
		RelicModel externalFirst = new CompatibilityExternalInspectRelicA();
		RelicModel externalSecond = new CompatibilityExternalInspectRelicB();

		Expect(HextechInspectHooks.ShouldHandleInspectRequest(requested), "Hextech inspect request should be handled");
		Expect(
			!HextechInspectHooks.ShouldHandleInspectRequest(externalFirst),
			"third-party inspect request must pass through untouched");

		RelicModel[] externallyModifiedRelics = [externalFirst, externalSecond];
		IReadOnlyList<RelicModel> merged = HextechInspectHooks.MergeRequestedInspectRelic(
			externallyModifiedRelics,
			requested,
			out int requestedIndex);
		SequenceEqual(
			[externalFirst, externalSecond, requested],
			merged,
			"external prefix relic entries must survive Hextech merge");
		Equal(2, requestedIndex, "merged Hextech inspect index");

		IReadOnlyList<RelicModel> unchanged = HextechInspectHooks.MergeRequestedInspectRelic(
			merged,
			requested,
			out int existingIndex);
		Expect(ReferenceEquals(merged, unchanged), "an existing request must not replace another mod's final list object");
		Equal(requestedIndex, existingIndex, "existing Hextech inspect index");
	}

	private static void TurnProcKeysPreserveBuiltInsAndNamespaceExternalDerivatives()
	{
		Equal(
			nameof(AdamantRune),
			HextechRelicBase.GetStableTurnProcKey(typeof(AdamantRune)),
			"built-in turn proc key");

		Type externalTypeA = typeof(CompatibilityExternalModA.SharedDebuffRune);
		Type externalTypeB = typeof(CompatibilityExternalModB.SharedDebuffRune);
		Equal(externalTypeA.Name, externalTypeB.Name, "external test short names");

		string expectedExternalKeyA = $"{externalTypeA.Assembly.GetName().Name}:{externalTypeA.FullName}";
		string expectedExternalKeyB = $"{externalTypeB.Assembly.GetName().Name}:{externalTypeB.FullName}";
		string firstKeyA = HextechRelicBase.GetStableTurnProcKey(externalTypeA);
		string secondKeyA = HextechRelicBase.GetStableTurnProcKey(externalTypeA);
		string keyB = HextechRelicBase.GetStableTurnProcKey(externalTypeB);
		Equal(expectedExternalKeyA, firstKeyA, "first external turn proc key");
		Equal(expectedExternalKeyB, keyB, "second external turn proc key");
		Expect(firstKeyA != keyB, "external derived types with the same short name must not share a proc key");
		Equal(firstKeyA, secondKeyA, "single-player and network turn proc key stability");
	}

	private abstract class CompatibilityCardPoolBase : CardPoolModel
	{
		public override string Title => "Compatibility";
		public override string EnergyColorName => "red";
		public override string CardFrameMaterialPath => "ironclad";
		public override Color DeckEntryCardColor => Colors.White;
		public override bool IsColorless => false;

		protected override CardModel[] GenerateAllCards()
		{
			return [];
		}
	}

	private sealed class CompatibilityOwnerCardPool : CompatibilityCardPoolBase
	{
	}

	private sealed class CompatibilityExternalCardPool : CompatibilityCardPoolBase
	{
	}

	private abstract class CompatibilityExternalActMap : ActMap
	{
	}

	private sealed class CompatibilityExternalInspectRelicA : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private sealed class CompatibilityExternalInspectRelicB : RelicModel
	{
		public sealed override RelicRarity Rarity => RelicRarity.Event;
	}

	private static class CompatibilityExternalModA
	{
		public sealed class SharedDebuffRune : LimitedDebuffProcRelicBase
		{
			protected override Task OnEnemyDebuffApplied(Creature target)
			{
				return Task.CompletedTask;
			}
		}
	}

	private static class CompatibilityExternalModB
	{
		public sealed class SharedDebuffRune : LimitedDebuffProcRelicBase
		{
			protected override Task OnEnemyDebuffApplied(Creature target)
			{
				return Task.CompletedTask;
			}
		}
	}
}
