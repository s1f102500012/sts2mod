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

			HextechRarityTier rarity = modifier.GetRarityForAct(actIndex) ?? RollRandomRarity(modifier, actIndex, runState);
			modifier.SetRarityForAct(actIndex, rarity);
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection rarity: act={actIndex} rarity={rarity}");

			if (modifier.GetMonsterHexForAct(actIndex) == null)
			{
				modifier.SetMonsterHexForAct(actIndex, ChooseMonsterHexForAct(modifier, rarity, runState));
			}
			MonsterHexKind? monsterHex = modifier.GetMonsterHexForAct(actIndex);
			Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection monsterHex: act={actIndex} hex={monsterHex}");
			RelicModel? monsterHexRelic = monsterHex.HasValue ? ModInfo.GetIconRelicForMonsterHex(monsterHex.Value).ToMutable() : null;

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
					RelicModel? selected = await SelectRune(player, options, monsterHexRelic);
					if (!IsCurrentRun(runState))
					{
						Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: selection returned for stale run");
						return;
					}
					selected ??= options[0];
					await RelicCmd.Obtain(selected, player);
					Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection obtained: player={player.NetId} relic={(selected.CanonicalInstance?.Id ?? selected.Id).Entry}");
				}
			}
			else
			{
				await SelectRunesForAllPlayersMultiplayer(runState, rarity, monsterHexRelic);
			}
			if (!IsCurrentRun(runState))
			{
				Log.Info($"[{ModInfo.Id}][Mayhem] HandleHextechActSelection abort: run changed before resolving act");
				return;
			}

			modifier.SetActResolved(actIndex, true);
			HextechEnemyUi.Refresh(modifier);
			await modifier.ApplyToCurrentEnemiesIfNeeded();
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

	private static List<RelicModel> BuildSelectableRunesForRarity(Player player, HextechRarityTier rarity, RunState runState, IReadOnlySet<ModelId>? excludedIds = null)
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

	private static async Task SelectRunesForAllPlayersMultiplayer(RunState runState, HextechRarityTier rarity, RelicModel? monsterHexRelic)
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
				RelicModel? selected = await SelectRune(player, options, monsterHexRelic);
				selected ??= options[0];
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
		}

		RelicModel?[] selectedRelics = await Task.WhenAll(pendingSelections.Select(selection => SelectRuneMultiplayer(selection, synchronizer, monsterHexRelic)));
		for (int i = 0; i < pendingSelections.Count; i++)
		{
			PendingRuneSelection selection = pendingSelections[i];
			RelicModel selectedRelic = selectedRelics[i] ?? selection.Options[0];
			await RelicCmd.Obtain(selectedRelic, selection.Player);
		}
	}

	private static async Task<RelicModel?> SelectRune(Player player, IReadOnlyList<RelicModel> options, RelicModel? monsterHexRelic)
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
				(relics, slotIndex) => RerollSingleOptionAndTrack(player, relics, slotIndex, seenOptionIds));
			return (await screen.RelicsSelected()).FirstOrDefault();
		}

		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			return await RelicSelectCmd.FromChooseARelicScreen(player, options);
		}

		uint choiceId = synchronizer.ReserveChoiceId(player);
		if (IsLocalPlayer(runManager, player))
		{
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic);
			MarkRelicsSeen(options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex) => RerollSingleOptionAndTrack(player, relics, slotIndex, seenOptionIds));
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			synchronizer.SyncLocalChoice(player, choiceId, CreateRuneChoiceResult(screen, selectedRelic));
			return selectedRelic;
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(player, choiceId);
		return ResolveRemoteRuneChoice(player, options, remoteChoice, monsterHexRelic);
	}

	private static async Task<RelicModel?> SelectRuneMultiplayer(PendingRuneSelection selection, PlayerChoiceSynchronizer synchronizer, RelicModel? monsterHexRelic)
	{
		if (selection.IsLocal)
		{
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(selection.Options, monsterHexRelic);
			MarkRelicsSeen(selection.Options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				selection.Options,
				monsterHexRelic,
				(relics, slotIndex) => RerollSingleOptionAndTrack(selection.Player, relics, slotIndex, seenOptionIds));
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			synchronizer.SyncLocalChoice(selection.Player, selection.ChoiceId, CreateRuneChoiceResult(screen, selectedRelic));
			return selectedRelic;
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(selection.Player, selection.ChoiceId);
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

	private static async Task<HextechRuneSelectionScreen> CreateRuneSelectionScreenAsync(IReadOnlyList<RelicModel> relics, RelicModel? monsterHexRelic, Func<IReadOnlyList<RelicModel>, int, IReadOnlyList<RelicModel>>? rerollFunc = null)
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

	private static RelicModel? ResolveRemoteRuneChoice(Player player, IReadOnlyList<RelicModel> options, PlayerChoiceResult remoteChoice, RelicModel? monsterHexRelic)
	{
		(int selectedIndex, List<int> rerollHistory) = DecodeRuneChoiceResult(remoteChoice);
		HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, monsterHexRelic);
		IReadOnlyList<RelicModel> currentOptions = options;
		foreach (int slotIndex in rerollHistory)
		{
			currentOptions = RerollSingleOptionAndTrack(player, currentOptions, slotIndex, seenOptionIds);
		}

		Log.Info($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: player={player.NetId} selectedIndex={selectedIndex} rerolls={string.Join(",", rerollHistory)}");
		return selectedIndex >= 0 && selectedIndex < currentOptions.Count ? currentOptions[selectedIndex] : null;
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
