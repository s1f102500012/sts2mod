using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static async Task<RuneSelectionResult> SelectRune(
		HextechMayhemModifier modifier,
		Player player,
		int actIndex,
		int choiceOrdinal,
		IReadOnlyList<RelicModel> options,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null)
	{
		string context = $"rune-choice act={actIndex} ordinal={choiceOrdinal}";
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			MarkRelicsSeen(options);
			modifier.RecordSeenPlayerRunes(player, options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic, modifier.GetSeenPlayerRuneIds(player));
			AddMonsterHexIconIds(seenOptionIds, GetEnemyHexesExcludedFromPlayerRerolls(enemyHexOptions));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				player,
				actIndex,
				choiceOrdinal,
				options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, _) => RerollSingleOptionAndTrack(
					modifier,
					player,
					relics,
					slotIndex,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll)),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll);
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes);
		}

		PlayerChoiceSynchronizer synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);

		uint choiceId = synchronizer.ReserveChoiceId(player);
		if (IsLocalPlayer(runManager, player))
		{
			MarkRelicsSeen(options);
			modifier.RecordSeenPlayerRunes(player, options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic, modifier.GetSeenPlayerRuneIds(player));
			AddMonsterHexIconIds(seenOptionIds, GetEnemyHexesExcludedFromPlayerRerolls(enemyHexOptions));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				player,
				actIndex,
				choiceOrdinal,
				options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(
					modifier,
					player,
					relics,
					slotIndex,
					rerollOrdinal,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll)),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll);
			RelicModel? selectedRelic;
			try
			{
				selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			}
			catch (OperationCanceledException)
			{
				uint canceledChoiceId = SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic: null),
					context);
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync canceled: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={canceledChoiceId}");
				throw;
			}

			uint sentChoiceId = SyncLocalHextechChoice(
				synchronizer,
				player,
				choiceId,
				CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic),
				context);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={sentChoiceId}");
			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={choiceId}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId)? received = await TryWaitForRemoteHextechChoice(
			synchronizer,
			(RunState)player.RunState,
			player,
			choiceId,
			result => HextechChoiceCodec.IsRuneSelection(result, actIndex, choiceOrdinal),
			context,
			RemoteRuneChoicePollFrames,
			() => ShouldKeepWaitingForRemoteRuneChoice((RunState)player.RunState));
		if (!received.HasValue)
		{
			throw new OperationCanceledException(
				$"Remote rune selection was interrupted: {context} player={player.NetId} choiceId={choiceId}.");
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = received.Value;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={receivedChoiceId}");
		return ResolveRemoteRuneChoice(modifier, player, actIndex, choiceOrdinal, remoteChoice);
	}

	private static async Task<RuneSelectionResult> SelectRuneMultiplayer(
		HextechMayhemModifier modifier,
		PendingRuneSelection selection,
		PlayerChoiceSynchronizer synchronizer,
		int actIndex,
		int choiceOrdinal,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null,
		Func<HextechRuneSelectionScreen, Task>? afterLocalSelection = null,
		Action<HextechRuneSelectionScreen>? screenCreated = null,
		Func<Task?>? getConcurrentTask = null,
		CancellationToken cancellationToken = default)
	{
		string context = $"rune-choice act={actIndex} ordinal={choiceOrdinal}";
		cancellationToken.ThrowIfCancellationRequested();
		if (selection.IsLocal)
		{
			MarkRelicsSeen(selection.Options);
			modifier.RecordSeenPlayerRunes(selection.Player, selection.Options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(selection.Options, monsterHexRelic, modifier.GetSeenPlayerRuneIds(selection.Player));
			AddMonsterHexIconIds(seenOptionIds, GetEnemyHexesExcludedFromPlayerRerolls(enemyHexOptions));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				selection.Player,
				actIndex,
				choiceOrdinal,
				selection.Options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				selection.Options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(
					modifier,
					selection.Player,
					relics,
					slotIndex,
					rerollOrdinal,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll)),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll,
				cancellationToken: cancellationToken);
			screenCreated?.Invoke(screen);
			RelicModel? selectedRelic;
			try
			{
				Task<IEnumerable<RelicModel>> localSelection = screen.RelicsSelected(removeOverlay: false);
				selectedRelic = (await WaitForSelectionWithConcurrentFailure(
					localSelection,
					getConcurrentTask?.Invoke(),
					context,
					cancellationToken)).FirstOrDefault();
			}
			catch (OperationCanceledException)
			{
				if (IsMultiplayerConnected())
				{
					uint canceledChoiceId = SyncLocalHextechChoice(
						synchronizer,
						selection.Player,
						selection.ChoiceId,
						CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic: null),
						context);
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync canceled: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={canceledChoiceId}");
				}

				throw;
			}

			uint sentChoiceId = SyncLocalHextechChoice(
				synchronizer,
				selection.Player,
				selection.ChoiceId,
				CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic),
				context);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={sentChoiceId}");
			_ = RequireCompletedSelection(
				selectedRelic,
				$"local {context} player={selection.Player.NetId} choiceId={sentChoiceId}");

			if (afterLocalSelection != null)
			{
				await afterLocalSelection(screen).WaitAsync(cancellationToken);
			}

			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes, screen);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={selection.ChoiceId}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId)? received = await TryWaitForRemoteHextechChoice(
			synchronizer,
			(RunState)selection.Player.RunState,
			selection.Player,
			selection.ChoiceId,
			result => HextechChoiceCodec.IsRuneSelection(result, actIndex, choiceOrdinal),
			context,
			RemoteRuneChoicePollFrames,
			() => ShouldKeepWaitingForRemoteRuneChoice((RunState)selection.Player.RunState),
			cancellationToken: cancellationToken);
		if (!received.HasValue)
		{
			throw new OperationCanceledException(
				$"Remote rune selection was interrupted: {context} player={selection.Player.NetId} choiceId={selection.ChoiceId}.");
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = received.Value;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={receivedChoiceId}");
		return ResolveRemoteRuneChoice(modifier, selection.Player, actIndex, choiceOrdinal, remoteChoice);
	}

	private static bool ShouldKeepWaitingForRemoteRuneChoice(RunState runState)
	{
		return IsCurrentRun(runState) && IsMultiplayerConnected();
	}

	private static async Task<HextechRuneSelectionScreen> CreateRuneSelectionScreenAsync(
		IReadOnlyList<RelicModel> relics,
		RelicModel? monsterHexRelic,
		Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? rerollFunc = null,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null,
		int playerRuneRerollLimit = 1,
		string? titleOverride = null,
		HextechGoldenRerollSession? goldenRerollSession = null,
		CancellationToken cancellationToken = default)
	{
		await WaitForSingletonAsync(static () => NOverlayStack.Instance, cancellationToken: cancellationToken);
		HextechRuneSelectionScreen selectionScreen = HextechRuneSelectionScreen.Create(
			relics,
			monsterHexRelic,
			rerollFunc,
			enemyHexOptions,
			playerRuneRerollLimit,
			titleOverride,
			goldenRerollSession: goldenRerollSession);
		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for rune selection.");
		}

		NOverlayStack.Instance.Push(selectionScreen);
		enemyHexOptions?.ScreenCreated?.Invoke(selectionScreen);
		return selectionScreen;
	}

	private static async Task<RuneSelectionResult> SelectRuneWithLocalScreen(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> options,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions,
		bool useMultiplayerReroll,
		bool removeOverlay,
		string? titleOverride = null)
	{
		MarkRelicsSeen(options);
		modifier.RecordSeenPlayerRunes(player, options);
		HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic, modifier.GetSeenPlayerRuneIds(player));
		AddMonsterHexIconIds(seenOptionIds, GetEnemyHexesExcludedFromPlayerRerolls(enemyHexOptions));
		HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
			options,
			monsterHexRelic,
			useMultiplayerReroll
				? (relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(modifier, player, relics, slotIndex, rerollOrdinal, seenOptionIds)
				: (relics, slotIndex, _) => RerollSingleOptionAndTrack(modifier, player, relics, slotIndex, seenOptionIds),
			enemyHexOptions,
			modifier.PlayerRuneRerollLimit,
			titleOverride);
		RelicModel? selectedRelic = (await screen.RelicsSelected(removeOverlay)).FirstOrDefault();
		return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes, removeOverlay ? null : screen);
	}

	private static PlayerChoiceResult CreateRuneChoiceResult(int actIndex, int choiceOrdinal, HextechRuneSelectionScreen screen, RelicModel? selectedRelic)
	{
		int selectedIndex = IndexOfRelicInstance(screen.CurrentRelics, selectedRelic);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] CreateRuneChoiceResult: act={actIndex} ordinal={choiceOrdinal} selectedIndex={selectedIndex} rerolls={string.Join(",", screen.RerollHistory)}");
		return HextechChoiceCodec.CreateRuneSelection(actIndex, choiceOrdinal, selectedIndex, screen.RerollHistory, screen.CurrentRelics);
	}

	private static RuneSelectionResult ResolveRemoteRuneChoice(
		HextechMayhemModifier modifier,
		Player player,
		int actIndex,
		int choiceOrdinal,
		PlayerChoiceResult remoteChoice)
	{
		if (!HextechChoiceCodec.TryDecodeRuneSelection(remoteChoice, actIndex, choiceOrdinal, out int selectedIndex, out List<int> rerollHistory, out List<ModelId> syncedOptionIds))
		{
			string message =
				$"Malformed rune selection payload: act={actIndex} ordinal={choiceOrdinal} " +
				$"player={player.NetId} result={remoteChoice}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (syncedOptionIds.Count == 0)
		{
			string message =
				$"Rune selection payload omitted authoritative final options: act={actIndex} " +
				$"ordinal={choiceOrdinal} player={player.NetId}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (!TryCreateSyncedRuneOptions(player, syncedOptionIds, actIndex, choiceOrdinal, out List<RelicModel> syncedOptions))
		{
			string message =
				$"Failed to load authoritative rune options: act={actIndex} ordinal={choiceOrdinal} " +
				$"player={player.NetId} ids={string.Join(",", syncedOptionIds.Select(static id => id.Entry))}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (selectedIndex < -1 || selectedIndex >= syncedOptions.Count)
		{
			string message =
				$"Invalid rune selection index: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} " +
				$"index={selectedIndex} optionCount={syncedOptions.Count}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		MarkRelicsSeen(syncedOptions);
		modifier.RecordSeenPlayerRunes(player, syncedOptions);
		RelicModel syncedSelectedRelic = RequireCompletedSelection(
			selectedIndex >= 0 ? syncedOptions[selectedIndex] : null,
			$"remote rune-choice act={actIndex} ordinal={choiceOrdinal} player={player.NetId}");
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: player={player.NetId} selectedIndex={selectedIndex} rerolls={string.Join(",", rerollHistory)} syncedOptions={string.Join(",", syncedOptions.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
		return new RuneSelectionResult(syncedSelectedRelic, syncedOptions, rerollHistory.Count, null);
	}

	private static async Task<T> WaitForSelectionWithConcurrentFailure<T>(
		Task<T> selectionTask,
		Task? concurrentTask,
		string context,
		CancellationToken cancellationToken)
	{
		if (concurrentTask == null)
		{
			return await selectionTask.WaitAsync(cancellationToken);
		}

		using CancellationTokenSource monitorCancellation =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task concurrentFailure = WaitForFailureAsync(concurrentTask, monitorCancellation.Token);
		Task<T> cancelableSelection = selectionTask.WaitAsync(cancellationToken);
		try
		{
			Task winner = await Task.WhenAny(cancelableSelection, concurrentFailure);
			if (winner == concurrentFailure)
			{
				await concurrentFailure;
			}

			return await cancelableSelection;
		}
		finally
		{
			monitorCancellation.Cancel();
			ObserveCompletion(concurrentFailure, $"{context} concurrent task monitor");
		}
	}

	private static async Task WaitForFailureAsync(Task task, CancellationToken cancellationToken)
	{
		await task.WaitAsync(cancellationToken);
		await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
	}

	private static IEnumerable<MonsterHexKind>? GetEnemyHexesExcludedFromPlayerRerolls(HextechEnemyHexAdjustmentOptions? enemyHexOptions)
	{
		if (enemyHexOptions == null)
		{
			return null;
		}

		return enemyHexOptions.ExcludedHexes.Count > 0
			? enemyHexOptions.ExcludedHexes
			: enemyHexOptions.InitialHexes;
	}

	private static bool TryCreateSyncedRuneOptions(
		Player player,
		IReadOnlyList<ModelId> optionIds,
		int actIndex,
		int choiceOrdinal,
		out List<RelicModel> options)
	{
		options = new(optionIds.Count);
		try
		{
			foreach (ModelId id in optionIds)
			{
				RelicModel relic = ModelDb.GetById<RelicModel>(id);
				options.Add(CreateSelectableRuneOption(player, relic));
			}

			return options.Count > 0;
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: failed to load synced option model: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} ids={string.Join(",", optionIds)} error={ex}");
			options.Clear();
			return false;
		}
	}
}
