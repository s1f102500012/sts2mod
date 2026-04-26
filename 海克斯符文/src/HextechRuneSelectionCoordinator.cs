using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static class HextechRuneSelectionCoordinator
{
	private readonly record struct PendingRuneSelection(Player Player, List<RelicModel> Options, uint ChoiceId, bool IsLocal);
	private readonly record struct RuneSelectionResult(RelicModel? SelectedRelic, IReadOnlyList<RelicModel> FinalOptions, int RerollCount, HextechRuneSelectionScreen? BlockingScreen = null);

	private const int FirstActSilverWeight = 20;
	private const int FirstActGoldWeight = 50;
	private const int FirstActPrismaticWeight = 30;

	private static bool _handlingActSelection;

	public static void ResetActSelectionState()
	{
		_handlingActSelection = false;
	}

	public static Task HandleActStarted(HextechMayhemModifier modifier)
	{
		return HandleActSelection(modifier.ActiveRunState, modifier);
	}

	public static async Task HandleActSelection(RunState runState, HextechMayhemModifier modifier)
	{
		int actIndex = runState.CurrentActIndex;
		if (!modifier.IsActResolved(actIndex) && modifier.TryRecoverResolvedActsFromPlayerRelics(nameof(HandleActSelection)))
		{
			HextechEnemyUi.Refresh(modifier);
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection enter: room={runState.CurrentRoom?.GetType().Name ?? "null"} actIndex={actIndex} resolved={modifier.IsActResolved(actIndex)} handling={_handlingActSelection}");
		if (_handlingActSelection || !IsCurrentRun(runState) || actIndex < 0 || actIndex > 2 || modifier.IsActResolved(actIndex))
		{
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection skip");
			return;
		}

		_handlingActSelection = true;
		bool reopenMapAfterSelection = false;
		try
		{
			if (NMapScreen.Instance?.IsOpen == true && NGame.Instance != null)
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: closing map before showing selection overlay");
				NMapScreen.Instance.Close(animateOut: false);
				reopenMapAfterSelection = true;
				await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			if (!IsCurrentRun(runState))
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run is no longer current");
				return;
			}

			foreach (Player player in runState.Players)
			{
				RemoveRunesFromGrabBags(player);
			}

			(HextechRarityTier rarity, MonsterHexKind monsterHex) = await ResolveActRoll(runState, modifier, actIndex);
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection rarity: act={actIndex} rarity={rarity}");
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection monsterHex: act={actIndex} hex={monsterHex}");
			RelicModel? monsterHexRelic = ModInfo.GetIconRelicForMonsterHex(monsterHex).ToMutable();

			NetGameType gameType = RunManager.Instance.NetService.Type;
			if (gameType is NetGameType.Singleplayer or NetGameType.None)
			{
				foreach (Player player in runState.Players)
				{
					HashSet<ModelId>? excludedIds = monsterHexRelic == null ? null : new HashSet<ModelId>
					{
						monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id
					};
					List<RelicModel> options = BuildSelectableRunesForRarity(player, rarity, runState, excludedIds);
					Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection options: player={player.NetId} count={options.Count} ids={string.Join(",", options.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
					RuneSelectionResult selection = await SelectRune(player, options, monsterHexRelic);
					if (!IsCurrentRun(runState))
					{
						Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: selection returned for stale run");
						return;
					}
					RelicModel selected = selection.SelectedRelic ?? options[0];
					HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, player, selection.FinalOptions, selected, selection.RerollCount);
					await RelicCmd.Obtain(selected, player);
					Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection obtained: player={player.NetId} relic={(selected.CanonicalInstance?.Id ?? selected.Id).Entry}");
				}
			}
			else
			{
				await SelectRunesForAllPlayersMultiplayer(runState, actIndex, rarity, monsterHexRelic);
			}
			if (!IsCurrentRun(runState))
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run changed before resolving act");
				return;
			}

			modifier.SetActResolved(actIndex, true);
			HextechEnemyUi.Refresh(modifier);
			await modifier.ApplyToCurrentEnemiesIfNeeded();
			await PersistActSelection(runState, actIndex);
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection resolved: act={actIndex}");
		}
		finally
		{
			if (reopenMapAfterSelection
				&& IsCurrentRun(runState)
				&& NMapScreen.Instance != null
				&& !NMapScreen.Instance.IsOpen)
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection: reopening map after selection overlay");
				NMapScreen.Instance.Open();
			}

			_handlingActSelection = false;
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection exit: act={actIndex}");
		}
	}

	private static async Task PersistActSelection(RunState runState, int actIndex)
	{
		try
		{
			if (!IsCurrentRun(runState) || RunManager.Instance.NetService.Type == NetGameType.Replay)
			{
				return;
			}

			await SaveManager.Instance.SaveRun(null!, saveProgress: false);
			Log.Info($"[{ModInfo.Id}][Mayhem] PersistActSelection: saved current run after resolving act={actIndex}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] PersistActSelection failed: act={actIndex} error={ex}");
		}
	}

	private static HextechRarityTier RollRandomRarity(HextechMayhemModifier modifier, int actIndex, RunState runState)
	{
		if (actIndex == 0)
		{
			return RollWeightedRarity(runState, FirstActSilverWeight, FirstActGoldWeight, FirstActPrismaticWeight);
		}

		if (actIndex == 1 && modifier.GetRarityForAct(0) == HextechRarityTier.Silver)
		{
			return RollWeightedRarity(runState, 0, 1, 1);
		}

		return (HextechRarityTier)runState.Rng.Niche.NextInt(3);
	}

	private static async Task<(HextechRarityTier Rarity, MonsterHexKind MonsterHex)> ResolveActRoll(RunState runState, HextechMayhemModifier modifier, int actIndex)
	{
		HextechRarityTier localRarity = modifier.GetRarityForAct(actIndex) ?? RollRandomRarity(modifier, actIndex, runState);
		modifier.SetRarityForAct(actIndex, localRarity);

		MonsterHexKind localMonsterHex = modifier.GetMonsterHexForAct(actIndex) ?? ChooseMonsterHexForAct(modifier, localRarity, runState);
		modifier.SetMonsterHexForAct(actIndex, localMonsterHex);

		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None or NetGameType.Replay)
		{
			return (localRarity, localMonsterHex);
		}

		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		Player? authorityPlayer = GetActRollAuthorityPlayer(runManager, runState);
		if (synchronizer == null || authorityPlayer == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] ResolveActRoll: falling back to local roll act={actIndex} rarity={localRarity} monsterHex={localMonsterHex} synchronizer={synchronizer != null} authority={authorityPlayer?.NetId}");
			return (localRarity, localMonsterHex);
		}

		uint choiceId = synchronizer.ReserveChoiceId(authorityPlayer);
		if (gameType == NetGameType.Host)
		{
			synchronizer.SyncLocalChoice(authorityPlayer, choiceId, CreateActRollChoiceResult(actIndex, localRarity, localMonsterHex));
			Log.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll host sync: act={actIndex} choiceId={choiceId} authority={authorityPlayer.NetId} rarity={localRarity} monsterHex={localMonsterHex}");
			return (localRarity, localMonsterHex);
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(authorityPlayer, choiceId);
		if (!TryDecodeActRollChoiceResult(remoteChoice, actIndex, out HextechRarityTier syncedRarity, out MonsterHexKind syncedMonsterHex))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] ResolveActRoll: malformed host payload act={actIndex}; using local rarity={localRarity} monsterHex={localMonsterHex}");
			return (localRarity, localMonsterHex);
		}

		modifier.SetRarityForAct(actIndex, syncedRarity);
		modifier.SetMonsterHexForAct(actIndex, syncedMonsterHex);
		Log.Info($"[{ModInfo.Id}][Mayhem] ResolveActRoll client sync: act={actIndex} choiceId={choiceId} authority={authorityPlayer.NetId} rarity={syncedRarity} monsterHex={syncedMonsterHex} localRarity={localRarity} localMonsterHex={localMonsterHex}");
		return (syncedRarity, syncedMonsterHex);
	}

	private static PlayerChoiceResult CreateActRollChoiceResult(int actIndex, HextechRarityTier rarity, MonsterHexKind monsterHex)
	{
		return PlayerChoiceResult.FromIndexes([actIndex, (int)rarity, (int)monsterHex]);
	}

	private static bool TryDecodeActRollChoiceResult(PlayerChoiceResult result, int expectedActIndex, out HextechRarityTier rarity, out MonsterHexKind monsterHex)
	{
		rarity = default;
		monsterHex = default;
		List<int>? payload = result.AsIndexes();
		if (payload == null || payload.Count < 3 || payload[0] != expectedActIndex)
		{
			return false;
		}

		if (!Enum.IsDefined(typeof(HextechRarityTier), payload[1]) || !Enum.IsDefined(typeof(MonsterHexKind), payload[2]))
		{
			return false;
		}

		rarity = (HextechRarityTier)payload[1];
		monsterHex = (MonsterHexKind)payload[2];
		return true;
	}

	private static Player? GetActRollAuthorityPlayer(RunManager runManager, RunState runState)
	{
		if (runManager.NetService.Type == NetGameType.Host)
		{
			return runState.Players.FirstOrDefault(player => player.NetId == runManager.NetService.NetId);
		}

		return runState.Players.FirstOrDefault();
	}

	private static HextechRarityTier RollWeightedRarity(RunState runState, int silverWeight, int goldWeight, int prismaticWeight)
	{
		int totalWeight = silverWeight + goldWeight + prismaticWeight;
		int roll = runState.Rng.Niche.NextInt(totalWeight);
		if (roll < silverWeight)
		{
			return HextechRarityTier.Silver;
		}

		roll -= silverWeight;
		if (roll < goldWeight)
		{
			return HextechRarityTier.Gold;
		}

		return HextechRarityTier.Prismatic;
	}

	private static MonsterHexKind ChooseMonsterHexForAct(HextechMayhemModifier modifier, HextechRarityTier rarity, RunState runState)
	{
		HashSet<MonsterHexKind> alreadyChosen = [];
		for (int i = 0; i < 3; i++)
		{
			MonsterHexKind? kind = modifier.GetMonsterHexForAct(i);
			if (kind.HasValue)
			{
				alreadyChosen.Add(kind.Value);
			}
		}

		List<MonsterHexKind> pool = ModInfo.GetMonsterHexesForRarity(rarity)
			.Where(kind => !alreadyChosen.Contains(kind))
			.ToList();
		if (pool.Count == 0)
		{
			pool = ModInfo.GetMonsterHexesForRarity(rarity).ToList();
		}

		return pool[runState.Rng.Niche.NextInt(pool.Count)];
	}

	private static List<RelicModel> BuildSelectableRunePool(Player player, HextechRarityTier rarity, IReadOnlySet<ModelId>? excludedIds = null)
	{
		HashSet<ModelId> ownedIds = player.Relics
			.Where(ModInfo.IsHextechRelic)
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> blockedOwnedIds = ownedIds.ToHashSet();
		blockedOwnedIds.UnionWith(ModInfo.GetMutuallyExclusivePlayerRuneIds(ownedIds));

		List<RelicModel> pool = ModInfo.GetPlayerRuneTypesForRarity(rarity)
			.Select(static type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)))
			.Where(relic => ModInfo.IsAvailableForPlayer(relic, player)
				&& !blockedOwnedIds.Contains(relic.CanonicalInstance?.Id ?? relic.Id)
				&& (excludedIds == null || !excludedIds.Contains(relic.CanonicalInstance?.Id ?? relic.Id)))
			.ToList();

		return pool;
	}

	private static List<RelicModel> BuildSelectableRunesForRarity(Player player, HextechRarityTier rarity, RunState runState, IReadOnlySet<ModelId>? excludedIds = null)
	{
		List<RelicModel> pool = BuildSelectableRunePool(player, rarity, excludedIds);

		List<RelicModel> options = [];
		int picks = Math.Min(3, pool.Count);
		for (int i = 0; i < picks; i++)
		{
			int index = runState.Rng.Niche.NextInt(pool.Count);
			options.Add(pool[index].ToMutable());
			pool.RemoveAt(index);
		}

		return options;
	}

	private static async Task SelectRunesForAllPlayersMultiplayer(RunState runState, int actIndex, HextechRarityTier rarity, RelicModel? monsterHexRelic)
	{
		RunManager runManager = RunManager.Instance;
		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			foreach (Player player in runState.Players)
			{
				HashSet<ModelId>? excludedIds = monsterHexRelic == null ? null : new HashSet<ModelId>
				{
					monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id
				};
				List<RelicModel> options = BuildSelectableRunesForRarity(player, rarity, runState, excludedIds);
				RuneSelectionResult selection = await SelectRune(player, options, monsterHexRelic);
				RelicModel selected = selection.SelectedRelic ?? options[0];
				HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, player, selection.FinalOptions, selected, selection.RerollCount);
				await RelicCmd.Obtain(selected, player);
			}

			return;
		}

		List<PendingRuneSelection> pendingSelections = [];
		foreach (Player player in runState.Players)
		{
			HashSet<ModelId>? excludedIds = monsterHexRelic == null ? null : new HashSet<ModelId>
			{
				monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id
			};
			List<RelicModel> options = BuildSelectableRunesForRarity(player, rarity, runState, excludedIds);
			foreach (RelicModel relic in options)
			{
				SaveManager.Instance.MarkRelicAsSeen(relic);
			}

			uint choiceId = synchronizer.ReserveChoiceId(player);
			pendingSelections.Add(new PendingRuneSelection(player, options, choiceId, IsLocalPlayer(runManager, player)));
			Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice pending: player={player.NetId} choiceId={choiceId} local={IsLocalPlayer(runManager, player)} options={string.Join(",", options.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
		}

		RuneSelectionResult[] selectedRelics = [];
		try
		{
			selectedRelics = await Task.WhenAll(pendingSelections.Select(selection => SelectRuneMultiplayer(selection, synchronizer, monsterHexRelic)));
			for (int i = 0; i < pendingSelections.Count; i++)
			{
				PendingRuneSelection selection = pendingSelections[i];
				RuneSelectionResult selectedResult = selectedRelics[i];
				RelicModel selectedRelic = selectedResult.SelectedRelic ?? selection.Options[0];
				HextechTelemetry.RecordRuneChoice(runState, actIndex, rarity, selection.Player, selectedResult.FinalOptions, selectedRelic, selectedResult.RerollCount);
				await RelicCmd.Obtain(selectedRelic, selection.Player);
			}

			await SynchronizeActSelectionApplied(runState, synchronizer, actIndex);
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

	private static async Task SynchronizeActSelectionApplied(RunState runState, PlayerChoiceSynchronizer synchronizer, int actIndex)
	{
		RunManager runManager = RunManager.Instance;
		List<Task> pendingAcks = [];
		foreach (Player player in runState.Players)
		{
			uint choiceId = synchronizer.ReserveChoiceId(player);
			if (IsLocalPlayer(runManager, player))
			{
				synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndexes([ actIndex, 1 ]));
				Log.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied sync local: act={actIndex} player={player.NetId} choiceId={choiceId}");
				continue;
			}

			pendingAcks.Add(WaitForRemoteActSelectionApplied(synchronizer, player, choiceId, actIndex));
		}

		if (pendingAcks.Count == 0)
		{
			return;
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied waiting: act={actIndex} remoteCount={pendingAcks.Count}");
		await Task.WhenAll(pendingAcks);
		Log.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied complete: act={actIndex}");
	}

	private static async Task WaitForRemoteActSelectionApplied(PlayerChoiceSynchronizer synchronizer, Player player, uint choiceId, int actIndex)
	{
		PlayerChoiceResult remoteAck = await synchronizer.WaitForRemoteChoice(player, choiceId);
		if (!TryDecodeActSelectionApplied(remoteAck, actIndex))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] ActSelectionApplied malformed ack: act={actIndex} player={player.NetId} choiceId={choiceId}");
			return;
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] ActSelectionApplied remote: act={actIndex} player={player.NetId} choiceId={choiceId}");
	}

	private static bool TryDecodeActSelectionApplied(PlayerChoiceResult result, int expectedActIndex)
	{
		List<int>? payload = result.AsIndexes();
		return payload != null
			&& payload.Count >= 2
			&& payload[0] == expectedActIndex
			&& payload[1] == 1;
	}

	private static async Task<RuneSelectionResult> SelectRune(Player player, IReadOnlyList<RelicModel> options, RelicModel? monsterHexRelic)
	{
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic);
			MarkRelicsSeen(options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, _) => RerollSingleOptionAndTrack(player, relics, slotIndex, seenOptionIds));
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count);
		}

		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			RelicModel? selectedRelic = await RelicSelectCmd.FromChooseARelicScreen(player, options);
			return new RuneSelectionResult(selectedRelic, options.ToList(), 0);
		}

		uint choiceId = synchronizer.ReserveChoiceId(player);
		if (IsLocalPlayer(runManager, player))
		{
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic);
			MarkRelicsSeen(options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(player, relics, slotIndex, rerollOrdinal, seenOptionIds));
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			synchronizer.SyncLocalChoice(player, choiceId, CreateRuneChoiceResult(screen, selectedRelic));
			Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: player={player.NetId} choiceId={choiceId}");
			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count);
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: player={player.NetId} choiceId={choiceId}");
		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(player, choiceId);
		Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: player={player.NetId} choiceId={choiceId}");
		return ResolveRemoteRuneChoice(player, options, remoteChoice, monsterHexRelic);
	}

	private static async Task<RuneSelectionResult> SelectRuneMultiplayer(PendingRuneSelection selection, PlayerChoiceSynchronizer synchronizer, RelicModel? monsterHexRelic)
	{
		if (selection.IsLocal)
		{
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(selection.Options, monsterHexRelic);
			MarkRelicsSeen(selection.Options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				selection.Options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(selection.Player, relics, slotIndex, rerollOrdinal, seenOptionIds));
			RelicModel? selectedRelic = (await screen.RelicsSelected(removeOverlay: false)).FirstOrDefault();
			synchronizer.SyncLocalChoice(selection.Player, selection.ChoiceId, CreateRuneChoiceResult(screen, selectedRelic));
			Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: player={selection.Player.NetId} choiceId={selection.ChoiceId}");
			return new RuneSelectionResult(selectedRelic, screen.CurrentRelics.ToList(), screen.RerollHistory.Count, screen);
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: player={selection.Player.NetId} choiceId={selection.ChoiceId}");
		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(selection.Player, selection.ChoiceId);
		Log.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: player={selection.Player.NetId} choiceId={selection.ChoiceId}");
		return ResolveRemoteRuneChoice(selection.Player, selection.Options, remoteChoice, monsterHexRelic);
	}

	private static async Task<PlayerChoiceSynchronizer?> WaitForPlayerChoiceSynchronizerAsync(RunManager runManager)
	{
		for (int i = 0; i < 60; i++)
		{
			if (runManager.PlayerChoiceSynchronizer != null)
			{
				return runManager.PlayerChoiceSynchronizer;
			}

			await Task.Yield();
		}

		return runManager.PlayerChoiceSynchronizer;
	}

	private static bool IsLocalPlayer(RunManager runManager, Player player)
	{
		return player.NetId != 0UL && player.NetId == runManager.NetService.NetId;
	}

	private static async Task<HextechRuneSelectionScreen> CreateRuneSelectionScreenAsync(IReadOnlyList<RelicModel> relics, RelicModel? monsterHexRelic, Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? rerollFunc = null)
	{
		for (int i = 0; i < 60; i++)
		{
			if (NOverlayStack.Instance != null)
			{
				break;
			}

			await Task.Yield();
		}

		HextechRuneSelectionScreen selectionScreen = HextechRuneSelectionScreen.Create(relics, monsterHexRelic, rerollFunc);
		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for rune selection.");
		}

		NOverlayStack.Instance.Push(selectionScreen);
		return selectionScreen;
	}

	private static PlayerChoiceResult CreateRuneChoiceResult(HextechRuneSelectionScreen screen, RelicModel? selectedRelic)
	{
		int selectedIndex = selectedRelic == null ? -1 : IndexOfRelic(screen.CurrentRelics, selectedRelic);
		List<int> payload = [ selectedIndex, screen.RerollHistory.Count ];
		payload.AddRange(screen.RerollHistory);
		Log.Info($"[{ModInfo.Id}][Mayhem] CreateRuneChoiceResult: selectedIndex={selectedIndex} rerolls={string.Join(",", screen.RerollHistory)}");
		return PlayerChoiceResult.FromIndexes(payload);
	}

	private static int IndexOfRelic(IReadOnlyList<RelicModel> relics, RelicModel relic)
	{
		for (int i = 0; i < relics.Count; i++)
		{
			if (ReferenceEquals(relics[i], relic))
			{
				return i;
			}
		}

		return -1;
	}

	private static RuneSelectionResult ResolveRemoteRuneChoice(Player player, IReadOnlyList<RelicModel> options, PlayerChoiceResult remoteChoice, RelicModel? monsterHexRelic)
	{
		(int selectedIndex, List<int> rerollHistory) = DecodeRuneChoiceResult(remoteChoice);
		HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic);
		IReadOnlyList<RelicModel> currentOptions = options;
		for (int i = 0; i < rerollHistory.Count; i++)
		{
			int slotIndex = rerollHistory[i];
			currentOptions = RerollSingleOptionAndTrackMultiplayer(player, currentOptions, slotIndex, i, seenOptionIds);
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: player={player.NetId} selectedIndex={selectedIndex} rerolls={string.Join(",", rerollHistory)}");
		RelicModel? selectedRelic = selectedIndex >= 0 && selectedIndex < currentOptions.Count ? currentOptions[selectedIndex] : null;
		return new RuneSelectionResult(selectedRelic, currentOptions.ToList(), rerollHistory.Count);
	}

	private static (int SelectedIndex, List<int> RerollHistory) DecodeRuneChoiceResult(PlayerChoiceResult result)
	{
		List<int>? payload = result.AsIndexes();
		if (payload == null || payload.Count == 0)
		{
			return (result.AsIndex(), []);
		}

		int selectedIndex = payload[0];
		if (payload.Count == 1)
		{
			return (selectedIndex, []);
		}

		int rerollCount = Math.Max(0, payload[1]);
		if (payload.Count < rerollCount + 2)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] DecodeRuneChoiceResult: malformed payload={string.Join(",", payload)}");
			return (selectedIndex, []);
		}

		return (selectedIndex, payload.Skip(2).Take(rerollCount).ToList());
	}

	private static HashSet<ModelId> CreateSeenOptionIds(IEnumerable<RelicModel> options, RelicModel? monsterHexRelic)
	{
		HashSet<ModelId> seenOptionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		if (monsterHexRelic != null)
		{
			seenOptionIds.Add(monsterHexRelic.CanonicalInstance?.Id ?? monsterHexRelic.Id);
		}

		return seenOptionIds;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrack(Player player, IReadOnlyList<RelicModel> currentOptions, int slotIndex, HashSet<ModelId> seenOptionIds)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOption(player, (RunState)player.RunState, currentOptions, slotIndex, seenOptionIds);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOption(Player player, RunState runState, IReadOnlyList<RelicModel> currentOptions, int slotIndex, IReadOnlySet<ModelId> seenOptionIds)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> excludedIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		excludedIds.UnionWith(seenOptionIds);
		List<RelicModel> rerolled = BuildSelectableRunesForRarity(player, GetRarityForOptions(currentOptions), runState, excludedIds);
		if (rerolled.Count == 0)
		{
			return currentOptions;
		}

		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = rerolled[0];
		return updated;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrackMultiplayer(Player player, IReadOnlyList<RelicModel> currentOptions, int slotIndex, int rerollOrdinal, HashSet<ModelId> seenOptionIds)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOptionMultiplayer(player, currentOptions, slotIndex, rerollOrdinal, seenOptionIds);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			Log.Info($"[{ModInfo.Id}][Mayhem] RerollSingleOptionMultiplayer: player={player.NetId} slot={slotIndex} ordinal={rerollOrdinal} relic={rerolledId.Entry}");
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionMultiplayer(Player player, IReadOnlyList<RelicModel> currentOptions, int slotIndex, int rerollOrdinal, IReadOnlySet<ModelId> seenOptionIds)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> excludedIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		excludedIds.UnionWith(seenOptionIds);

		HextechRarityTier rarity = GetRarityForOptions(currentOptions);
		List<RelicModel> pool = BuildSelectableRunePool(player, rarity, excludedIds)
			.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
			.ToList();
		if (pool.Count == 0)
		{
			return currentOptions;
		}

		int index = GetMultiplayerRerollIndex(player, pool, rarity, slotIndex, rerollOrdinal);
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = pool[index].ToMutable();
		return updated;
	}

	private static int GetMultiplayerRerollIndex(Player player, IReadOnlyList<RelicModel> pool, HextechRarityTier rarity, int slotIndex, int rerollOrdinal)
	{
		RunState runState = (RunState)player.RunState;
		ulong hash = 14695981039346656037UL;
		AddDeterministicHash(ref hash, runState.Rng.StringSeed);
		AddDeterministicHash(ref hash, "|act:");
		AddDeterministicHash(ref hash, runState.CurrentActIndex.ToString());
		AddDeterministicHash(ref hash, "|player:");
		AddDeterministicHash(ref hash, runState.GetPlayerSlotIndex(player).ToString());
		AddDeterministicHash(ref hash, "|net:");
		AddDeterministicHash(ref hash, player.NetId.ToString());
		AddDeterministicHash(ref hash, "|rarity:");
		AddDeterministicHash(ref hash, ((int)rarity).ToString());
		AddDeterministicHash(ref hash, "|slot:");
		AddDeterministicHash(ref hash, slotIndex.ToString());
		AddDeterministicHash(ref hash, "|ordinal:");
		AddDeterministicHash(ref hash, rerollOrdinal.ToString());
		foreach (RelicModel relic in pool)
		{
			AddDeterministicHash(ref hash, "|pool:");
			AddDeterministicHash(ref hash, (relic.CanonicalInstance?.Id ?? relic.Id).Entry);
		}

		return (int)(hash % (ulong)pool.Count);
	}

	private static void AddDeterministicHash(ref ulong hash, string value)
	{
		const ulong prime = 1099511628211UL;
		foreach (char ch in value)
		{
			hash ^= (ulong)ch;
			hash *= prime;
		}
	}

	private static void MarkRelicsSeen(IEnumerable<RelicModel> relics)
	{
		foreach (RelicModel relic in relics)
		{
			SaveManager.Instance.MarkRelicAsSeen(relic);
		}
	}

	private static HextechRarityTier GetRarityForOptions(IReadOnlyList<RelicModel> relics)
	{
		if (relics.Count == 0)
		{
			return HextechRarityTier.Gold;
		}

		ModelId id = relics[0].CanonicalInstance?.Id ?? relics[0].Id;
		if (ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Silver).Any(type => ModelDb.GetId(type) == id))
		{
			return HextechRarityTier.Silver;
		}

		if (ModInfo.GetPlayerRuneTypesForRarity(HextechRarityTier.Prismatic).Any(type => ModelDb.GetId(type) == id))
		{
			return HextechRarityTier.Prismatic;
		}

		return HextechRarityTier.Gold;
	}

	public static void RemoveRunesFromGrabBags(Player player)
	{
		foreach (RelicModel relic in ModInfo.GetCanonicalRunes())
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
