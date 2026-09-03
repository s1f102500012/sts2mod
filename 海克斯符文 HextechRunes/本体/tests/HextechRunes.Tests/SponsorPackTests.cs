using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HextechRunesSponsorPack;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes.Tests;

internal static partial class Program
{
	// 神迹事件的随机结果必须逐位不变:算法从 MiracleEvent.StableRoll 抽到 SponsorStableRandom 后,
	// 用纯函数核心守住「同输入同输出、任一盐位变化则输出变化」。
	private static void SponsorStableRandomHashIsDeterministicAndSaltSensitive()
	{
		ulong baseline = SponsorStableRandom.Hash("SEED-1", 2, 17, "enchantment-master", "3", "card");
		Equal(
			baseline,
			SponsorStableRandom.Hash("SEED-1", 2, 17, "enchantment-master", "3", "card"),
			"same seed/act/floor/salt must produce the same hash");
		Expect(baseline != SponsorStableRandom.Hash("SEED-1", 2, 17, "enchantment-master", "3", "enchant"), "a different salt tail must change the hash");
		Expect(baseline != SponsorStableRandom.Hash("SEED-1", 2, 18, "enchantment-master", "3", "card"), "a different floor must change the hash");
		Expect(baseline != SponsorStableRandom.Hash("SEED-1", 3, 17, "enchantment-master", "3", "card"), "a different act must change the hash");
		Expect(baseline != SponsorStableRandom.Hash("SEED-2", 2, 17, "enchantment-master", "3", "card"), "a different run seed must change the hash");
		Expect(baseline != SponsorStableRandom.Hash("SEED-1", 2, 17, "enchantment-master", "4", "card"), "a different owner net id must change the hash");

		// 神迹事件的历史盐(miracle.*)与附魔大师共用同一核心,顺序与分隔符不能变。
		Expect(
			SponsorStableRandom.Hash("SEED-1", 0, 0, "miracle.gift", "2", "1")
				!= SponsorStableRandom.Hash("SEED-1", 0, 0, "miracle.gift", "1", "2"),
			"salt parts must not be order-insensitive");
	}

	// 功能组注册的依赖表必须与清单表自洽:可获得内容都在锻造器/符文表里,依赖都在载体/图标/事件遗物表里;
	// 并且运行期硬引用选择遗物的锻造器/符文都声明了依赖(漏声明 = 依赖注册失败时它照样入池,结算时 ModelDb.Relic<T>() 报错)。
	private static void SponsorCatalogDependencyTableIsConsistent()
	{
		HashSet<Type> obtainables = [.. SponsorCatalog.ObtainableTypes];
		HashSet<Type> dependencies = [.. SponsorCatalog.DependencyTypes];
		foreach ((Type obtainable, Type[] required) in SponsorCatalog.RequiredDependencies)
		{
			Expect(obtainables.Contains(obtainable), $"{obtainable.Name} in Requires must be a registered forge or rune");
			Expect(required.Length > 0, $"{obtainable.Name} must list at least one dependency");
			foreach (Type dependency in required)
			{
				Expect(dependencies.Contains(dependency), $"{obtainable.Name} depends on {dependency.Name}, which is not in any dependency table");
			}
		}

		foreach (Type expected in new[] { typeof(EntropyForge), typeof(ArcaneForge), typeof(DollysMirrorForge), typeof(EvolutionForge), typeof(StarlightSparkleRune), typeof(AbyssalContractRune) })
		{
			Expect(SponsorCatalog.RequiredDependencies.ContainsKey(expected), $"{expected.Name} hard-references choice relics or carriers and must declare them");
		}
	}

	// 排除规则的类型部分(纯函数,不触碰 Godot 资源层)。
	private static void RandomEnchantmentPoolExcludesDeprecatedNegativeAndMarkerTypes()
	{
		Expect(RandomEnchantmentPool.IsExcludedType(typeof(DeprecatedEnchantment)), "DeprecatedEnchantment must be excluded");
		Expect(RandomEnchantmentPool.IsExcludedType(typeof(Corrupted)), "Corrupted must be excluded");
		Expect(RandomEnchantmentPool.IsExcludedType(typeof(Clone)), "Clone must be excluded");
		Expect(RandomEnchantmentPool.IsExcludedType(typeof(SponsorCompositeEnchantment)), "the composite migration shell must be excluded");
		Expect(!RandomEnchantmentPool.IsExcludedType(typeof(Sharp)), "a plain vanilla enchantment must stay eligible");
		Expect(!RandomEnchantmentPool.IsExcludedType(typeof(Sown)), "a plain vanilla enchantment must stay eligible");

		// 候选池包含本包自己的附魔;熵减是负面(打出后战斗结束把牌移出牌组),与 Corrupted 同类排除。
		Expect(!RandomEnchantmentPool.IsExcludedType(typeof(Evolution)), "the sponsor pack's own Evolution must stay eligible");
		Expect(!RandomEnchantmentPool.IsExcludedType(typeof(EntropyIncrease)), "the sponsor pack's own EntropyIncrease must stay eligible");
		Expect(RandomEnchantmentPool.IsExcludedType(typeof(EntropyDecrease)), "EntropyDecrease is a negative enchantment and must be excluded");

		// MultiEnchantmentMod 的标记基类按 FullName 字符串比对,不引用该程序集。
		ModuleBuilder module = AssemblyBuilder
			.DefineDynamicAssembly(new AssemblyName("SponsorPackFakeMultiEnchantmentMod"), AssemblyBuilderAccess.Run)
			.DefineDynamicModule("SponsorPackFakeMultiEnchantmentMod");
		Type marker = module
			.DefineType("MultiEnchantmentMod.Api.MarkerEnchantmentModel", TypeAttributes.Public | TypeAttributes.Abstract)
			.CreateType();
		Type markerSubclass = module.DefineType("MultiEnchantmentMod.SomeMarker", TypeAttributes.Public, marker).CreateType();
		Type subEnchantment = module.DefineType("PengoTarot.PlanetSubEnchantment", TypeAttributes.Public).CreateType();
		Type unrelated = module.DefineType("SomeMod.SomeEnchantment", TypeAttributes.Public).CreateType();

		Expect(RandomEnchantmentPool.IsExcludedType(marker), "the MultiEnchantmentMod marker base type must be excluded");
		Expect(RandomEnchantmentPool.IsExcludedType(markerSubclass), "types deriving from the MultiEnchantmentMod marker must be excluded");
		Expect(RandomEnchantmentPool.IsExcludedType(subEnchantment), "types named *SubEnchantment must be excluded");
		Expect(!RandomEnchantmentPool.IsExcludedType(unrelated), "unrelated third-party enchantments must stay eligible");

		// 没有图标的第三方附魔按「模组内部伴随附魔」排除。预置 EnchantmentModel._iconPath 避开 Godot ResourceLoader。
		FieldInfo iconPath = typeof(EnchantmentModel).GetField("_iconPath", BindingFlags.Instance | BindingFlags.NonPublic)!;
		EnchantmentModel sharp = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		iconPath.SetValue(sharp, EnchantmentModel.MissingIconPath);
		Expect(RandomEnchantmentPool.IsExcluded(sharp), "an enchantment falling back to the missing icon must be excluded");
		iconPath.SetValue(sharp, "res://images/enchantments/sharp.png");
		Expect(!RandomEnchantmentPool.IsExcluded(sharp), "an enchantment with a real icon must stay eligible");

		// 本包附魔的图标经 HextechRunesApi.RegisterEnchantmentIcon 登记(只改 Icon 不改 IconPath),IconPath 恒为
		// MissingIconPath,所以图标规则只能对非本包程序集生效,否则进化 / 熵增会被误排除。
		// (真实 canonical 实例要 ModelDb + Godot 资源层,测试进程里造不出来;这里用未初始化实例复现同样的 IconPath 状态。)
		EnchantmentModel evolution = (Evolution)RuntimeHelpers.GetUninitializedObject(typeof(Evolution));
		iconPath.SetValue(evolution, EnchantmentModel.MissingIconPath);
		Expect(!RandomEnchantmentPool.IsExcluded(evolution), "the sponsor pack's own enchantments must not be excluded by the icon rule");
	}

	// GetLegalEnchantments 只做「过 CanEnchant 的保序过滤」;池本身的 Id.Entry 有序由 SortByEntryOrdinal 保证。
	// 真实池要 ModelDb 造 canonical 实例(需要 Godot 资源层),测试进程里跑不了,所以走纯函数重载。
	private static void RandomEnchantmentPoolLegalEnchantmentsPreserveOrderAndCanEnchant()
	{
		CardModel skill = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		typeof(CardModel)
			.GetField("<Type>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(skill, CardType.Skill);

		EnchantmentModel vigorous = (Vigorous)RuntimeHelpers.GetUninitializedObject(typeof(Vigorous));
		EnchantmentModel steady = (Steady)RuntimeHelpers.GetUninitializedObject(typeof(Steady));
		EnchantmentModel sharp = (Sharp)RuntimeHelpers.GetUninitializedObject(typeof(Sharp));
		EnchantmentModel adroit = (Adroit)RuntimeHelpers.GetUninitializedObject(typeof(Adroit));
		EnchantmentModel[] pool = [ vigorous, steady, sharp, adroit ];

		IReadOnlyList<EnchantmentModel> legal = RandomEnchantmentPool.GetLegalEnchantments(skill, pool);
		Equal(2, legal.Count, "legal enchantment count for a Skill card");
		Equal(steady, legal[0], "first legal enchantment keeps pool order");
		Equal(adroit, legal[1], "second legal enchantment keeps pool order");
		foreach (EnchantmentModel enchantment in legal)
		{
			Expect(enchantment.CanEnchant(skill), $"{enchantment.GetType().Name} must actually satisfy CanEnchant");
		}

		// 池的排序键是 Id.Entry,比较器必须是 Ordinal(大写在前),不是 OrdinalIgnoreCase。
		List<string> sorted = RandomEnchantmentPool.SortByEntryOrdinal([ "b", "A", "a", "B" ], static entry => entry);
		Equal("A", sorted[0], "ordinal sort puts uppercase first");
		Equal("B", sorted[1], "ordinal sort puts uppercase first");
		Equal("a", sorted[2], "ordinal sort order");
		Equal("b", sorted[3], "ordinal sort order");
	}

	// 复合附魔只剩一个只读迁移壳:恒不可附、只保留 SavedEnchantmentsJson 这一个 [SavedProperty](net-id 布局不变)、
	// 保留 OnEnchant 作为读档迁移入口。真正的迁移路径要 ModelDb + SaveUtil 造内层附魔,测试进程里无法执行。
	private static void SponsorCompositeEnchantmentIsReadOnlyMigrationShell()
	{
		SponsorCompositeEnchantment shell = (SponsorCompositeEnchantment)RuntimeHelpers.GetUninitializedObject(typeof(SponsorCompositeEnchantment));
		Expect(!shell.CanEnchant((Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash))), "the migration shell must never be enchantable");
		Expect(!shell.HasExtraCardText, "the migration shell must not contribute card text");

		PropertyInfo[] savedProperties = typeof(SponsorCompositeEnchantment)
			.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
			.Where(static property => property.GetCustomAttributes()
				.Any(static attribute => attribute.GetType().Name == "SavedPropertyAttribute"))
			.ToArray();
		Equal(1, savedProperties.Length, "the migration shell must keep exactly one SavedProperty");
		Equal("SavedEnchantmentsJson", savedProperties[0].Name, "the SavedProperty name must not change (it decides the net-id layout)");
		Equal(typeof(string), savedProperties[0].PropertyType, "SavedEnchantmentsJson must stay a string");

		Expect(
			typeof(SponsorCompositeEnchantment).GetMethod(
				"OnEnchant",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null,
			"the migration shell must keep OnEnchant as the save-load migration entry point");
		Expect(
			typeof(SponsorCompositeEnchantment).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
				.All(static method => method.Name is nameof(EnchantmentModel.CanEnchant) or "get_HasExtraCardText"),
			"the migration shell must not expose any multi-enchantment API any more");
	}

	// 熵减的「战后一次预览批量删除」不再靠 Hook.AfterCombatEnd 的补丁收集,改由第一个被回调的实例
	// 扫一遍牌组。选牌是纯函数,在这里守住:只挑本场打出过(PendingRemoval)的熵减牌,且保持牌组顺序。
	private static void EntropyDecreaseCollectsOnlyCardsMarkedForRemoval()
	{
		FieldInfo enchantmentField = typeof(CardModel)
			.GetField("<Enchantment>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

		CardModel plain = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));

		CardModel unplayed = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		enchantmentField.SetValue(unplayed, RuntimeHelpers.GetUninitializedObject(typeof(EntropyDecrease)));

		CardModel played = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		EntropyDecrease playedEnchantment = (EntropyDecrease)RuntimeHelpers.GetUninitializedObject(typeof(EntropyDecrease));
		playedEnchantment.PendingRemoval = true;
		enchantmentField.SetValue(played, playedEnchantment);

		CardModel otherEnchantment = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		enchantmentField.SetValue(otherEnchantment, RuntimeHelpers.GetUninitializedObject(typeof(EntropyIncrease)));

		CardModel alsoPlayed = (Bash)RuntimeHelpers.GetUninitializedObject(typeof(Bash));
		EntropyDecrease alsoPlayedEnchantment = (EntropyDecrease)RuntimeHelpers.GetUninitializedObject(typeof(EntropyDecrease));
		alsoPlayedEnchantment.PendingRemoval = true;
		enchantmentField.SetValue(alsoPlayed, alsoPlayedEnchantment);

		IReadOnlyList<CardModel> collected = EntropyDecrease.CollectPendingRemovalCards(
			[plain, unplayed, played, otherEnchantment, alsoPlayed]);
		Equal(2, collected.Count, "only the played entropy-decrease cards should be collected");
		Equal(played, collected[0], "collection keeps deck order");
		Equal(alsoPlayed, collected[1], "collection keeps deck order");
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
