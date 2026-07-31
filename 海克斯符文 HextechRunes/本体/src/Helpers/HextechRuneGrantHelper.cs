using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static class HextechRuneGrantHelper
{
	// 这些符文会在 AfterObtained 内立即移除自己或批量替换已有海克斯。
	// 原版宝箱以 fire-and-forget 方式调用 RelicCmd.Obtain，随后仍持有刚发放的遗物实例继续跑动画；
	// 因此棱彩蛋不能把这类符文塞进宝箱奖励，否则实例会在动画结束前从背包消失并卡住宝箱流程。
	private static readonly IReadOnlySet<Type> DestructiveRandomRewardRuneTypes = new HashSet<Type>
	{
		typeof(TransmuteChaosRune),
		typeof(TransmutePrismaticRune),
		typeof(TransmuteGoldRune),
		typeof(PandorasBoxRune)
	};

	internal static bool IsDestructiveRandomRewardRuneType(Type runeType)
	{
		return DestructiveRandomRewardRuneTypes.Contains(runeType);
	}

	public static async Task ObtainRandomRunes(
		Player player,
		IEnumerable<Type> candidateTypes,
		int count,
		string operationContext)
	{
		await ObtainRandomRunes(player, candidateTypes, count, blockedIds: null, operationContext);
	}

	public static async Task ObtainRandomRunes(
		Player player,
		IEnumerable<Type> candidateTypes,
		int count,
		IReadOnlySet<ModelId>? blockedIds,
		string operationContext)
	{
		IReadOnlyList<Type> candidates = candidateTypes as IReadOnlyList<Type> ?? candidateTypes.ToArray();
		if (await TryObtainRandomRunesMultiplayer(player, candidates, count, blockedIds, operationContext))
		{
			return;
		}

		List<ModelId> selectedIds = SelectRandomRuneIds(player, candidates, count, blockedIds);
		await ObtainRuneIds(player, selectedIds);
	}

	private static async Task<bool> TryObtainRandomRunesMultiplayer(
		Player player,
		IReadOnlyList<Type> candidateTypes,
		int count,
		IReadOnlySet<ModelId>? blockedIds,
		string operationContext)
	{
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is not (NetGameType.Host or NetGameType.Client) || player.NetId == 0UL)
		{
			return false;
		}

		PlayerChoiceSynchronizer synchronizer = await HextechRuneSelectionCoordinator.WaitForPlayerChoiceSynchronizerAsync(runManager);

		uint choiceId = synchronizer.ReserveChoiceId(player);
		int operationToken = HextechChoiceCodec.ComputeOperationToken(
			"random-rune-grant",
			choiceId,
			player.NetId,
			operationContext);
		RunState runState = (RunState)player.RunState;
		if (HextechRuneSelectionCoordinator.IsLocalPlayer(runManager, player))
		{
			try
			{
				List<ModelId> selectedIds = SelectRandomRuneIds(player, candidateTypes, count, blockedIds);
				uint sentChoiceId = HextechRuneSelectionCoordinator.SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					HextechChoiceCodec.CreateRandomRuneGrant(operationToken, selectedIds),
					$"random-rune-grant {operationContext}");

				HextechLog.Info($"[{ModInfo.Id}][Mayhem] RandomRuneGrant sync local: player={player.NetId} choiceId={sentChoiceId} context={operationContext} ids={string.Join(",", selectedIds.Select(static id => id.Entry))}");
				await ObtainRuneIds(player, selectedIds);
				return true;
			}
			catch (HextechChoiceProtocolException)
			{
				throw;
			}
			catch (Exception ex)
			{
				string message =
					$"Local random rune grant failed after reserving choice: " +
					$"player={player.NetId} choiceId={choiceId} context={operationContext}";
				throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"random-rune-grant {operationContext}", message, ex);
			}
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = await HextechRuneSelectionCoordinator.WaitForRemoteHextechChoice(
			synchronizer,
			runState,
			player,
			choiceId,
			choice => HextechChoiceCodec.IsRandomRuneGrant(choice, operationToken),
			$"random-rune-grant {operationContext}");
		if (!HextechChoiceCodec.TryDecodeRandomRuneGrant(
			remoteChoice,
			operationToken,
			out List<ModelId> syncedIds))
		{
			string message =
				$"Malformed remote random rune grant payload: player={player.NetId} " +
				$"choiceId={receivedChoiceId} context={operationContext}";
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"random-rune-grant {operationContext}", message);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RandomRuneGrant remote received: player={player.NetId} choiceId={receivedChoiceId} context={operationContext} ids={string.Join(",", syncedIds.Select(static id => id.Entry))}");
		try
		{
			await ObtainRuneIds(player, syncedIds);
			return true;
		}
		catch (Exception ex)
		{
			string message =
				$"Failed to apply authoritative random rune grant: player={player.NetId} " +
				$"choiceId={receivedChoiceId} context={operationContext} ids={string.Join(",", syncedIds.Select(static id => id.Entry))}";
			throw HextechRuneSelectionCoordinator.CreateProtocolFailure($"random-rune-grant {operationContext}", message, ex);
		}
	}

	private static List<ModelId> SelectRandomRuneIds(
		Player player,
		IReadOnlyList<Type> candidateTypes,
		int count,
		IReadOnlySet<ModelId>? blockedIds)
	{
		List<ModelId> selectedIds = [];
		HashSet<ModelId> selectedIdSet = [];
		for (int i = 0; i < count; i++)
		{
			List<Type> pool = BuildObtainableRunePool(player, candidateTypes, blockedIds, selectedIdSet);
			if (pool.Count == 0)
			{
				break;
			}

			Type runeType = HextechStableRandom.Pick(
				pool,
				(RunState)player.RunState,
				HextechStableRandom.TypeModelKey,
				"rune-grant",
				HextechStableRandom.PlayerKey(player),
				i.ToString(),
				count.ToString(),
				string.Join(",", selectedIdSet.Select(static id => id.Entry).OrderBy(static entry => entry, StringComparer.Ordinal)),
				blockedIds == null ? "" : string.Join(",", blockedIds.Select(static id => id.Entry).OrderBy(static entry => entry, StringComparer.Ordinal)));
			ModelId runeId = ModelDb.GetId(runeType);
			selectedIds.Add(runeId);
			selectedIdSet.Add(runeId);
		}

		return selectedIds;
	}

	private static async Task ObtainRuneIds(Player player, IEnumerable<ModelId> runeIds)
	{
		foreach (ModelId runeId in runeIds)
		{
			RelicModel relic = ModelDb.GetById<RelicModel>(runeId).ToMutable();
			SaveManager.Instance.MarkRelicAsSeen(relic);
			await RelicCmd.Obtain(relic, player);
		}
	}

	private static List<Type> BuildObtainableRunePool(
		Player player,
		IEnumerable<Type> candidateTypes,
		IReadOnlySet<ModelId>? blockedIds,
		IReadOnlySet<ModelId> selectedIds)
	{
		bool applyConfiguration = HextechRunePoolBuilder.ShouldApplyPlayerRuneConfiguration(player);
		IReadOnlySet<string> disabledIds = applyConfiguration
			? HextechRunePoolBuilder.GetEffectiveDisabledPlayerRuneIds((RunState)player.RunState)
			: new HashSet<string>(StringComparer.Ordinal);
		HashSet<ModelId> ownedAndSelectedIds = player.Relics
			.Where(HextechCatalog.IsHextechRelic)
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.Concat(selectedIds)
			.ToHashSet();
		HashSet<ModelId> unavailableIds = ownedAndSelectedIds.ToHashSet();
		unavailableIds.UnionWith(HextechCatalog.GetMutuallyExclusivePlayerRuneIds(ownedAndSelectedIds));
		if (blockedIds != null)
		{
			unavailableIds.UnionWith(blockedIds);
		}

		return candidateTypes
			.Where(type => applyConfiguration
				? HextechCatalog.IsPlayerRuneTypeConfigurable(type)
				: HextechCatalog.IsPlayerRuneTypeSelectable(type))
			.Where(type => !applyConfiguration || !disabledIds.Contains(ModelDb.GetId(type).Entry))
			.Where(type => !IsDestructiveRandomRewardRuneType(type))
			.Where(type => !unavailableIds.Contains(ModelDb.GetId(type)))
			.Where(type =>
			{
				RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
				return HextechCatalog.IsAvailableForPlayer(relic, player);
			})
			.ToList();
	}

	public static async Task ReplaceOwnedHextechRunesWithRandomRunes(
		Player player,
		IEnumerable<Type> candidateTypes,
		string operationContext,
		IReadOnlySet<ModelId>? blockedIds = null)
	{
		List<RelicModel> ownedRunes = player.Relics.Where(HextechCatalog.IsHextechRelic).ToList();
		if (ownedRunes.Count == 0)
		{
			return;
		}

		foreach (RelicModel relic in ownedRunes)
		{
			await RelicCmd.Remove(relic);
		}

		await ObtainRandomRunes(player, candidateTypes, ownedRunes.Count, blockedIds, operationContext);
	}

	public static async Task ConsumeAndObtainRandomRunes(RelicModel consumedRune, Player player, IEnumerable<Type> candidateTypes, int count)
	{
		ModelId consumedRuneId = consumedRune.CanonicalInstance?.Id ?? consumedRune.Id;
		await RelicCmd.Remove(consumedRune);
		await ObtainRandomRunes(
			player,
			candidateTypes,
			count,
			$"consume:{consumedRuneId.Category}:{consumedRuneId.Entry}");
	}

	/// <summary>
	/// 为奖励槽确定性挑一个可获得符文(排除已拥有/已禁用/blockedIds;盐两端一致 → 联机一致,
	/// 无需 choiceId 同步)。池空(符文全拥有或全禁用)返回 null,调用方应保留原奖励兜底。
	/// </summary>
	public static Type? PickRewardRuneType(Player player, IReadOnlySet<ModelId>? blockedIds, Func<Type, bool>? extraFilter, params string?[] saltParts)
	{
		IEnumerable<Type> candidates = HextechCatalog.GetAllConfigurableRuneTypes();
		if (extraFilter != null)
		{
			candidates = candidates.Where(extraFilter);
		}

		List<Type> pool = BuildObtainableRunePool(player, candidates, blockedIds, selectedIds: new HashSet<ModelId>());
		if (pool.Count == 0)
		{
			return null;
		}

		return HextechStableRandom.Pick(pool, (RunState)player.RunState, HextechStableRandom.TypeModelKey, saltParts);
	}

	/// <summary>局内所有玩家已拥有的海克斯符文 id 并集(奖励可能被任意玩家领取时的排重口径)。</summary>
	public static HashSet<ModelId> CollectRuneIdsOwnedByAnyPlayer(IEnumerable<Player> players)
	{
		HashSet<ModelId> owned = [];
		foreach (Player player in players)
		{
			foreach (RelicModel relic in player.Relics.Where(HextechCatalog.IsHextechRelic))
			{
				owned.Add(relic.Id);
			}
		}

		return owned;
	}
}
