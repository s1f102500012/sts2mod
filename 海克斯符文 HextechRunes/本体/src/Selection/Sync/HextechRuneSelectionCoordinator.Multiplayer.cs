using MegaCrit.Sts2.Core.Saves;
using static HextechRunes.HextechSelectionHelpers;

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
		int choiceOrdinal,
		bool allowEnemyHexAdjustment)
	{
		RunManager runManager = RunManager.Instance;
		IReadOnlyList<MonsterHexKind> initialActiveMonsterHexes = CombineMonsterHexes(previousMonsterHexes, initialNewMonsterHexes);
		PlayerChoiceSynchronizer synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);

		HashSet<ModelId> enemyRerollExcludedIdsForAllPlayers = new();
		List<PendingRuneSelection> pendingSelections = [];
		List<(Player Player, RelicModel SelectedRelic, bool Applied)> resolvedSelections = [];
		foreach (Player player in runState.Players)
		{
			bool hasJournalEntry = modifier.TryGetRuneSelectionJournalEntry(
				actIndex,
				choiceOrdinal,
				player.NetId,
				out HextechRuneSelectionJournalEntry journalEntry);
			if (!hasJournalEntry)
			{
				hasJournalEntry = modifier.TryRecoverRuneSelectionJournalEntryFromTelemetry(
					actIndex,
					choiceOrdinal,
					player,
					out journalEntry);
				if (hasJournalEntry)
				{
					HextechLog.Info(
						$"[{ModInfo.Id}][Mayhem] RuneChoice journal rebuilt from saved telemetry: " +
						$"act={actIndex} ordinal={choiceOrdinal} player={player.NetId}");
				}
			}

			if (hasJournalEntry)
			{
				RelicModel recoveredRelic = ModelDb.GetById<RelicModel>(journalEntry.SelectedId).ToMutable();
				// Applied 是不可重放的提交边界；符文之后可能自我消耗、替换或被其他机制移除，
				// 因此当前背包缺席不能反证当时未成功发放。
				resolvedSelections.Add((player, recoveredRelic, journalEntry.Applied));
				HextechLog.Info(
					$"[{ModInfo.Id}][Mayhem] RuneChoice journal recovered: act={actIndex} " +
					$"ordinal={choiceOrdinal} player={player.NetId} " +
					$"relic={journalEntry.SelectedId.Category}:{journalEntry.SelectedId.Entry} " +
					$"applied={journalEntry.Applied}");
				continue;
			}

			HashSet<ModelId> excludedIds = CreateBaseExcludedIds(modifier, player, initialActiveMonsterHexes);
			List<RelicModel> options = BuildStableSelectableRunesForRarity(
				player,
				rarity,
				runState,
				actIndex,
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

		EnemyHexAdjustmentSyncContext? enemyHexSync =
			allowEnemyHexAdjustment && pendingSelections.Count > 0 && initialNewMonsterHexes.Count > 0
				? CreateEnemyHexAdjustmentSyncContext(
					runManager,
					runState,
					synchronizer,
					actIndex,
					initialNewMonsterHexes)
				: null;

		RuneSelectionResult[] selectedRelics = [];
		List<HextechRuneSelectionScreen> blockingScreens = [];
		using CancellationTokenSource batchCancellation = new();
		void TrackBlockingScreen(HextechRuneSelectionScreen screen)
		{
			lock (blockingScreens)
			{
				blockingScreens.Add(screen);
			}
		}

		async Task<RuneSelectionResult> RunSelection(PendingRuneSelection selection)
		{
			try
			{
				return await SelectRuneMultiplayer(
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
						selection,
						batchCancellation.Token),
					screen => CompleteLocalEnemyHexAdjustmentSync(runManager, enemyHexSync, screen),
					TrackBlockingScreen,
					() => enemyHexSync?.RemoteReceiveTask,
					batchCancellation.Token);
			}
			catch
			{
				batchCancellation.Cancel();
				throw;
			}
		}

		try
		{
			Task<RuneSelectionResult>[] selectionTasks = pendingSelections
				.Select(RunSelection)
				.ToArray();
			selectedRelics = await Task.WhenAll(selectionTasks);
			for (int i = 0; i < pendingSelections.Count; i++)
			{
				PendingRuneSelection selection = pendingSelections[i];
				RuneSelectionResult selectedResult = selectedRelics[i];
				RelicModel selectedRelic = RequireCompletedSelection(
					selectedResult.SelectedRelic,
					$"multiplayer telemetry act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId}");
				ModelId selectedId = selectedRelic.CanonicalInstance?.Id ?? selectedRelic.Id;
				modifier.RecordRuneSelectionJournalSelection(
					actIndex,
					choiceOrdinal,
					selection.Player.NetId,
					selectedId);
				resolvedSelections.Add((selection.Player, selectedRelic, Applied: false));
				HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, selection.Player, selectedResult.FinalOptions, selectedRelic, selectedResult.RerollCount, choiceOrdinal);
			}

			IReadOnlyList<MonsterHexKind> resolvedMonsterHexes = enemyHexSync != null
				? CombineMonsterHexes(previousMonsterHexes, enemyHexSync.CurrentMonsterHexes)
				: initialActiveMonsterHexes;
			modifier.SetMonsterHexesForAct(actIndex, resolvedMonsterHexes);
			await PersistRuneSelectionCheckpoint(runState, actIndex, choiceOrdinal);

			foreach ((Player player, RelicModel selectedRelic, bool applied) in resolvedSelections)
			{
				if (!IsCurrentRun(runState) || !IsMultiplayerConnected())
				{
					throw new OperationCanceledException(
						$"Rune obtain transaction became inactive: act={actIndex} "
						+ $"ordinal={choiceOrdinal} player={player.NetId}");
				}

				ModelId selectedId = selectedRelic.CanonicalInstance?.Id ?? selectedRelic.Id;
				bool currentlyOwned = PlayerHasRelicId(player, selectedId);
				if (!HextechRuneSelectionJournalState.RequiresRelicObtain(applied, currentlyOwned))
				{
					if (!applied)
					{
						modifier.MarkRuneSelectionJournalApplied(
							actIndex,
							choiceOrdinal,
							player.NetId,
							selectedId);
					}
					continue;
				}

				try
				{
					await RelicCmd.Obtain(selectedRelic, player);
					modifier.MarkRuneSelectionJournalApplied(
						actIndex,
						choiceOrdinal,
						player.NetId,
						selectedId);
				}
				catch (Exception ex)
				{
					if (player.Relics.Any(relic => ReferenceEquals(relic, selectedRelic)))
					{
						modifier.MarkRuneSelectionJournalApplied(
							actIndex,
							choiceOrdinal,
							player.NetId,
							selectedId);
					}

					string message =
						$"Rune obtain transaction failed: act={actIndex} ordinal={choiceOrdinal} " +
						$"player={player.NetId} relic={selectedId.Category}:{selectedId.Entry}";
					Log.Error($"[{ModInfo.Id}][Mayhem] {message}: {ex}");
					AbortMultiplayerChoiceTransaction(
						$"rune-choice act={actIndex} ordinal={choiceOrdinal}",
						message);
					throw;
				}
			}

			await SynchronizeActSelectionApplied(
				runState,
				synchronizer,
				actIndex,
				choiceOrdinal,
				batchCancellation.Token);

			return resolvedMonsterHexes;
		}
		catch (OperationCanceledException)
		{
			batchCancellation.Cancel();
			throw;
		}
		catch (Exception ex)
		{
			batchCancellation.Cancel();
			string message =
				$"Multiplayer rune selection transaction failed: act={actIndex} " +
				$"ordinal={choiceOrdinal}";
			Log.Error($"[{ModInfo.Id}][Mayhem] {message}: {ex}");
			AbortMultiplayerChoiceTransaction(
				$"rune-choice act={actIndex} ordinal={choiceOrdinal}",
				message);
			throw;
		}
		finally
		{
			batchCancellation.Cancel();
			await ObserveEnemyHexAdjustmentReceiveTask(enemyHexSync);
			HextechRuneSelectionScreen[] screens;
			lock (blockingScreens)
			{
				screens = blockingScreens.ToArray();
			}

			await DismissBlockingSelectionScreens(screens);
		}
	}

	private static bool PlayerHasRelicId(Player player, ModelId expectedId)
	{
		return player.Relics.Any(relic =>
		{
			ModelId actualId = relic.CanonicalInstance?.Id ?? relic.Id;
			return actualId == expectedId;
		});
	}

	private static async Task PersistRuneSelectionCheckpoint(
		RunState runState,
		int actIndex,
		int choiceOrdinal)
	{
		try
		{
			if (RunManager.Instance.NetService.Type == NetGameType.Replay)
			{
				return;
			}
			if (!IsCurrentRun(runState) || !IsMultiplayerConnected())
			{
				throw new OperationCanceledException(
					$"RuneChoice checkpoint canceled because the multiplayer transaction is inactive: "
					+ $"act={actIndex} ordinal={choiceOrdinal}");
			}

			await SaveManager.Instance.SaveRun(null!, saveProgress: false);
			HextechLog.Info(
				$"[{ModInfo.Id}][Mayhem] RuneChoice checkpoint saved: " +
				$"act={actIndex} ordinal={choiceOrdinal}");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			string message =
				$"RuneChoice checkpoint save failed before relic obtain: " +
				$"act={actIndex} ordinal={choiceOrdinal}";
			Log.Error($"[{ModInfo.Id}][Mayhem] {message} error={ex}");
			throw new InvalidOperationException(message, ex);
		}
	}

	private static async Task DismissBlockingSelectionScreens(IEnumerable<HextechRuneSelectionScreen> screens)
	{
		foreach (HextechRuneSelectionScreen screen in screens.Distinct())
		{
			try
			{
				await screen.DismissAfterSelectionComplete();
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Failed to dismiss blocking rune selection screen: {ex}");
			}
		}
	}

}
