using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HextechRunesSponsorPack;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void RepeatableEnchantmentsRequireCurrentlyOwnedEnchantmentMasterRune()
	{
		Player player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		List<RelicModel> relics = [];
		typeof(Player)
			.GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(player, relics);

		Expect(!RepeatableEnchantmentAccessPolicy.IsEnabledFor(player), "repeatable enchantments must be disabled without EnchantmentMasterRune");
		relics.Add((EnchantmentMasterRune)RuntimeHelpers.GetUninitializedObject(typeof(EnchantmentMasterRune)));
		Expect(RepeatableEnchantmentAccessPolicy.IsEnabledFor(player), "repeatable enchantments must be enabled while EnchantmentMasterRune is owned");
		relics.Clear();
		Expect(!RepeatableEnchantmentAccessPolicy.IsEnabledFor(player), "repeatable enchantments must be disabled immediately after EnchantmentMasterRune is removed");
	}

	private static void EnchantmentCompositionAdapterFindsDirectEnchantments()
	{
		EnchantmentModel enchantment = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		Equal(enchantment, EnchantmentCompositionAdapter.Find(enchantment, typeof(Sharp)), "direct enchantment lookup");
		Expect(EnchantmentCompositionAdapter.Contains(enchantment, typeof(Sharp)), "direct enchantment should be contained");
		Expect(!EnchantmentCompositionAdapter.Contains(enchantment, typeof(Sown)), "different enchantment type should not be contained");
	}

	private static void EnchantmentCompositionAdapterFindsSponsorCompositeEnchantments()
	{
		EnchantmentModel sharp = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		SponsorCompositeEnchantment composite = (SponsorCompositeEnchantment)RuntimeHelpers.GetUninitializedObject(typeof(SponsorCompositeEnchantment));
		typeof(SponsorCompositeEnchantment)
			.GetField("_innerEnchantments", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(composite, new List<EnchantmentModel> { sharp });
		typeof(SponsorCompositeEnchantment)
			.GetField("_subscribedInnerEnchantments", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(composite, new List<EnchantmentModel>());

		Equal(sharp, EnchantmentCompositionAdapter.Find(composite, typeof(Sharp)), "sponsor composite lookup");
		Expect(EnchantmentCompositionAdapter.Contains(composite, typeof(Sharp)), "sponsor composite should contain its inner enchantment");
		Expect(!EnchantmentCompositionAdapter.Contains(composite, typeof(Sown)), "sponsor composite should reject missing enchantment types");
	}

	private static void EnchantmentCompositionAdapterUsesMultiEnchantmentPublicApi()
	{
		AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
			new AssemblyName("MultiEnchantmentMod"),
			AssemblyBuilderAccess.Run);
		TypeBuilder apiType = assembly
			.DefineDynamicModule("MultiEnchantmentMod")
			.DefineType(
				"MultiEnchantmentMod.Api.MultiEnchantmentApi",
				TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
		MethodBuilder getEnchantment = apiType.DefineMethod(
			"GetEnchantment",
			MethodAttributes.Public | MethodAttributes.Static,
			typeof(EnchantmentModel),
			[ typeof(CardModel), typeof(Type) ]);
		MethodInfo enchantmentGetter = typeof(CardModel)
			.GetProperty(nameof(CardModel.Enchantment))!
			.GetMethod!;
		ILGenerator il = getEnchantment.GetILGenerator();
		il.Emit(OpCodes.Ldarg_1);
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Callvirt, enchantmentGetter);
		il.Emit(OpCodes.Callvirt, typeof(Type).GetMethod(nameof(Type.IsInstanceOfType), [ typeof(object) ])!);
		Label missing = il.DefineLabel();
		il.Emit(OpCodes.Brfalse_S, missing);
		il.Emit(OpCodes.Ldarg_0);
		il.Emit(OpCodes.Callvirt, enchantmentGetter);
		il.Emit(OpCodes.Ret);
		il.MarkLabel(missing);
		il.Emit(OpCodes.Ldnull);
		il.Emit(OpCodes.Ret);
		apiType.CreateType();

		EnchantmentModel sharp = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		CardModel card = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		typeof(CardModel)
			.GetField("<Enchantment>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(card, sharp);

		Equal(sharp, EnchantmentCompositionAdapter.Find(card, typeof(Sharp)), "MultiEnchantmentMod public API lookup");
		Expect(EnchantmentCompositionAdapter.Find(card, typeof(Sown)) == null, "MultiEnchantmentMod public API should respect requested type");
	}

	private static void SponsorCompositeExpandsInnerHookListeners()
	{
		EnchantmentModel sharp = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		SponsorCompositeEnchantment composite = (SponsorCompositeEnchantment)RuntimeHelpers.GetUninitializedObject(typeof(SponsorCompositeEnchantment));
		typeof(SponsorCompositeEnchantment)
			.GetField("_innerEnchantments", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(composite, new List<EnchantmentModel> { sharp });
		typeof(SponsorCompositeEnchantment)
			.GetField("_subscribedInnerEnchantments", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(composite, new List<EnchantmentModel>());

		AbstractModel[] expanded = BuiltInRepeatableEnchantments
			.ExpandCompositeHookListeners([ composite ])
			.ToArray();
		Equal(1, expanded.Length, "expanded hook listener count");
		Equal(sharp, expanded[0], "expanded hook listener");

		string[] manuallyForwardedHooks =
		[
			nameof(AbstractModel.AfterCardPlayed),
			nameof(AbstractModel.AfterCardDrawn),
			nameof(AbstractModel.AfterPlayerTurnStart),
			nameof(AbstractModel.BeforeFlush),
			nameof(AbstractModel.ModifyShuffleOrder)
		];
		foreach (string hook in manuallyForwardedHooks)
		{
			Expect(
				typeof(SponsorCompositeEnchantment).GetMethod(
					hook,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) == null,
				$"composite should not hard-code hook forwarding for {hook}");
		}
	}

	private static void DollysMirrorRelicPagesStayWithinVanillaViewport()
	{
		DollyRelicPageLayout first = DollysMirrorForge.GetRelicPageLayout(13, 0);
		Equal(6, first.RelicCount, "first Dolly relic page count");
		Expect(!first.HasPreviousPage, "first Dolly relic page should not have previous-page navigation");
		Expect(first.HasNextPage, "first Dolly relic page should have next-page navigation");

		DollyRelicPageLayout middle = DollysMirrorForge.GetRelicPageLayout(13, 1);
		Equal(6, middle.RelicCount, "middle Dolly relic page count");
		Expect(middle.HasPreviousPage, "middle Dolly relic page should have previous-page navigation");
		Expect(middle.HasNextPage, "middle Dolly relic page should have next-page navigation");

		DollyRelicPageLayout last = DollysMirrorForge.GetRelicPageLayout(13, 99);
		Equal(1, last.RelicCount, "last Dolly relic page count");
		Expect(last.HasPreviousPage, "last Dolly relic page should have previous-page navigation");
		Expect(!last.HasNextPage, "last Dolly relic page should not have next-page navigation");
		Equal(2, last.PageIndex, "Dolly relic page index clamp");
	}

	private static void AbyssalContractChoiceModelsMapToExpectedContracts()
	{
		Equal(
			AbyssalContractKind.Warrior,
			AbyssalContractRune.GetContractKindForChoice(
				(WarriorContractChoiceRelic)RuntimeHelpers.GetUninitializedObject(typeof(WarriorContractChoiceRelic))),
			"warrior contract choice");
		Equal(
			AbyssalContractKind.Hunter,
			AbyssalContractRune.GetContractKindForChoice(
				(HunterContractChoiceRelic)RuntimeHelpers.GetUninitializedObject(typeof(HunterContractChoiceRelic))),
			"hunter contract choice");
		Equal(
			AbyssalContractKind.Regent,
			AbyssalContractRune.GetContractKindForChoice(
				(RegentContractChoiceRelic)RuntimeHelpers.GetUninitializedObject(typeof(RegentContractChoiceRelic))),
			"regent contract choice");
		Equal(
			AbyssalContractKind.Necrobinder,
			AbyssalContractRune.GetContractKindForChoice(
				(NecrobinderContractChoiceRelic)RuntimeHelpers.GetUninitializedObject(typeof(NecrobinderContractChoiceRelic))),
			"necrobinder contract choice");
		Equal(
			AbyssalContractKind.Automaton,
			AbyssalContractRune.GetContractKindForChoice(
				(AutomatonContractChoiceRelic)RuntimeHelpers.GetUninitializedObject(typeof(AutomatonContractChoiceRelic))),
			"automaton contract choice");
	}

	private static void AbyssalContractWarriorEliteThresholdGrows()
	{
		int eliteKills = 0;
		int strengthBonuses = 0;
		Expect(
			AbyssalContractRune.AdvanceWarriorEliteProgress(ref eliteKills, ref strengthBonuses),
			"first elite should immediately grant the first strength bonus");
		Equal(0, eliteKills, "elite progress after first bonus");
		Equal(1, strengthBonuses, "first strength bonus count");

		Expect(
			!AbyssalContractRune.AdvanceWarriorEliteProgress(ref eliteKills, ref strengthBonuses),
			"one more elite should not yet grant the second strength bonus");
		Equal(1, eliteKills, "elite progress toward second bonus");
		Expect(
			AbyssalContractRune.AdvanceWarriorEliteProgress(ref eliteKills, ref strengthBonuses),
			"two more elites should grant the second strength bonus");
		Equal(0, eliteKills, "elite progress after second bonus");
		Equal(2, strengthBonuses, "second strength bonus count");
	}

	private static void AbyssalContractStarterUpgradeMappingsCoverVanillaCharacters()
	{
		Equal(typeof(BlackBlood), AbyssalContractRune.GetStarterUpgradeType(typeof(Ironclad)), "Ironclad starter upgrade");
		Equal(typeof(RingOfTheDrake), AbyssalContractRune.GetStarterUpgradeType(typeof(Silent)), "Silent starter upgrade");
		Equal(typeof(DivineDestiny), AbyssalContractRune.GetStarterUpgradeType(typeof(Regent)), "Regent starter upgrade");
		Equal(typeof(PhylacteryUnbound), AbyssalContractRune.GetStarterUpgradeType(typeof(Necrobinder)), "Necrobinder starter upgrade");
		Equal(typeof(InfusedCore), AbyssalContractRune.GetStarterUpgradeType(typeof(Defect)), "Defect starter upgrade");
	}

	private static void AbyssalContractWarriorCardFilterRejectsSkillsAndPowers()
	{
		Expect(!AbyssalContractRune.IsWarriorForbiddenCardType(CardType.Attack), "attacks should remain legal");
		Expect(AbyssalContractRune.IsWarriorForbiddenCardType(CardType.Skill), "skills should be forbidden");
		Expect(AbyssalContractRune.IsWarriorForbiddenCardType(CardType.Power), "powers should be forbidden");
		Expect(!AbyssalContractRune.IsWarriorForbiddenCardType(CardType.Status), "statuses should remain legal");
		Expect(!AbyssalContractRune.IsWarriorForbiddenCardType(CardType.Curse), "curses should remain legal");
	}
}
