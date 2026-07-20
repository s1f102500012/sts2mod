namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrack(HextechMayhemModifier modifier, Player player, IReadOnlyList<RelicModel> currentOptions, int slotIndex, HashSet<ModelId> seenOptionIds)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOption(player, (RunState)player.RunState, currentOptions, slotIndex, seenOptionIds, modifier.IsEndlessLoopActive);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			modifier.RecordSeenPlayerRunes(player, [ rerolled[slotIndex] ]);
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOption(
		Player player,
		RunState runState,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		HashSet<ModelId> seenOptionIds,
		bool useEndlessTagWindow)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> currentOptionIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> excludedIds = new(currentOptionIds);
		excludedIds.UnionWith(seenOptionIds);
		HextechRarityTier rarity = GetRarityForOptions(currentOptions);
		List<RelicModel> candidates = ConstrainRerollCandidates(
			player,
			BuildSelectableRunePool(player, rarity, runState, excludedIds),
			currentOptions,
			slotIndex);
		if (candidates.Count == 0 && seenOptionIds.Count > 0)
		{
			// 池被「已见」清空:重置(清空)已见集,让重随能重新刷到此前见过的符文(仍排除当前选项)。
			seenOptionIds.Clear();
			candidates = ConstrainRerollCandidates(
				player,
				BuildSelectableRunePool(player, rarity, runState, currentOptionIds),
				currentOptions,
				slotIndex);
		}

		if (candidates.Count == 0)
		{
			return currentOptions;
		}

		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		List<int> weights = BuildRuneTagWeights(candidates, tagCounts, useEndlessTagWindow, out int totalWeight);
		int selectedIndex = SelectWeightedIndex(weights, runState.Rng.Niche.NextInt(totalWeight));
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = CreateSelectableRuneOption(player, candidates[selectedIndex]);
		return updated;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrackMultiplayer(HextechMayhemModifier modifier, Player player, IReadOnlyList<RelicModel> currentOptions, int slotIndex, int rerollOrdinal, HashSet<ModelId> seenOptionIds)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOptionMultiplayer(player, currentOptions, slotIndex, rerollOrdinal, seenOptionIds, modifier.IsEndlessLoopActive);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			modifier.RecordSeenPlayerRunes(player, [ rerolled[slotIndex] ]);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RerollSingleOptionMultiplayer: player={player.NetId} slot={slotIndex} ordinal={rerollOrdinal} relic={rerolledId.Entry}");
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionMultiplayer(
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		int rerollOrdinal,
		HashSet<ModelId> seenOptionIds,
		bool useEndlessTagWindow)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> currentOptionIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> excludedIds = new(currentOptionIds);
		excludedIds.UnionWith(seenOptionIds);

		HextechRarityTier rarity = GetRarityForOptions(currentOptions);
		RunState runState = (RunState)player.RunState;
		List<RelicModel> pool = ConstrainRerollCandidates(
				player,
				BuildSelectableRunePool(player, rarity, runState, excludedIds),
				currentOptions,
				slotIndex)
			.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
			.ToList();
		if (pool.Count == 0 && seenOptionIds.Count > 0)
		{
			// 池被「已见」清空:重置(清空)已见集,让重随能重新刷到此前见过的符文(仍排除当前选项)。
			seenOptionIds.Clear();
			pool = ConstrainRerollCandidates(
					player,
					BuildSelectableRunePool(player, rarity, runState, currentOptionIds),
					currentOptions,
					slotIndex)
				.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
				.ToList();
		}

		if (pool.Count == 0)
		{
			return currentOptions;
		}

		int index = GetMultiplayerRerollIndex(player, pool, rarity, slotIndex, rerollOrdinal, useEndlessTagWindow);
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = CreateSelectableRuneOption(player, pool[index]);
		return updated;
	}

	private static List<RelicModel> ConstrainRerollCandidates(
		Player player,
		IEnumerable<RelicModel> candidates,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex)
	{
		bool upgradeAlreadyPresent = currentOptions
			.Where((_, index) => index != slotIndex)
			.Any(HextechRunePoolBuilder.IsUpgradeRune);
		PlayerRuneCharacterPool? characterPool = HextechPlayerContextHelper.TryGetRuneCharacterPool(
			player,
			out PlayerRuneCharacterPool resolvedCharacterPool)
				? resolvedCharacterPool
				: null;
		return HextechRunePoolBuilder.ConstrainCandidatesForSlot(
			candidates,
			characterPool,
			slotIndex,
			upgradeAlreadyPresent);
	}

	private static int GetMultiplayerRerollIndex(
		Player player,
		IReadOnlyList<RelicModel> pool,
		HextechRarityTier rarity,
		int slotIndex,
		int rerollOrdinal,
		bool useEndlessTagWindow)
	{
		RunState runState = (RunState)player.RunState;
		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		List<int> weights = BuildRuneTagWeights(pool, tagCounts, useEndlessTagWindow, out int totalWeight);
		List<string> parts =
		[
			runState.Rng.StringSeed,
			"|act:",
			runState.CurrentActIndex.ToString(),
			"|player:",
			HextechStableRandom.PlayerKey(player),
			"|rarity:",
			((int)rarity).ToString(),
			"|slot:",
			slotIndex.ToString(),
			"|ordinal:",
			rerollOrdinal.ToString()
		];
		for (int i = 0; i < pool.Count; i++)
		{
			parts.Add("|pool:");
			parts.Add((pool[i].CanonicalInstance?.Id ?? pool[i].Id).Entry);
			parts.Add(":");
			parts.Add(weights[i].ToString());
		}

		int roll = HextechStableRandom.IndexFromRawParts(totalWeight, parts.ToArray());
		return SelectWeightedIndex(weights, roll);
	}
}
