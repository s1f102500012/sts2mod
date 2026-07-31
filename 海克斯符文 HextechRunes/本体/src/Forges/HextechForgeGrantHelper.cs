using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal readonly record struct HextechForgeRarityWeights(int Silver, int Gold, int Prismatic)
{
	public int Total => Silver + Gold + Prismatic;
}

public sealed class RandomForgeShopRelic : HextechRelicBase
{
	private const string PriceVarName = "Price";

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedPurchaseCount
	{
		get => PurchaseCount;
		set => PurchaseCount = Math.Max(0, value);
	}

	public int PurchaseCount { get; private set; }

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar(PriceVarName, HextechRuneConfiguration.GetDefaultRandomForgeShopPrice())
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return false;
	}

	public void SetDisplayedPrice(int price)
	{
		DynamicVars[PriceVarName].BaseValue = HextechRuneConfiguration.ClampRandomForgeShopPrice(price);
	}

	public void IncrementPurchaseCount()
	{
		PurchaseCount++;
	}
}

internal static class HextechForgeGrantHelper
{
	public static async Task ObtainRandomForges(Player player, int count)
	{
		for (int i = 0; i < count; i++)
		{
			if (!TryCreateStableRandomForgeChoice(player, "obtain-random-forges", i, out List<RelicModel> options))
			{
				return;
			}

			RelicModel? selected = await HextechForgeSelectionCoordinator.SelectForge(player, options, $"obtain-random-forges:{i}");
			if (selected == null)
			{
				return;
			}

			await ObtainSelectedForge(player, selected, syncObtainedRelic: false);
		}
	}

	public static async Task ObtainRandomForges(
		Player player,
		HextechRarityTier rarity,
		int count,
		Func<Type, bool> forgeTypePredicate,
		string source)
	{
		for (int i = 0; i < count; i++)
		{
			if (!TryCreateStableRandomForgeChoice(player, rarity, source, i, forgeTypePredicate, out List<RelicModel> options))
			{
				return;
			}

			RelicModel? selected = await HextechForgeSelectionCoordinator.SelectForge(player, options, $"{source}:{i}");
			if (selected == null)
			{
				return;
			}

			await ObtainSelectedForge(player, selected, syncObtainedRelic: false);
		}
	}

	public static bool AddRandomForgeReward(Player player, CombatRoom room)
	{
		if (!TryCreateStableRandomForgeChoice(player, "combat-random-forge-reward", 0, out List<RelicModel> options))
		{
			return false;
		}

		room.AddExtraReward(player, new HextechForgeChoiceReward(options, player));
		return true;
	}

	public static bool AddWeightedRandomForgeReward(
		Player player,
		CombatRoom room,
		string source,
		int silverWeight,
		int goldWeight,
		int prismaticWeight)
	{
		if (!TryCreateStableRandomForgeChoice(player, source, 0, silverWeight, goldWeight, prismaticWeight, out List<RelicModel> options))
		{
			return false;
		}

		room.AddExtraReward(player, new HextechForgeChoiceReward(options, player));
		return true;
	}

	public static bool AddRandomForgeReward(Player player, CombatRoom room, HextechRarityTier rarity)
	{
		if (!TryCreateStableRandomForgeChoice(player, rarity, "combat-random-forge-reward-rarity", 0, out List<RelicModel> options))
		{
			return false;
		}

		room.AddExtraReward(player, new HextechForgeChoiceReward(options, player));
		return true;
	}

	public static async Task ObtainSelectedForge(Player player, RelicModel forge, bool syncObtainedRelic)
	{
		// 主机权威复核(联机安全网):决不发放被配置禁用的属性锻造器。
		// 候选池在战斗结束 / 进店那一刻就按当时的有效配置快照(modifier.DisabledForgeIdsForPool)构建,而联机下
		// 客机要到幕开局 ActRoll 把主机配置同步过来之前,快照都是默认的「空禁用」——这段时间窗里被禁锻造器会混进
		// 候选并被稳定随机选中。真正「落地获得」(领奖 / 购买完成)通常晚于建池,此刻主机配置多半已同步到位,故在
		// 这唯一落地点再用最新的有效禁用集兜底校验一次,挡掉任何漏网的被禁锻造器。
		if (IsForgeDisabledForPlayer(player, forge))
		{
			Log.Warn($"[{ModInfo.Id}][ForgeChoice] Blocked obtaining a config-disabled forge: player={player.NetId} relic={(forge.CanonicalInstance?.Id ?? forge.Id).Entry}");
			return;
		}

		SaveManager.Instance.MarkRelicAsSeen(forge);
		bool syncedBeforePickup = false;
		if (syncObtainedRelic)
		{
			INetGameService netService = RunManager.Instance.NetService;
			if (netService.Type is NetGameType.Host or NetGameType.Client && netService.IsConnected)
			{
				// Enchantment forges open a nested deck choice during pickup; remote clients must know about the forge first.
				ModelId forgeId = forge.CanonicalInstance?.Id ?? forge.Id;
				RelicModel syncCopy = ModelDb.GetById<RelicModel>(forgeId).ToMutable();
				RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(syncCopy);
				syncedBeforePickup = true;
			}
			else if (netService.Type is NetGameType.Host or NetGameType.Client)
			{
				Log.Warn($"[{ModInfo.Id}][ForgeChoice] Skipped forge reward sync because multiplayer service is disconnected: relic={forge.Id.Entry}");
			}
		}

		await RelicCmd.Obtain(forge, player);
		if (syncedBeforePickup)
		{
			HextechLog.Info($"[{ModInfo.Id}][ForgeChoice] Synced obtained forge before pickup effect: player={player.NetId} relic={forge.Id.Entry}");
		}
	}

	internal static bool TryCreateRandomForge(Player player, Rng rng, out RelicModel? forge)
	{
		HextechRarityTier rarity = RollForgeRarity(player, rng);
		return TryCreateRandomForge(player, rarity, rng, out forge);
	}

	internal static bool TryCreateRandomForgeChoice(Player player, Rng rng, out List<RelicModel> options)
	{
		HextechRarityTier rarity = RollForgeRarity(player, rng);
		return TryCreateRandomForgeChoice(player, rarity, rng, out options);
	}

	internal static bool TryCreateStableShopForgeChoice(Player player, int purchaseOrdinal, out List<RelicModel> options)
	{
		return TryCreateStableRandomForgeChoice(player, "shop-random-forge", purchaseOrdinal, out options);
	}

	private static bool TryCreateStableRandomForge(Player player, string source, int ordinal, out RelicModel? forge)
	{
		HextechRarityTier rarity = RollStableForgeRarity(player, source, ordinal);
		return TryCreateStableRandomForge(player, rarity, source, ordinal, out forge);
	}

	private static bool TryCreateStableRandomForgeChoice(Player player, string source, int ordinal, out List<RelicModel> options)
	{
		HextechRarityTier rarity = RollStableForgeRarity(player, source, ordinal);
		return TryCreateStableRandomForgeChoice(player, rarity, source, ordinal, out options);
	}

	private static bool TryCreateStableRandomForge(
		Player player,
		string source,
		int ordinal,
		int silverWeight,
		int goldWeight,
		int prismaticWeight,
		out RelicModel? forge)
	{
		HextechRarityTier rarity = RollStableForgeRarity(player, source, ordinal, silverWeight, goldWeight, prismaticWeight);
		return TryCreateStableRandomForge(player, rarity, source, ordinal, out forge);
	}

	private static bool TryCreateStableRandomForgeChoice(
		Player player,
		string source,
		int ordinal,
		int silverWeight,
		int goldWeight,
		int prismaticWeight,
		out List<RelicModel> options)
	{
		HextechRarityTier rarity = RollStableForgeRarity(player, source, ordinal, silverWeight, goldWeight, prismaticWeight);
		return TryCreateStableRandomForgeChoice(player, rarity, source, ordinal, out options);
	}

	private static bool TryCreateStableRandomForge(Player player, HextechRarityTier rarity, string source, int ordinal, out RelicModel? forge)
	{
		List<Type> pool = BuildAvailableForgePool(player, HextechCatalog.GetForgeTypesForRarity(rarity));
		if (pool.Count == 0)
		{
			pool = BuildAvailableForgePool(player, HextechCatalog.GetAllForgeTypes());
		}

		if (pool.Count == 0)
		{
			forge = null;
			return false;
		}

		Type forgeType = HextechStableRandom.Pick(
			pool,
			(RunState)player.RunState,
			HextechStableRandom.TypeModelKey,
			source,
			HextechStableRandom.PlayerKey(player),
			ordinal.ToString(),
			((int)rarity).ToString(),
			player.Relics.Count.ToString());
		forge = ModelDb.GetById<RelicModel>(ModelDb.GetId(forgeType)).ToMutable();
		return true;
	}

	private static bool TryCreateStableRandomForgeChoice(Player player, HextechRarityTier rarity, string source, int ordinal, out List<RelicModel> options)
	{
		List<Type> pool = BuildAvailableForgePool(player, HextechCatalog.GetForgeTypesForRarity(rarity));
		if (pool.Count == 0)
		{
			pool = BuildAvailableForgePool(player, HextechCatalog.GetAllForgeTypes());
		}

		if (pool.Count == 0)
		{
			options = [];
			return false;
		}

		List<Type> forgeTypes = HextechStableRandom.PickDistinct(
			pool,
			Math.Min(3, pool.Count),
			(RunState)player.RunState,
			HextechStableRandom.TypeModelKey,
			source,
			"forge-choice",
			HextechStableRandom.PlayerKey(player),
			ordinal.ToString(),
			((int)rarity).ToString(),
			player.Relics.Count.ToString());
		options = forgeTypes
			.Select(static type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable())
			.ToList();
		return options.Count > 0;
	}

	private static bool TryCreateStableRandomForgeChoice(
		Player player,
		HextechRarityTier rarity,
		string source,
		int ordinal,
		Func<Type, bool> forgeTypePredicate,
		out List<RelicModel> options)
	{
		List<Type> pool = BuildAvailableForgePool(player, HextechCatalog.GetForgeTypesForRarity(rarity).Where(forgeTypePredicate));
		if (pool.Count == 0)
		{
			options = [];
			return false;
		}

		List<Type> forgeTypes = HextechStableRandom.PickDistinct(
			pool,
			Math.Min(3, pool.Count),
			(RunState)player.RunState,
			HextechStableRandom.TypeModelKey,
			source,
			"filtered-forge-choice",
			HextechStableRandom.PlayerKey(player),
			ordinal.ToString(),
			((int)rarity).ToString(),
			player.Relics.Count.ToString());
		options = forgeTypes
			.Select(static type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable())
			.ToList();
		return options.Count > 0;
	}

	private static bool TryCreateRandomForge(Player player, HextechRarityTier rarity, Rng rng, out RelicModel? forge)
	{
		List<Type> pool = BuildAvailableForgePool(player, HextechCatalog.GetForgeTypesForRarity(rarity));
		if (pool.Count == 0)
		{
			pool = BuildAvailableForgePool(player, HextechCatalog.GetAllForgeTypes());
		}

		if (pool.Count == 0)
		{
			forge = null;
			return false;
		}

		Type forgeType = pool[rng.NextInt(pool.Count)];
		forge = ModelDb.GetById<RelicModel>(ModelDb.GetId(forgeType)).ToMutable();
		return true;
	}

	private static bool TryCreateRandomForgeChoice(Player player, HextechRarityTier rarity, Rng rng, out List<RelicModel> options)
	{
		List<Type> pool = BuildAvailableForgePool(player, HextechCatalog.GetForgeTypesForRarity(rarity));
		if (pool.Count == 0)
		{
			pool = BuildAvailableForgePool(player, HextechCatalog.GetAllForgeTypes());
		}

		if (pool.Count == 0)
		{
			options = [];
			return false;
		}

		List<RelicModel> selected = new(Math.Min(3, pool.Count));
		for (int i = 0; i < 3 && pool.Count > 0; i++)
		{
			int index = rng.NextInt(pool.Count);
			Type forgeType = pool[index];
			pool.RemoveAt(index);
			selected.Add(ModelDb.GetById<RelicModel>(ModelDb.GetId(forgeType)).ToMutable());
		}

		options = selected;
		return options.Count > 0;
	}

	private static List<Type> BuildAvailableForgePool(Player player, IEnumerable<Type> candidateTypes)
	{
		IReadOnlySet<string> disabledForgeIds = GetEffectiveDisabledForgeIds(player);
		return candidateTypes
			.Where(type => !disabledForgeIds.Contains(ModelDb.GetId(type).Entry))
			.Where(type => HextechCatalog.IsAvailableForPlayer(ModelDb.GetById<RelicModel>(ModelDb.GetId(type)), player))
			.ToList();
	}

	internal static HextechForgeRarityWeights ApplyDiceManiacForgeRarityModifier(HextechForgeRarityWeights weights, bool hasDiceManiac)
	{
		weights = NormalizeForgeRarityWeights(weights);
		if (!hasDiceManiac)
		{
			return weights;
		}

		return weights with
		{
			Gold = weights.Gold * DiceManiacRune.ForgeRarityMultiplier,
			Prismatic = weights.Prismatic * DiceManiacRune.ForgeRarityMultiplier
		};
	}

	private static HextechRarityTier RollForgeRarity(Player player, Rng rng)
	{
		HextechForgeRarityWeights baseWeights = GetBaseForgeRarityWeights(player);
		HextechForgeRarityWeights weights = GetModifiedForgeRarityWeights(player, baseWeights.Silver, baseWeights.Gold, baseWeights.Prismatic);
		if (weights.Total <= 0)
		{
			return HextechRarityTier.Silver;
		}

		return ResolveForgeRarity(weights, rng.NextInt(weights.Total));
	}

	private static HextechRarityTier RollStableForgeRarity(Player player, string source, int ordinal)
	{
		HextechForgeRarityWeights baseWeights = GetBaseForgeRarityWeights(player);
		return RollStableForgeRarity(player, source, ordinal, baseWeights.Silver, baseWeights.Gold, baseWeights.Prismatic);
	}

	private static HextechRarityTier RollStableForgeRarity(
		Player player,
		string source,
		int ordinal,
		int silverWeight,
		int goldWeight,
		int prismaticWeight)
	{
		HextechForgeRarityWeights weights = GetModifiedForgeRarityWeights(player, silverWeight, goldWeight, prismaticWeight);
		if (weights.Total <= 0)
		{
			return HextechRarityTier.Silver;
		}

		int roll = HextechStableRandom.Index(
			(RunState)player.RunState,
			weights.Total,
			source,
			"forge-rarity",
			HextechStableRandom.PlayerKey(player),
			ordinal.ToString(),
			player.Relics.Count.ToString(),
			weights.Silver.ToString(),
			weights.Gold.ToString(),
			weights.Prismatic.ToString());
		return ResolveForgeRarity(weights, roll);
	}

	private static HextechForgeRarityWeights GetModifiedForgeRarityWeights(
		Player player,
		int silverWeight,
		int goldWeight,
		int prismaticWeight)
	{
		HextechForgeRarityWeights weights = new(silverWeight, goldWeight, prismaticWeight);
		return ApplyDiceManiacForgeRarityModifier(weights, player.GetRelic<DiceManiacRune>() != null);
	}

	private static HextechForgeRarityWeights GetBaseForgeRarityWeights(Player player)
	{
		try
		{
			if (player.RunState is RunState runState
				&& runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is HextechMayhemModifier modifier)
			{
				return modifier.ForgeRarityWeights;
			}
		}
		catch (InvalidOperationException ex)
		{
			if (HextechRunLogBudget.TryConsume("forge.rarity-config-fallback", 3))
			{
				Log.Warn(
					$"[{ModInfo.Id}][Forge] Could not read synchronized forge rarity weights; "
					+ $"using local configuration fallback: {ex.GetType().Name}: {ex.Message}");
			}
		}

		return HextechRuneConfiguration.GetSnapshot().ForgeRarityWeights;
	}

	internal static bool IsForgeDisabledForPlayer(Player player, RelicModel forge)
	{
		string entry = (forge.CanonicalInstance?.Id ?? forge.Id).Entry;
		return GetEffectiveDisabledForgeIds(player).Contains(entry);
	}

	private static IReadOnlySet<string> GetEffectiveDisabledForgeIds(Player player)
	{
		try
		{
			if (player.RunState is RunState runState
				&& runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is HextechMayhemModifier modifier)
			{
				return modifier.DisabledForgeIdsForPool;
			}
		}
		catch (InvalidOperationException ex)
		{
			if (HextechRunLogBudget.TryConsume("forge.disabled-config-fallback", 3))
			{
				Log.Warn(
					$"[{ModInfo.Id}][Forge] Could not read synchronized disabled forge IDs; "
					+ $"using local configuration fallback: {ex.GetType().Name}: {ex.Message}");
			}
		}

		return HextechRuneConfiguration.GetDisabledForgeIds();
	}

	private static HextechForgeRarityWeights NormalizeForgeRarityWeights(HextechForgeRarityWeights weights)
	{
		return new HextechForgeRarityWeights(
			Math.Max(0, weights.Silver),
			Math.Max(0, weights.Gold),
			Math.Max(0, weights.Prismatic));
	}

	private static HextechRarityTier ResolveForgeRarity(HextechForgeRarityWeights weights, int roll)
	{
		if (roll < weights.Silver)
		{
			return HextechRarityTier.Silver;
		}

		if (roll < weights.Silver + weights.Gold)
		{
			return HextechRarityTier.Gold;
		}

		return HextechRarityTier.Prismatic;
	}
}
