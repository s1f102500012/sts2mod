using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static HextechRarityTier RollRandomRarity(HextechMayhemModifier modifier, int actIndex, RunState runState, IReadOnlyList<HextechRarityTier> enabledRarities)
	{
		if (actIndex == 0)
		{
			HextechRarityWeights weights = modifier.FirstActRuneRarityWeights;
			return RollWeightedRarity(runState, weights.Silver, weights.Gold, weights.Prismatic, deterministic: false, actIndex, enabledRarities);
		}

		if (actIndex == 1 && modifier.GetRarityForAct(0) == HextechRarityTier.Silver)
		{
			HextechRarityWeights weights = modifier.SecondActAfterSilverRuneRarityWeights;
			return RollWeightedRarity(runState, weights.Silver, weights.Gold, weights.Prismatic, deterministic: false, actIndex, enabledRarities);
		}

		HextechRarityWeights normalWeights = modifier.NormalRuneRarityWeights;
		return RollWeightedRarity(runState, normalWeights.Silver, normalWeights.Gold, normalWeights.Prismatic, deterministic: false, actIndex, enabledRarities);
	}

	private static HextechRarityTier RollStableRarity(HextechMayhemModifier modifier, int actIndex, RunState runState, IReadOnlyList<HextechRarityTier> enabledRarities)
	{
		if (actIndex == 0)
		{
			HextechRarityWeights weights = modifier.FirstActRuneRarityWeights;
			return RollWeightedRarity(runState, weights.Silver, weights.Gold, weights.Prismatic, deterministic: true, actIndex, enabledRarities);
		}

		if (actIndex == 1 && modifier.GetRarityForAct(0) == HextechRarityTier.Silver)
		{
			HextechRarityWeights weights = modifier.SecondActAfterSilverRuneRarityWeights;
			return RollWeightedRarity(runState, weights.Silver, weights.Gold, weights.Prismatic, deterministic: true, actIndex, enabledRarities);
		}

		HextechRarityWeights normalWeights = modifier.NormalRuneRarityWeights;
		return RollWeightedRarity(runState, normalWeights.Silver, normalWeights.Gold, normalWeights.Prismatic, deterministic: true, actIndex, enabledRarities);
	}

	private static async Task<(HextechRarityTier Rarity, MonsterHexKind? MonsterHex)> ResolveActRoll(RunState runState, HextechMayhemModifier modifier, int actIndex)
	{
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		bool isMultiplayer = gameType is NetGameType.Host or NetGameType.Client;
		IReadOnlySet<string> localDisabledPlayerRuneIds = HextechRuneConfiguration.GetDisabledPlayerRuneIds();
		HextechRunConfigurationSnapshot localRunConfigSnapshot = modifier.GetEffectiveRunConfigurationSnapshot();

		HextechRarityTier? savedRarity = modifier.GetRarityForAct(actIndex);
		HextechRarityTier? forcedRarity = HextechCustomRunModifierHooks.GetForcedRarity(runState);
		IReadOnlyList<HextechRarityTier> enabledRarities = HextechRunePoolBuilder.GetEnabledPlayerRuneRarities(runState);
		HextechRarityTier? effectiveForcedRarity = forcedRarity.HasValue && enabledRarities.Contains(forcedRarity.Value)
			? forcedRarity
			: null;
		HextechRarityTier localRarity = savedRarity
			?? effectiveForcedRarity
			?? (isMultiplayer ? RollStableRarity(modifier, actIndex, runState, enabledRarities) : RollRandomRarity(modifier, actIndex, runState, enabledRarities));
		if (!savedRarity.HasValue && effectiveForcedRarity.HasValue)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll forced rarity: act={actIndex} rarity={localRarity}");
		}
		else if (!savedRarity.HasValue && forcedRarity.HasValue)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll ignored disabled forced rarity: act={actIndex} forced={forcedRarity} enabled={string.Join(",", enabledRarities)} rarity={localRarity}");
		}
		else if (!savedRarity.HasValue && enabledRarities.Count < Enum.GetValues<HextechRarityTier>().Length)
		{
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll rarity pool filtered by player rune config: act={actIndex} enabled={string.Join(",", enabledRarities)} rarity={localRarity}");
		}

		IReadOnlyList<MonsterHexKind> previousHexes = modifier.GetActiveMonsterHexesBeforeAct(actIndex);
		int newEnemyHexCount = modifier.GetEnemyHexCountForAct(actIndex);
		MonsterHexKind? savedPrimaryMonsterHex = modifier.GetMonsterHexesForAct(actIndex)
			.Where(hex => !previousHexes.Contains(hex))
			.Cast<MonsterHexKind?>()
			.FirstOrDefault();
		MonsterHexKind? localMonsterHex = newEnemyHexCount <= 0
			? null
			: savedPrimaryMonsterHex
				?? (isMultiplayer
					? ChooseStableMonsterHexForAct(modifier, localRarity, runState, actIndex, previousHexes)
					: ChooseMonsterHexForAct(modifier, localRarity, runState, previousHexes));
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll enemy count: act={actIndex} newCount={newEnemyHexCount} previous={previousHexes.Count} primary={localMonsterHex}");

		if (gameType is NetGameType.Singleplayer or NetGameType.None or NetGameType.Replay)
		{
			modifier.SetRarityForAct(actIndex, localRarity);
			if (!modifier.HasPlayerRuneConfigDisabledIdsSnapshot)
			{
				modifier.SetPlayerRuneConfigDisabledIdsSnapshot(localDisabledPlayerRuneIds, $"local act-roll act={actIndex}");
			}

			modifier.SetRunConfigurationSnapshot(localRunConfigSnapshot, $"local act-roll act={actIndex}");
			modifier.HostUsesBetterMultiplayerScaling = false;
			return (localRarity, localMonsterHex);
		}

		PlayerChoiceSynchronizer synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		Player authorityPlayer = GetActRollAuthorityPlayer(runManager, runState)
			?? throw CreateProtocolFailure(
				$"act-roll act={actIndex}",
				$"No act-roll authority is available for multiplayer act={actIndex}.");
		if (!IsCurrentRun(runState) || !IsMultiplayerConnected())
		{
			throw new OperationCanceledException(
				$"Multiplayer act-roll transaction is no longer active for act={actIndex}.");
		}

		uint choiceId = synchronizer.ReserveChoiceId(authorityPlayer);
		if (gameType == NetGameType.Host)
		{
			try
			{
				bool hostUsesExternalScaling = HextechMultiplayerScalingCompat.IsBetterMultiplayerScalingLoaded();
				modifier.HostUsesBetterMultiplayerScaling = hostUsesExternalScaling;
				if (!modifier.HasPlayerRuneConfigDisabledIdsSnapshot)
				{
					modifier.SetPlayerRuneConfigDisabledIdsSnapshot(localDisabledPlayerRuneIds, $"host act-roll act={actIndex}");
				}

				HextechRunConfigurationSnapshot hostSnapshot = modifier.GetEffectiveRunConfigurationSnapshot();
				uint sentChoiceId = SyncLocalHextechChoice(
					synchronizer,
					authorityPlayer,
					choiceId,
					HextechChoiceCodec.CreateActRoll(
						actIndex,
						localRarity,
						localMonsterHex,
						hostUsesExternalScaling,
						modifier.EnemyHexCountsByAct,
						modifier.PlayerRuneConfigDisabledIds,
						hostSnapshot),
					$"act-roll act={actIndex}");

				modifier.SetRarityForAct(actIndex, localRarity);
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll host sync: act={actIndex} choiceId={sentChoiceId} authority={authorityPlayer.NetId} rarity={localRarity} monsterHex={localMonsterHex} playerCounts={string.Join(",", hostSnapshot.PlayerHexCountsByAct)} enemyCounts={string.Join(",", modifier.EnemyHexCountsByAct)} playerConfigDisabled={modifier.PlayerRuneConfigDisabledIds.Count} enemyConfigDisabled={hostSnapshot.DisabledMonsterHexIds.Count} forgeConfigDisabled={hostSnapshot.DisabledForgeIds.Count} betterMultiplayerScaling={hostUsesExternalScaling}");
				return (localRarity, localMonsterHex);
			}
			catch (HextechChoiceProtocolException)
			{
				throw;
			}
			catch (Exception ex)
			{
				string message =
					$"Host act-roll transaction failed after reserving choice: " +
					$"act={actIndex} player={authorityPlayer.NetId} choiceId={choiceId}";
				throw CreateProtocolFailure($"act-roll act={actIndex}", message, ex);
			}
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = await WaitForRemoteHextechChoice(
			synchronizer,
			runState,
			authorityPlayer,
			choiceId,
			result => HextechChoiceCodec.TryDecodeActRoll(result, actIndex, out _, out _, out _, out _, out _, out _),
			$"act-roll act={actIndex}");
		if (!HextechChoiceCodec.TryDecodeActRoll(remoteChoice, actIndex, out HextechRarityTier syncedRarity, out MonsterHexKind? syncedMonsterHex, out bool syncedHostUsesExternalScaling, out int[] syncedEnemyHexCountsByAct, out HashSet<string> syncedDisabledPlayerRuneIds, out HextechRunConfigurationSnapshot syncedRunConfigSnapshot))
		{
			throw CreateProtocolFailure(
				$"act-roll act={actIndex}",
				$"Malformed host act-roll payload: act={actIndex} player={authorityPlayer.NetId} choiceId={receivedChoiceId}");
		}

		modifier.SetRarityForAct(actIndex, syncedRarity);
		modifier.SetEnemyHexCountsByActSnapshot(syncedEnemyHexCountsByAct, $"host act-roll act={actIndex}");
		modifier.SetPlayerRuneConfigDisabledIdsSnapshot(syncedDisabledPlayerRuneIds, $"host act-roll act={actIndex}");
		modifier.SetRunConfigurationSnapshot(syncedRunConfigSnapshot with
		{
			EnemyHexCountsByAct = syncedEnemyHexCountsByAct,
			DisabledPlayerRuneIds = syncedDisabledPlayerRuneIds
		}, $"host act-roll act={actIndex}");
		modifier.HostUsesBetterMultiplayerScaling = syncedHostUsesExternalScaling;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll client sync: act={actIndex} choiceId={receivedChoiceId} authority={authorityPlayer.NetId} rarity={syncedRarity} monsterHex={syncedMonsterHex} playerCounts={string.Join(",", modifier.PlayerHexCountsByAct)} enemyCounts={string.Join(",", modifier.EnemyHexCountsByAct)} playerConfigDisabled={modifier.PlayerRuneConfigDisabledIds.Count} enemyConfigDisabled={modifier.DisabledMonsterHexIdsForPool.Count} forgeConfigDisabled={modifier.DisabledForgeIdsForPool.Count} betterMultiplayerScaling={syncedHostUsesExternalScaling} localRarity={localRarity} localMonsterHex={localMonsterHex}");
		return (syncedRarity, syncedMonsterHex);
	}

	private static Player? GetActRollAuthorityPlayer(RunManager runManager, RunState runState)
	{
		if (runManager.NetService.Type == NetGameType.Host)
		{
			return runState.Players.FirstOrDefault(player => player.NetId == runManager.NetService.NetId);
		}

		return runState.Players.FirstOrDefault();
	}

	private static HextechRarityTier RollWeightedRarity(
		RunState runState,
		int silverWeight,
		int goldWeight,
		int prismaticWeight,
		bool deterministic,
		int actIndex,
		IReadOnlyList<HextechRarityTier> enabledRarities)
	{
		HextechRarityWeights weights = HextechRarityRollResolver.ApplyEnabledRarities(
			silverWeight,
			goldWeight,
			prismaticWeight,
			enabledRarities);
		if (weights.Total <= 0)
		{
			return RollUniformRarity(runState, deterministic, actIndex, enabledRarities);
		}

		int roll = deterministic
			? HextechStableRandom.Index(runState, weights.Total, "act-roll-weighted-rarity", actIndex.ToString(), weights.Silver.ToString(), weights.Gold.ToString(), weights.Prismatic.ToString())
			: runState.Rng.Niche.NextInt(weights.Total);
		return HextechRarityRollResolver.ResolveWeighted(weights, roll);
	}

	private static HextechRarityTier RollUniformRarity(RunState runState, bool deterministic, int actIndex, IReadOnlyList<HextechRarityTier> enabledRarities)
	{
		HextechRarityTier[] orderedRarities = HextechRarityRollResolver.GetUniformRarityOrder(enabledRarities);

		if (HextechRarityRollResolver.HasAllRarities(orderedRarities))
		{
			int roll = deterministic
				? HextechStableRandom.Index(runState, 3, "act-roll-rarity", actIndex.ToString())
				: runState.Rng.Niche.NextInt(3);
			return HextechRarityRollResolver.ResolveUniform(orderedRarities, roll);
		}

		int index = deterministic
			? HextechStableRandom.Index(
				runState,
				orderedRarities.Length,
				"act-roll-rarity",
				actIndex.ToString(),
				"enabled",
				string.Join(",", orderedRarities.Select(static rarity => ((int)rarity).ToString()).OrderBy(static value => value, StringComparer.Ordinal)))
			: runState.Rng.Niche.NextInt(orderedRarities.Length);
		return orderedRarities[index];
	}

	private static MonsterHexKind? ChooseMonsterHexForAct(HextechMayhemModifier modifier, HextechRarityTier rarity, RunState runState, IEnumerable<MonsterHexKind>? extraExcludedHexes = null)
	{
		IReadOnlyList<MonsterHexKind> pool = HextechMonsterHexRoller.BuildActPool(rarity, modifier.GetKnownMonsterHexes(), extraExcludedHexes, modifier.DisabledMonsterHexIdsForPool);
		return pool.Count > 0 ? pool[runState.Rng.Niche.NextInt(pool.Count)] : null;
	}

	private static MonsterHexKind? ChooseStableMonsterHexForAct(HextechMayhemModifier modifier, HextechRarityTier rarity, RunState runState, int actIndex, IEnumerable<MonsterHexKind>? extraExcludedHexes = null, int ordinal = 0)
	{
		IReadOnlyList<MonsterHexKind> pool = HextechMonsterHexRoller.BuildActPool(rarity, modifier.GetKnownMonsterHexes(), extraExcludedHexes, modifier.DisabledMonsterHexIdsForPool);
		if (pool.Count == 0)
		{
			return null;
		}

		return pool[HextechStableRandom.Index(
			runState,
			pool.Count,
			"act-roll-monster-hex",
			actIndex.ToString(),
			ordinal.ToString(),
			((int)rarity).ToString(),
			string.Join(",", pool.Select(static kind => ((int)kind).ToString()).OrderBy(static key => key, StringComparer.Ordinal)))];
	}

	private static IReadOnlyList<MonsterHexKind> ResolveNewMonsterHexesForAct(
		HextechMayhemModifier modifier,
		HextechRarityTier rarity,
		RunState runState,
		int actIndex,
		MonsterHexKind? primaryMonsterHex)
	{
		int newEnemyHexCount = modifier.GetEnemyHexCountForAct(actIndex);
		IReadOnlyList<MonsterHexKind> previousHexes = modifier.GetActiveMonsterHexesBeforeAct(actIndex);
		MonsterHexKind[] checkpointedNewHexes = modifier.GetMonsterHexesForAct(actIndex)
			.Where(hex => !previousHexes.Contains(hex))
			.Distinct()
			.ToArray();
		if (checkpointedNewHexes.Length == newEnemyHexCount)
		{
			HextechLog.Info(
				$"[{ModInfo.Id}][Mayhem] ResolveNewMonsterHexesForAct: " +
				$"restored checkpoint act={actIndex} newHexes={string.Join(",", checkpointedNewHexes)}");
			return checkpointedNewHexes;
		}

		NetGameType gameType = RunManager.Instance.NetService.Type;
		bool isMultiplayer = gameType is NetGameType.Host or NetGameType.Client;
		IReadOnlyList<MonsterHexKind> resolvedNewHexes = HextechMonsterHexRoller.ResolveNewMonsterHexes(
			newEnemyHexCount,
			previousHexes,
			primaryMonsterHex,
			(excludedHexes, ordinal) => isMultiplayer
				? ChooseStableMonsterHexForAct(modifier, rarity, runState, actIndex, excludedHexes, ordinal)
				: ChooseMonsterHexForAct(modifier, rarity, runState, excludedHexes));

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveNewMonsterHexesForAct: act={actIndex} newCount={newEnemyHexCount} previous={previousHexes.Count} primary={primaryMonsterHex} newHexes={string.Join(",", resolvedNewHexes)}");
		return resolvedNewHexes;
	}

	private static IReadOnlyList<MonsterHexKind> CombineMonsterHexes(IEnumerable<MonsterHexKind> previousHexes, IEnumerable<MonsterHexKind> newHexes)
	{
		return HextechMonsterHexRoller.CombineActiveHexes(previousHexes, newHexes);
	}

	private static MonsterHexKind? RerollEnemyHexForAct(
		HextechMayhemModifier modifier,
		HextechRarityTier rarity,
		RunState runState,
		int actIndex,
		MonsterHexKind? currentHex,
		int rerollOrdinal,
		IReadOnlySet<ModelId> excludedIconRelicIds)
	{
		IReadOnlyList<MonsterHexKind> pool = HextechMonsterHexRoller.BuildRerollPool(
			rarity,
			modifier.GetKnownMonsterHexes(),
			currentHex,
			excludedIconRelicIds,
			GetMonsterHexIconRelicId,
			modifier.DisabledMonsterHexIdsForPool);
		if (pool.Count == 0)
		{
			return currentHex;
		}

		string poolKey = string.Join(",", pool.Select(static kind => ((int)kind).ToString()).OrderBy(static key => key, StringComparer.Ordinal));
		int index = HextechStableRandom.Index(
			runState,
			pool.Count,
			"enemy-hex-reroll",
			actIndex.ToString(),
			((int)rarity).ToString(),
			(currentHex.HasValue ? ((int)currentHex.Value).ToString() : "none"),
			rerollOrdinal.ToString(),
			poolKey);
		return pool[index];
	}

	private static ModelId GetMonsterHexIconRelicId(MonsterHexKind hex)
	{
		RelicModel relic = MonsterHexCatalog.GetIconRelicForMonsterHex(hex);
		return relic.CanonicalInstance?.Id ?? relic.Id;
	}
}
