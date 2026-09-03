using HextechRunes;
using MegaCrit.Sts2.Core.Logging;

namespace HextechRunesSponsorPack;

/// <summary>
/// 拓展包的内容清单:符文 / 锻造器 / 事件遗物 / SavedProperty 载体 / 附魔图标各一张只读表,
/// <see cref="RegisterAll"/> 按表逐条调 <see cref="HextechRunesApi"/>。
/// </summary>
/// <remarks>
/// 表内顺序即注册顺序,决定 ModelDb 的发现顺序与池内顺序,是 append-only 清单:新增条目追加到对应表末尾,不重排既有条目。
/// </remarks>
internal static class SponsorCatalog
{
	// 迁移壳 SponsorCompositeEnchantment 仍带 [SavedProperty],载体注册保留;它不再有图标(不注册、不进随机附魔池)。
	private static readonly Type[] SavedPropertyCarriers =
	[
		typeof(Evolution),
		typeof(EntropyIncrease),
		typeof(EntropyDecrease),
		typeof(SponsorCompositeEnchantment)
	];

	private static readonly (Type Enchantment, string IconFile)[] EnchantmentIcons =
	[
		(typeof(Evolution), "evolution.png"),
		(typeof(EntropyIncrease), "plus.png"),
		(typeof(EntropyDecrease), "minus.png")
	];

	private static readonly (Type Forge, HextechRarityTier Rarity)[] Forges =
	[
		(typeof(BasicForge), HextechRarityTier.Gold),
		(typeof(EnchantmentForge), HextechRarityTier.Gold),
		(typeof(EntropyForge), HextechRarityTier.Gold),
		(typeof(ArcaneForge), HextechRarityTier.Prismatic),
		(typeof(DollysMirrorForge), HextechRarityTier.Prismatic),
		(typeof(EvolutionForge), HextechRarityTier.Prismatic),
		(typeof(MysticForge), HextechRarityTier.Prismatic)
	];

	// 信徒(棱彩,仅单人):IsAvailableForPlayer 内部按 !IsNetworkMultiplayerRun() 门控单人。
	private static readonly (Type Rune, HextechRarityTier Rarity, string TagKey)[] PlayerRunes =
	[
		(typeof(StarlightSparkleRune), HextechRarityTier.Gold, "COMPREHENSIVE"),
		(typeof(CosplayRune), HextechRarityTier.Prismatic, "COMPREHENSIVE"),
		(typeof(OtterAndFriendsRune), HextechRarityTier.Prismatic, "COMPREHENSIVE"),
		(typeof(RegretRune), HextechRarityTier.Prismatic, "SURVIVAL"),
		(typeof(GastritisRune), HextechRarityTier.Prismatic, "OUTPUT"),
		(typeof(EnchantmentMasterRune), HextechRarityTier.Prismatic, "COMPREHENSIVE"),
		(typeof(DesperateFinaleRune), HextechRarityTier.Prismatic, "COMPREHENSIVE"),
		(typeof(AbyssalContractRune), HextechRarityTier.Prismatic, "COMPREHENSIVE"),
		(typeof(BelieverRune), HextechRarityTier.Prismatic, "COMPREHENSIVE")
	];

	private static readonly Type[] EventRelics =
	[
		typeof(GoldStarRelic),
		typeof(ArcaneCloneChoiceRelic),
		typeof(ArcaneSoulsPowerChoiceRelic),
		typeof(ArcaneRoyallyApprovedChoiceRelic),
		typeof(DollyCardChoiceRelic),
		typeof(DollyRelicChoiceRelic),
		typeof(DollyPreviousPageRelic),
		typeof(DollyNextPageRelic),
		typeof(EntropyIncreaseChoiceRelic),
		typeof(EntropyDecreaseChoiceRelic),
		typeof(WarriorContractChoiceRelic),
		typeof(HunterContractChoiceRelic),
		typeof(RegentContractChoiceRelic),
		typeof(NecrobinderContractChoiceRelic),
		typeof(AutomatonContractChoiceRelic)
	];

	// 可获得内容(锻造器 / 符文)在运行期硬引用的依赖:选择用的事件遗物、附魔载体与图标。
	// 依赖里任何一项注册失败,对应的可获得内容就不入池——否则玩家拿到锻造器后结算时 ModelDb.Relic<T>() 直接报错。
	// 没列出的可获得内容没有硬依赖(附魔大师对锻造器类型只做过滤,缺席仍能工作)。
	private static readonly Dictionary<Type, Type[]> Requires = new()
	{
		[typeof(EntropyForge)] = [typeof(EntropyIncrease), typeof(EntropyDecrease), typeof(EntropyIncreaseChoiceRelic), typeof(EntropyDecreaseChoiceRelic)],
		[typeof(ArcaneForge)] = [typeof(ArcaneCloneChoiceRelic), typeof(ArcaneSoulsPowerChoiceRelic), typeof(ArcaneRoyallyApprovedChoiceRelic)],
		[typeof(DollysMirrorForge)] = [typeof(DollyCardChoiceRelic), typeof(DollyRelicChoiceRelic), typeof(DollyPreviousPageRelic), typeof(DollyNextPageRelic)],
		[typeof(EvolutionForge)] = [typeof(Evolution)],
		[typeof(StarlightSparkleRune)] = [typeof(GoldStarRelic)],
		[typeof(AbyssalContractRune)] = [typeof(WarriorContractChoiceRelic), typeof(HunterContractChoiceRelic), typeof(RegentContractChoiceRelic), typeof(NecrobinderContractChoiceRelic), typeof(AutomatonContractChoiceRelic)]
	};

	// 供清单一致性测试用:依赖表里的每个可获得类型都必须在锻造器/符文表里,每个依赖都必须在载体/图标/事件遗物表里。
	internal static IEnumerable<Type> ObtainableTypes =>
		Forges.Select(static entry => entry.Forge).Concat(PlayerRunes.Select(static entry => entry.Rune));

	internal static IEnumerable<Type> DependencyTypes =>
		SavedPropertyCarriers.Concat(EnchantmentIcons.Select(static entry => entry.Enchantment)).Concat(EventRelics);

	internal static IReadOnlyDictionary<Type, Type[]> RequiredDependencies => Requires;

	/// <summary>
	/// 按功能组注册。注册不是事务:本体的注册表没有回滚口子,一条失败不能把前面已入池的内容撤回。
	/// 所以分两趟:先注册全部依赖(载体、图标、事件遗物),每条独立容错并记下失败的类型;
	/// 再注册可获得内容(锻造器、符文),<see cref="Requires"/> 里有依赖失败的直接跳过不入池。
	/// 各类别内部保持表序(决定池内顺序),类别之间的先后没有消费者。返回失败 + 跳过的条数。
	/// 补丁由入口无条件应用(每个补丁都以"玩家持有对应符文"为前提,内容缺席时只是空转)。
	/// </summary>
	internal static int RegisterAll()
	{
		HashSet<Type> failed = [];

		foreach (Type carrier in SavedPropertyCarriers)
		{
			Register("SavedProperty carrier", carrier, failed, () => HextechRunesApi.RegisterSavedPropertyCarrier(carrier));
		}

		foreach ((Type enchantment, string iconFile) in EnchantmentIcons)
		{
			Register("enchantment icon", enchantment, failed, () => HextechRunesApi.RegisterEnchantmentIcon(enchantment, $"res://{ModInfo.Id}/images/enchantments/{iconFile}"));
		}

		foreach (Type relic in EventRelics)
		{
			Register("event relic", relic, failed, () => HextechRunesApi.RegisterEventRelic(relic, ModInfo.Id));
		}

		int skipped = 0;
		foreach ((Type forge, HextechRarityTier rarity) in Forges)
		{
			if (HasFailedDependency("forge", forge, failed))
			{
				skipped++;
				continue;
			}

			Register("forge", forge, failed, () => HextechRunesApi.RegisterForge(forge, rarity, ModInfo.Id));
		}

		foreach ((Type rune, HextechRarityTier rarity, string tagKey) in PlayerRunes)
		{
			if (HasFailedDependency("player rune", rune, failed))
			{
				skipped++;
				continue;
			}

			Register("player rune", rune, failed, () => HextechRunesApi.RegisterPlayerRune(rune, rarity, tagKey: tagKey, assetModId: ModInfo.Id));
		}

		Log.Info($"[{ModInfo.Id}] Registered IntegratedStrategyEvents soft-collab rune content with runtime availability gating.");
		return failed.Count + skipped;
	}

	private static bool HasFailedDependency(string kind, Type obtainable, HashSet<Type> failed)
	{
		if (!Requires.TryGetValue(obtainable, out Type[]? dependencies))
		{
			return false;
		}

		Type[] missing = dependencies.Where(failed.Contains).ToArray();
		if (missing.Length == 0)
		{
			return false;
		}

		Log.Warn($"[{ModInfo.Id}] Skipped {kind} {obtainable.Name}: dependency registration failed for {string.Join(", ", missing.Select(static type => type.Name))}.", 2);
		return true;
	}

	private static void Register(string kind, Type type, HashSet<Type> failed, Action register)
	{
		try
		{
			register();
		}
		catch (Exception ex)
		{
			failed.Add(type);
			Log.Warn($"[{ModInfo.Id}] Failed to register {kind} {type.Name}: {ex.GetType().Name}: {ex.Message}", 2);
		}
	}
}
