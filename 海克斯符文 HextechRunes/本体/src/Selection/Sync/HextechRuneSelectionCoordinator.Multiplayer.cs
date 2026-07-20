namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static async Task<IReadOnlyList<MonsterHexKind>> SelectRunesForAllPlayersMultiplayer(
		RunState runState,
		HextechMayhemModifier modifier,
		int actIndex,
		HextechRarityTier rarity,
		IReadOnlyList<MonsterHexKind> previousMonsterHexes,
		IReadOnlyList<MonsterHexKind> initialNewMonsterHexes,
		RelicModel? monsterHexRelic,
		int choiceOrdinal)
	{
		RunManager runManager = RunManager.Instance;
		IReadOnlyList<MonsterHexKind> initialActiveMonsterHexes = CombineMonsterHexes(previousMonsterHexes, initialNewMonsterHexes);
		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			List<MonsterHexKind> fallbackNewMonsterHexes = initialNewMonsterHexes.ToList();
			IReadOnlyList<MonsterHexKind> fallbackActiveMonsterHexes = initialActiveMonsterHexes;
			foreach (Player player in runState.Players)
			{
				HashSet<ModelId> excludedIds = CreateBaseExcludedIds(modifier, player, fallbackActiveMonsterHexes);
				List<RelicModel> options = BuildStableSelectableRunesForRarity(
					player,
					rarity,
					runState,
					excludedIds,
					useEndlessTagWindow: modifier.IsEndlessLoopActive);
				if (options.Count == 0)
				{
					Log.Warn($"[{ModInfo.Id}][Mayhem] No rune options for player={player.NetId} act={actIndex} ordinal={choiceOrdinal} rarity={rarity}; skipping this selection (fallback path).", 2);
					continue;
				}

				HashSet<ModelId> enemyRerollExcludedIds = CreateEnemyHexRerollExcludedIds(options);
				HextechEnemyHexAdjustmentOptions? enemyHexOptions = fallbackActiveMonsterHexes.Count > 0
					? new HextechEnemyHexAdjustmentOptions
					{
						// 只展示【本幕新增】的敌方 hex;不能回退到 fallbackActiveMonsterHexes(前几幕累积集),
						// 否则后续选择会把历史敌方海克斯一起显示(玩家实报)。本幕无新增时面板按空集隐藏。
						InitialHexes = fallbackNewMonsterHexes,
						ExcludedHexes = fallbackActiveMonsterHexes,
						RerollLimit = modifier.MonsterHexRerollLimit,
						ControlsEnabled = fallbackNewMonsterHexes.Count > 0 && runManager.NetService.Type == NetGameType.Host && IsLocalPlayer(runManager, player),
						RerollFunc = fallbackNewMonsterHexes.Count > 0
							? (currentHexes, slotIndex, rerollOrdinal) => RerollEnemyHexForAct(
								modifier,
								rarity,
								runState,
								actIndex,
								GetMonsterHexSlot(currentHexes, slotIndex),
								rerollOrdinal,
								CreateEnemyHexRerollExcludedIds(enemyRerollExcludedIds, currentHexes, slotIndex))
							: null
					}
					: null;
				RuneSelectionResult selection = await SelectRune(
					modifier,
					player,
					actIndex,
					choiceOrdinal,
					options,
					monsterHexRelic,
					enemyHexOptions);
				fallbackNewMonsterHexes = selection.ResolvedMonsterHexes.ToList();
				fallbackActiveMonsterHexes = CombineMonsterHexes(previousMonsterHexes, fallbackNewMonsterHexes);
				monsterHexRelic = CreateMonsterHexRelic(FirstMonsterHexOrNull(fallbackNewMonsterHexes));
				RelicModel selected = RequireCompletedSelection(
					selection.SelectedRelic,
					$"multiplayer fallback act={actIndex} ordinal={choiceOrdinal} player={player.NetId}");
				HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, player, selection.FinalOptions, selected, selection.RerollCount, choiceOrdinal);
				await RelicCmd.Obtain(selected, player);
			}

			return fallbackActiveMonsterHexes;
		}

		EnemyHexAdjustmentSyncContext? enemyHexSync = initialNewMonsterHexes.Count > 0
			? CreateEnemyHexAdjustmentSyncContext(runManager, runState, synchronizer, actIndex, initialNewMonsterHexes)
			: null;
		HashSet<ModelId> enemyRerollExcludedIdsForAllPlayers = new();
		List<PendingRuneSelection> pendingSelections = [];
		foreach (Player player in runState.Players)
		{
			HashSet<ModelId> excludedIds = CreateBaseExcludedIds(modifier, player, initialActiveMonsterHexes);
			List<RelicModel> options = BuildStableSelectableRunesForRarity(
				player,
				rarity,
				runState,
				excludedIds,
				useEndlessTagWindow: modifier.IsEndlessLoopActive);
			if (options.Count == 0)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] No rune options for player={player.NetId} act={actIndex} ordinal={choiceOrdinal} rarity={rarity}; skipping this selection.", 2);
				continue;
			}

			enemyRerollExcludedIdsForAllPlayers.UnionWith(CreateEnemyHexRerollExcludedIds(options));
			MarkRelicsSeen(options);
			modifier.RecordSeenPlayerRunes(player, options);

			uint choiceId = synchronizer.ReserveChoiceId(player);
			pendingSelections.Add(new PendingRuneSelection(player, options, choiceId, IsLocalPlayer(runManager, player)));
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice pending: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={choiceId} local={IsLocalPlayer(runManager, player)} options={string.Join(",", options.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
		}

		RuneSelectionResult[] selectedRelics = [];
		try
		{
			selectedRelics = await Task.WhenAll(pendingSelections.Select(selection =>
				SelectRuneMultiplayer(
					modifier,
					selection,
					synchronizer,
					actIndex,
					choiceOrdinal,
					monsterHexRelic,
					CreateEnemyHexAdjustmentOptionsForSelection(
						modifier,
						runManager,
						runState,
						actIndex,
						rarity,
						initialActiveMonsterHexes,
						initialNewMonsterHexes,
						enemyRerollExcludedIdsForAllPlayers,
						enemyHexSync,
						selection),
					screen => CompleteLocalEnemyHexAdjustmentSync(runManager, enemyHexSync, screen))));
			for (int i = 0; i < pendingSelections.Count; i++)
			{
				PendingRuneSelection selection = pendingSelections[i];
				RuneSelectionResult selectedResult = selectedRelics[i];
				RelicModel selectedRelic = RequireCompletedSelection(
					selectedResult.SelectedRelic,
					$"multiplayer telemetry act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId}");
				HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, selection.Player, selectedResult.FinalOptions, selectedRelic, selectedResult.RerollCount, choiceOrdinal);
			}

			for (int i = 0; i < pendingSelections.Count; i++)
			{
				PendingRuneSelection selection = pendingSelections[i];
				RuneSelectionResult selectedResult = selectedRelics[i];
				RelicModel selectedRelic = RequireCompletedSelection(
					selectedResult.SelectedRelic,
					$"multiplayer obtain act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId}");
				await RelicCmd.Obtain(selectedRelic, selection.Player);
			}

			await SynchronizeActSelectionApplied(runState, synchronizer, actIndex, choiceOrdinal);

			return enemyHexSync != null
				? CombineMonsterHexes(previousMonsterHexes, enemyHexSync.CurrentMonsterHexes)
				: initialActiveMonsterHexes;
		}
		finally
		{
			await DismissBlockingSelectionScreens(selectedRelics);
		}
	}

	private static async Task DismissBlockingSelectionScreens(IEnumerable<RuneSelectionResult> selections)
	{
		foreach (HextechRuneSelectionScreen screen in selections
			.Select(static selection => selection.BlockingScreen)
			.Where(static screen => screen != null)
			.Distinct()
			.Cast<HextechRuneSelectionScreen>())
		{
			await screen.DismissAfterSelectionComplete();
		}
	}

}
