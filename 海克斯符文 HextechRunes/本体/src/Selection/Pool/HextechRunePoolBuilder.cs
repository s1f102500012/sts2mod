namespace HextechRunes;

internal static class HextechRunePoolBuilder
{
	internal const int CharacterReservedSlotIndex = 0;
	private const int RuneTagBiasBaseWeight = 100;
	private const int RuneTagBiasNormalBonusPerMatch = 25;
	private const int RuneTagBiasEndlessBonusPerMatch = 20;
	private const int RuneTagBiasMaxBonus = 50;
	private const int RuneTagBiasEndlessHistoryWindow = 3;

	public static List<RelicModel> BuildSelectableRunePool(Player player, HextechRarityTier rarity, RunState runState, IReadOnlySet<ModelId>? excludedIds = null)
	{
		HashSet<ModelId> ownedIds = player.Relics
			.Where(HextechCatalog.IsHextechRelic)
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> blockedOwnedIds = ownedIds.ToHashSet();
		blockedOwnedIds.UnionWith(HextechCatalog.GetMutuallyExclusivePlayerRuneIds(ownedIds));
		bool applyConfiguration = ShouldApplyPlayerRuneConfiguration(player);

		List<RelicModel> pool = (applyConfiguration
				? HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(rarity)
				: HextechCatalog.GetPlayerRuneTypesForRarity(rarity))
			.Where(HextechRuntimeRuneCompatibility.IsPlayerRuneAvailableForCurrentRuntime)
			.Where(type => HextechCatalog.IsPlayerRuneAllowedInAct(type, runState.CurrentActIndex))
			.Select(static type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)))
			.Where(relic => HextechCatalog.IsAvailableForPlayer(relic, player)
				&& !blockedOwnedIds.Contains(relic.CanonicalInstance?.Id ?? relic.Id)
				&& (excludedIds == null || !excludedIds.Contains(relic.CanonicalInstance?.Id ?? relic.Id)))
			.ToList();

		return applyConfiguration ? ApplyPlayerRuneConfiguration(pool, rarity, runState) : pool;
	}

	public static List<RelicModel> BuildSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		List<RelicModel> pool = BuildSelectableRunePool(player, rarity, runState, excludedIds);
		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);

		List<RelicModel> options = [];
		int picks = Math.Min(3, pool.Count);
		for (int i = 0; i < picks; i++)
		{
			bool upgradeAlreadySelected = options.Any(IsUpgradeRune);
			List<RelicModel> candidates = ConstrainCandidatesForSlot(
				pool,
				GetRuneCharacterPool(player),
				i,
				upgradeAlreadySelected);
			if (candidates.Count == 0)
			{
				break;
			}

			List<int> weights = BuildRuneTagWeights(candidates, tagCounts, useEndlessTagWindow, out int totalWeight);
			int index = SelectWeightedIndex(weights, runState.Rng.Niche.NextInt(totalWeight));
			RelicModel selected = candidates[index];
			options.Add(CreateSelectableRuneOption(player, selected));
			RemoveById(pool, GetRelicId(selected));
		}

		return options;
	}

	public static List<RelicModel> BuildStableSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		// excludedIds 在这里是「已展示过(seen)」的룬集合,用来避免同一局里反复刷到见过的룬。但在长局/无尽里,
		// 当某稀有度的룬几乎都被展示过时,这层排除会把可选池清空——返回 0 个选项。空选项会在后续 options[0]
		// 之类访问处崩溃/中断,在联机重连、重开、重掷导致选择重建时表现为「选项被全部清掉 / 选择被初始化」。
		// 兜底:若「已见」排除清空了池,就放宽到忽略「已见」(仍排除已拥有/互斥/禁用),保证始终有可选项;
		// 同时把用于稳定随机的 salt 也一致地忽略「已见」,使重连/重开重建时能复现同一组选项(幂等、不再跳变)。
		IReadOnlySet<ModelId>? effectiveExcludedIds = excludedIds;
		List<RelicModel> pool = BuildSelectableRunePool(player, rarity, runState, effectiveExcludedIds);
		if (pool.Count == 0 && excludedIds is { Count: > 0 })
		{
			List<RelicModel> fallbackPool = BuildSelectableRunePool(player, rarity, runState, null);
			if (fallbackPool.Count > 0)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] {rarity} rune option pool exhausted by seen-history; falling back to the full pool (ignoring seen) so the selection is not emptied.", 2);
				pool = fallbackPool;
				effectiveExcludedIds = null;
			}
		}

		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		int picks = Math.Min(3, pool.Count);
		return PickStableWeightedDistinct(
			player,
			pool,
			picks,
			runState,
			tagCounts,
			useEndlessTagWindow,
			"rune-selection-options",
			runState.CurrentActIndex.ToString(),
			HextechStableRandom.PlayerKey(player),
			((int)rarity).ToString(),
			effectiveExcludedIds == null ? "" : string.Join(",", effectiveExcludedIds.Select(static id => id.Entry).OrderBy(static entry => entry, StringComparer.Ordinal)))
			.Select(relic => CreateSelectableRuneOption(player, relic))
			.ToList();
	}

	public static Dictionary<string, int> BuildOwnedRuneTagCounts(Player player, bool useEndlessTagWindow)
	{
		List<RelicModel> ownedRunes = player.Relics
			.Where(HextechCatalog.IsHextechRelic)
			.ToList();
		int startIndex = useEndlessTagWindow
			? Math.Max(0, ownedRunes.Count - RuneTagBiasEndlessHistoryWindow)
			: 0;

		Dictionary<string, int> counts = new(StringComparer.Ordinal);
		for (int i = startIndex; i < ownedRunes.Count; i++)
		{
			string tagKey = HextechCatalog.GetPlayerRuneTagKey(ownedRunes[i]);
			counts[tagKey] = counts.TryGetValue(tagKey, out int count) ? count + 1 : 1;
		}

		return counts;
	}

	public static List<int> BuildRuneTagWeights(
		IReadOnlyList<RelicModel> pool,
		IReadOnlyDictionary<string, int> tagCounts,
		bool useEndlessTagWindow,
		out int totalWeight)
	{
		List<int> weights = new(pool.Count);
		totalWeight = 0;
		foreach (RelicModel relic in pool)
		{
			int weight = GetRuneTagWeight(relic, tagCounts, useEndlessTagWindow);
			weights.Add(weight);
			totalWeight += weight;
		}

		return weights;
	}

	public static int SelectWeightedIndex(IReadOnlyList<int> weights, int roll)
	{
		for (int i = 0; i < weights.Count; i++)
		{
			if (roll < weights[i])
			{
				return i;
			}

			roll -= weights[i];
		}

		return Math.Max(0, weights.Count - 1);
	}

	public static RelicModel CreateSelectableRuneOption(Player player, RelicModel relic)
	{
		RelicModel option = relic.ToMutable();
		RefreshPlayerContextualRuneDescription(player, option);
		return option;
	}

	internal static bool IsUpgradeRune(RelicModel relic)
	{
		Type type = (relic.CanonicalInstance ?? relic).GetType();
		return type.Name.EndsWith("UpgradeRune", StringComparison.Ordinal);
	}

	internal static List<RelicModel> ConstrainCandidatesForSlot(
		IEnumerable<RelicModel> candidates,
		PlayerRuneCharacterPool? playerCharacterPool,
		int slotIndex,
		bool upgradeAlreadySelected)
	{
		List<RelicModel> eligible = candidates
			.Where(relic => !upgradeAlreadySelected || !IsUpgradeRune(relic))
			.ToList();
		if (slotIndex != CharacterReservedSlotIndex)
		{
			return eligible;
		}

		List<RelicModel> characterCandidates = eligible
			.Where(relic => IsRuneForCharacter(relic, playerCharacterPool))
			.ToList();
		if (characterCandidates.Count > 0)
		{
			return characterCandidates;
		}

		List<RelicModel> genericCandidates = eligible
			.Where(IsGenericRune)
			.ToList();
		return genericCandidates.Count > 0 ? genericCandidates : eligible;
	}

	public static HextechRarityTier GetRarityForOptions(IReadOnlyList<RelicModel> relics)
	{
		if (relics.Count == 0)
		{
			return HextechRarityTier.Gold;
		}

		ModelId id = relics[0].CanonicalInstance?.Id ?? relics[0].Id;
		if (HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(HextechRarityTier.Silver).Any(type => ModelDb.GetId(type) == id))
		{
			return HextechRarityTier.Silver;
		}

		if (HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(HextechRarityTier.Prismatic).Any(type => ModelDb.GetId(type) == id))
		{
			return HextechRarityTier.Prismatic;
		}

		return HextechRarityTier.Gold;
	}

	private static List<RelicModel> ApplyPlayerRuneConfiguration(List<RelicModel> pool, HextechRarityTier rarity, RunState runState)
	{
		IReadOnlySet<string> disabledIds = GetEffectiveDisabledPlayerRuneIds(runState);
		if (disabledIds.Count == 0)
		{
			return pool;
		}

		List<RelicModel> configuredPool = pool
			.Where(relic =>
			{
				ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
				return !disabledIds.Contains(id.Entry);
			})
			.ToList();
		bool hasAnyEnabledRarity = Enum.GetValues<HextechRarityTier>()
			.Any(enabledRarity => HasEnabledConfigurablePlayerRuneForRarity(enabledRarity, disabledIds));
		bool rarityDisabledByConfig = hasAnyEnabledRarity && !HasEnabledConfigurablePlayerRuneForRarity(rarity, disabledIds);
		if (configuredPool.Count > 0 || pool.Count == 0 || rarityDisabledByConfig)
		{
			return configuredPool;
		}

		Log.Warn($"[{ModInfo.Id}][RuneConfig] Player rune config filtered all {rarity} options; falling back to the default pool for this roll.", 2);
		return pool;
	}

	internal static bool ShouldApplyPlayerRuneConfiguration(Player player)
	{
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None
			or NetGameType.Host or NetGameType.Client)
		{
			return true;
		}

		return false;
	}

	internal static IReadOnlySet<string> GetEffectiveDisabledPlayerRuneIds(RunState runState)
	{
		if (runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() is HextechMayhemModifier modifier)
		{
			return modifier.PlayerRuneConfigDisabledIds;
		}

		try
		{
			if (HextechPlayerContextHelper.IsClientRun(fallbackWhenUnavailable: true))
			{
				return new HashSet<string>(StringComparer.Ordinal);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][RuneConfig] Failed to read multiplayer service while resolving disabled player runes; using deterministic empty fallback: {ex}");
			return new HashSet<string>(StringComparer.Ordinal);
		}

		return HextechRuneConfiguration.GetDisabledPlayerRuneIds();
	}

	internal static IReadOnlyList<HextechRarityTier> GetEnabledPlayerRuneRarities(RunState runState)
	{
		IReadOnlySet<string> disabledIds = GetEffectiveDisabledPlayerRuneIds(runState);
		return GetEnabledPlayerRuneRaritiesForDisabledIds(disabledIds);
	}

	internal static IReadOnlyList<HextechRarityTier> GetEnabledPlayerRuneRaritiesForDisabledIds(IReadOnlySet<string> disabledIds)
	{
		HextechRarityTier[] enabledRarities = Enum.GetValues<HextechRarityTier>()
			.Where(rarity => HasEnabledConfigurablePlayerRuneForRarity(rarity, disabledIds))
			.ToArray();

		return enabledRarities.Length > 0
			? enabledRarities
			: Enum.GetValues<HextechRarityTier>();
	}

	private static bool HasEnabledConfigurablePlayerRuneForRarity(HextechRarityTier rarity, IReadOnlySet<string> disabledIds)
	{
		return HextechCatalog.GetConfigurablePlayerRuneTypesForRarity(rarity)
			.Where(HextechRuntimeRuneCompatibility.IsPlayerRuneAvailableForCurrentRuntime)
			.Any(type => !disabledIds.Contains(ModelDb.GetId(type).Entry));
	}

	private static List<RelicModel> PickStableWeightedDistinct(
		Player player,
		IEnumerable<RelicModel> candidates,
		int count,
		RunState runState,
		IReadOnlyDictionary<string, int> tagCounts,
		bool useEndlessTagWindow,
		params string?[] saltParts)
	{
		List<RelicModel> pool = candidates
			.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
			.ToList();
		List<RelicModel> selected = new(Math.Min(Math.Max(0, count), pool.Count));
		for (int i = 0; i < count && pool.Count > 0; i++)
		{
			bool upgradeAlreadySelected = selected.Any(IsUpgradeRune);
			List<RelicModel> slotCandidates = ConstrainCandidatesForSlot(
				pool,
				GetRuneCharacterPool(player),
				i,
				upgradeAlreadySelected);
			if (slotCandidates.Count == 0)
			{
				break;
			}

			List<int> weights = BuildRuneTagWeights(slotCandidates, tagCounts, useEndlessTagWindow, out int totalWeight);
			string poolKey = BuildWeightedPoolKey(slotCandidates, weights);
			int roll = HextechStableRandom.Index(
				runState,
				totalWeight,
				AppendSelectionSalt(
					saltParts,
					"pick",
					i.ToString(),
					"slot-role",
					i == CharacterReservedSlotIndex ? "character-reserved" : "open",
					"player",
					HextechStableRandom.PlayerKey(player),
					"pool",
					poolKey));
			int index = SelectWeightedIndex(weights, roll);
			RelicModel chosen = slotCandidates[index];
			selected.Add(chosen);
			RemoveById(pool, GetRelicId(chosen));
		}

		return selected;
	}

	private static PlayerRuneCharacterPool? GetRuneCharacterPool(Player player)
	{
		return HextechPlayerContextHelper.TryGetRuneCharacterPool(player, out PlayerRuneCharacterPool characterPool)
			? characterPool
			: null;
	}

	private static bool IsRuneForCharacter(RelicModel relic, PlayerRuneCharacterPool? characterPool)
	{
		return characterPool.HasValue
			&& HextechContentRegistry.PlayerRuneMetadata.TryGetRegistration(
				(relic.CanonicalInstance ?? relic).GetType(),
				out PlayerRuneRegistration registration)
			&& registration.CharacterPool == characterPool;
	}

	private static bool IsGenericRune(RelicModel relic)
	{
		return HextechContentRegistry.PlayerRuneMetadata.TryGetRegistration(
				(relic.CanonicalInstance ?? relic).GetType(),
				out PlayerRuneRegistration registration)
			&& !registration.CharacterPool.HasValue;
	}

	private static ModelId GetRelicId(RelicModel relic)
	{
		return relic.CanonicalInstance?.Id ?? relic.Id;
	}

	private static void RemoveById(List<RelicModel> relics, ModelId id)
	{
		relics.RemoveAll(relic => GetRelicId(relic) == id);
	}

	private static int GetRuneTagWeight(RelicModel relic, IReadOnlyDictionary<string, int> tagCounts, bool useEndlessTagWindow)
	{
		string tagKey = HextechCatalog.GetPlayerRuneTagKey(relic);
		int weight = RuneTagBiasBaseWeight;
		if (tagCounts.TryGetValue(tagKey, out int matchingCount) && matchingCount > 0)
		{
			int bonusPerMatch = useEndlessTagWindow
				? RuneTagBiasEndlessBonusPerMatch
				: RuneTagBiasNormalBonusPerMatch;
			weight += Math.Min(RuneTagBiasMaxBonus, matchingCount * bonusPerMatch);
		}

		// (0.8.4 起升级卡牌类符文不再额外加权:刷新门槛已移除、改为获得时补目标卡,
		// 全量进池后再 ×2 会让升级类淹没三选一。)
		return weight;
	}

	private static string BuildWeightedPoolKey(IReadOnlyList<RelicModel> pool, IReadOnlyList<int> weights)
	{
		return string.Join(",", pool.Select((relic, index) => $"{(relic.CanonicalInstance?.Id ?? relic.Id).Entry}:{weights[index]}"));
	}

	private static string?[] AppendSelectionSalt(string?[] saltParts, params string?[] extra)
	{
		string?[] result = new string?[saltParts.Length + extra.Length];
		Array.Copy(saltParts, result, saltParts.Length);
		Array.Copy(extra, 0, result, saltParts.Length, extra.Length);
		return result;
	}

	private static void RefreshPlayerContextualRuneDescription(Player player, RelicModel relic)
	{
		if (relic is FlyingKickRune flyingKickRune)
		{
			flyingKickRune.RefreshExecutePercent(player.Creature.MaxHp);
		}
	}
}
