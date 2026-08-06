namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static List<RelicModel> BuildSelectableRunePool(Player player, HextechRarityTier rarity, RunState runState, IReadOnlySet<ModelId>? excludedIds = null)
	{
		return HextechRunePoolBuilder.BuildSelectableRunePool(player, rarity, runState, excludedIds);
	}

	private static List<RelicModel> BuildSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		return HextechRunePoolBuilder.BuildSelectableRunesForRarity(player, rarity, runState, excludedIds, useEndlessTagWindow);
	}

	private static List<RelicModel> BuildStableSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		int selectionStageIndex,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		return HextechRunePoolBuilder.BuildStableSelectableRunesForRarity(player, rarity, runState, selectionStageIndex, excludedIds, useEndlessTagWindow);
	}

	private static Dictionary<string, int> BuildOwnedRuneTagCounts(Player player, bool useEndlessTagWindow)
	{
		return HextechRunePoolBuilder.BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
	}

	private static List<int> BuildRuneTagWeights(
		IReadOnlyList<RelicModel> pool,
		IReadOnlyDictionary<string, int> tagCounts,
		bool useEndlessTagWindow,
		out int totalWeight)
	{
		return HextechRunePoolBuilder.BuildRuneTagWeights(pool, tagCounts, useEndlessTagWindow, out totalWeight);
	}

	private static int SelectWeightedIndex(IReadOnlyList<int> weights, int roll)
	{
		return HextechRunePoolBuilder.SelectWeightedIndex(weights, roll);
	}

	private static RelicModel CreateSelectableRuneOption(Player player, RelicModel relic)
	{
		return HextechRunePoolBuilder.CreateSelectableRuneOption(player, relic);
	}

	private static HashSet<ModelId> CreateBaseExcludedIds(HextechMayhemModifier modifier, Player player, RelicModel? monsterHexRelic)
	{
		HashSet<ModelId> excludedIds = modifier.GetSeenPlayerRuneIds(player);
		if (monsterHexRelic != null)
		{
			excludedIds.Add(monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id);
		}

		return excludedIds;
	}

	private static HashSet<ModelId> CreateBaseExcludedIds(HextechMayhemModifier modifier, Player player, IEnumerable<MonsterHexKind> monsterHexes)
	{
		HashSet<ModelId> excludedIds = modifier.GetSeenPlayerRuneIds(player);
		AddMonsterHexIconIds(excludedIds, monsterHexes);
		return excludedIds;
	}

	private static HashSet<ModelId> CreateSeenOptionIds(IEnumerable<RelicModel> options, RelicModel? monsterHexRelic, IEnumerable<ModelId>? alreadySeenIds = null)
	{
		HashSet<ModelId> seenOptionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		if (alreadySeenIds != null)
		{
			seenOptionIds.UnionWith(alreadySeenIds);
		}

		if (monsterHexRelic != null)
		{
			seenOptionIds.Add(monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id);
		}

		return seenOptionIds;
	}

	private static void AddMonsterHexIconIds(HashSet<ModelId> ids, IEnumerable<MonsterHexKind>? monsterHexes)
	{
		if (monsterHexes == null)
		{
			return;
		}

		foreach (MonsterHexKind monsterHex in monsterHexes)
		{
			ids.Add(GetMonsterHexIconRelicId(monsterHex));
		}
	}

	private static MonsterHexKind? FirstMonsterHexOrNull(IEnumerable<MonsterHexKind>? monsterHexes)
	{
		if (monsterHexes == null)
		{
			return null;
		}

		foreach (MonsterHexKind monsterHex in monsterHexes)
		{
			return monsterHex;
		}

		return null;
	}

	private static HashSet<ModelId> CreateEnemyHexRerollExcludedIds(IEnumerable<RelicModel> options)
	{
		return options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
	}

	private static HashSet<ModelId> CreateEnemyHexRerollExcludedIds(IReadOnlySet<ModelId> baseExcludedIds, IReadOnlyList<MonsterHexKind?> currentMonsterHexes, int rerollSlotIndex)
	{
		HashSet<ModelId> excludedIds = baseExcludedIds.ToHashSet();
		for (int i = 0; i < currentMonsterHexes.Count; i++)
		{
			if (i != rerollSlotIndex && currentMonsterHexes[i].HasValue)
			{
				excludedIds.Add(GetMonsterHexIconRelicId(currentMonsterHexes[i]!.Value));
			}
		}

		return excludedIds;
	}

	private static HextechRarityTier GetRarityForOptions(IReadOnlyList<RelicModel> relics)
	{
		return HextechRunePoolBuilder.GetRarityForOptions(relics);
	}

	public static void RemoveRunesFromGrabBags(Player player)
	{
		foreach (RelicModel relic in HextechCatalog.GetCanonicalRunes())
		{
			player.RelicGrabBag.Remove(relic);
			player.RunState.SharedRelicGrabBag.Remove(relic);
		}
	}

	private static bool IsCurrentRun(RunState runState)
	{
		return ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), runState);
	}
}
