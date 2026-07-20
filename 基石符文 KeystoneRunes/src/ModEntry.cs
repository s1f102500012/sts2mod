using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace KeystoneRunes;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.KeystoneRunes";

	private const string SelectionHandledPropertyName = nameof(Keystone_RelicBase.KeystoneRunes_SelectionHandled);

	private readonly record struct PendingRuneSelection(Player Player, List<RelicModel> Options, uint ChoiceId, bool IsLocal);

	private static Harmony? _harmony;

	private static bool _initialized;

	private static bool _selectionInProgress;

	private static readonly ConditionalWeakTable<Player, KeystoneSelectionRuntimeState> _selectionStates = new();

	public static void Initialize()
	{
		if (_initialized)
		{
			Log.Info($"[{ModInfo.Id}] Initialize skipped; hooks are already installed.");
			return;
		}

		_initialized = true;
		InjectSavedPropertyCaches();
		RegisterModels();
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		InstallHooks(harmony);
		TryInstallOptionalHookGroup("asset hooks", () => AssetHooks.Install(harmony));
		TryInstallOptionalHookGroup("collection hooks", () => CollectionHooks.Install(harmony));
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}

	private static void TryInstallOptionalHookGroup(string label, Action install)
	{
		try
		{
			install();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Optional hook group skipped: {label}: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void InjectSavedPropertyCaches()
	{
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_ElectrocuteRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_FirstStrikeRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_UndyingGraspRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_ConquerorRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_ConquerorTemporaryStrengthPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_SummonAeryRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_LethalTempoRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_PhaseRushRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_UnsealedSpellbookRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_HailOfBladesRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_FleetFootworkRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_ArcaneCometRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_DarkHarvestRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_GlacialAugmentRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_TemporarySlowPower));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_AftershockRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(Keystone_GuardianRune));
		EnsureSavedPropertyNetIdBitSize();
	}

	private static void EnsureSavedPropertyNetIdBitSize()
	{
		const int minimumBitSize = 16;
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

		FieldInfo? mapField = typeof(SavedPropertiesTypeCache).GetField("_netIdToPropertyNameMap", flags);
		int propertyNameCount = (mapField?.GetValue(null) as System.Collections.ICollection)?.Count ?? 0;
		int requiredBitSize = GetRequiredBitSize(propertyNameCount);
		int targetBitSize = Math.Max(minimumBitSize, requiredBitSize);
		int currentBitSize = SavedPropertiesTypeCache.NetIdBitSize;
		if (currentBitSize >= targetBitSize)
		{
			Log.Info($"[{ModInfo.Id}] SavedPropertiesTypeCache NetIdBitSize unchanged: bitSize={currentBitSize} propertyNames={propertyNameCount}");
			return;
		}

		FieldInfo? backingField = typeof(SavedPropertiesTypeCache).GetField("<NetIdBitSize>k__BackingField", flags);
		if (backingField == null)
		{
			Log.Warn($"[{ModInfo.Id}] SavedPropertiesTypeCache NetIdBitSize backing field not found; custom saved properties may desync in multiplayer.");
			return;
		}

		backingField.SetValue(null, targetBitSize);
		Log.Info($"[{ModInfo.Id}] SavedPropertiesTypeCache NetIdBitSize updated: old={currentBitSize} new={targetBitSize} propertyNames={propertyNameCount}");
	}

	private static int GetRequiredBitSize(int valueCount)
	{
		int maxValue = Math.Max(1, valueCount - 1);
		int bits = 0;
		while (maxValue > 0)
		{
			bits++;
			maxValue >>= 1;
		}

		return bits;
	}

	private static void RegisterModels()
	{
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_ElectrocuteRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_FirstStrikeRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_UndyingGraspRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_ConquerorRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_SummonAeryRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_LethalTempoRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_PhaseRushRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_UnsealedSpellbookRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_HailOfBladesRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_FleetFootworkRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_ArcaneCometRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_DarkHarvestRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_GlacialAugmentRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_AftershockRune>();
		ModHelper.AddModelToPool<SharedRelicPool, Keystone_GuardianRune>();
	}

	private static void InstallHooks(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(FinalizeStartingRelicsPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NGame), "StartRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(RunState)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(StartRunPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NGame), "LoadRun", BindingFlags.Instance | BindingFlags.Public, typeof(RunState), typeof(SerializableRoom)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(LoadRunPostfix)));
		harmony.Patch(
			RequireMethod(typeof(Player), nameof(Player.FromSerializable), BindingFlags.Static | BindingFlags.Public, typeof(SerializablePlayer)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(PlayerFromSerializablePostfix)));
		harmony.Patch(
			RequireMethod(typeof(Player), nameof(Player.ToSerializable), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(PlayerToSerializablePostfix)));
	}

	private static void FinalizeStartingRelicsPostfix(RunManager __instance, ref Task __result)
	{
		__result = FinalizeStartingRelicsAfterOriginal(__result, __instance);
	}

	private static async Task FinalizeStartingRelicsAfterOriginal(Task original, RunManager self)
	{
		await original;

		RunState? runState = self.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}

		foreach (Player player in runState.Players)
		{
			RemoveRunesFromGrabBags(player);
		}
	}

	private static void StartRunPostfix(RunState runState, ref Task __result)
	{
		__result = StartRunAfterOriginal(__result, runState);
	}

	private static async Task StartRunAfterOriginal(Task original, RunState runState)
	{
		await original;
		await EnsureKeystoneRunesSelectedForRun(runState, SelectionTrigger.NewRun);
	}

	private static void LoadRunPostfix(RunState runState, ref Task __result)
	{
		__result = LoadRunAfterOriginal(__result, runState);
	}

	private static async Task LoadRunAfterOriginal(Task original, RunState runState)
	{
		await original;
		await EnsureKeystoneRunesSelectedForRun(runState, SelectionTrigger.LoadedRun);
	}

	private static async Task EnsureKeystoneRunesSelectedForRun(RunState runState, SelectionTrigger trigger)
	{
		if (_selectionInProgress)
		{
			Log.Info($"[{ModInfo.Id}] Keystone selection skipped: another selection is already in progress.");
			return;
		}

		if (trigger == SelectionTrigger.LoadedRun && !ShouldPromptAfterLoadedRun(runState))
		{
			return;
		}

		_selectionInProgress = true;
		try
		{
			NetGameType gameType = RunManager.Instance.NetService.Type;
			if (gameType is NetGameType.Singleplayer or NetGameType.None)
			{
				foreach (Player player in runState.Players)
				{
					await EnsureKeystoneRuneSelected(player);
				}
			}
			else
			{
				await EnsureKeystoneRunesSelectedMultiplayer(runState.Players.ToList());
			}
		}
		finally
		{
			_selectionInProgress = false;
		}
	}

	private static bool ShouldPromptAfterLoadedRun(RunState runState)
	{
		if (runState.CurrentActIndex != 0 || runState.ActFloor > 1)
		{
			return false;
		}

		int historyEntryCount = runState.MapPointHistory.Sum(static history => history.Count);
		if (historyEntryCount > 1)
		{
			return false;
		}

		if (CombatManager.Instance?.IsInProgress == true)
		{
			return false;
		}

		return runState.Players.Any(NeedsKeystoneSelection);
	}

	private static async Task<bool> EnsureKeystoneRunesSelectedMultiplayer(IReadOnlyList<Player> players)
	{
		RunManager runManager = RunManager.Instance;
		RunState? runState = runManager.DebugOnlyGetState();
		if (KeystoneAiTeammateCompat.IsAiTeammateLoopbackRun(runState))
		{
			return await EnsureKeystoneRunesSelectedAiTeammateHostControlled(players);
		}

		IReadOnlyList<Player> orderedPlayers = players
			.OrderBy(static player => player.NetId)
			.ToList();

		bool changed = false;
		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			Log.Warn($"[{ModInfo.Id}] Keystone multiplayer selection skipped: PlayerChoiceSynchronizer is not available.");
			return false;
		}

		List<PendingRuneSelection> pendingSelections = new();
		foreach (Player player in orderedPlayers)
		{
			RemoveRunesFromGrabBags(player);
			if (!NeedsKeystoneSelection(player))
			{
				continue;
			}

			List<RelicModel> options = ModInfo.GetCanonicalRunes()
				.Select(static relic => relic.ToMutable())
				.ToList();
			foreach (RelicModel relic in options)
			{
				SaveManager.Instance.MarkRelicAsSeen(relic);
			}

			uint choiceId = synchronizer.ReserveChoiceId(player);
			pendingSelections.Add(new PendingRuneSelection(player, options, choiceId, IsLocalPlayer(runManager, player)));
		}

		List<KeystoneRuneSelectionScreen> localScreens = new();
		try
		{
			Task<RelicModel?>[] selectionTasks = pendingSelections
				.Select(selection => SelectRuneMultiplayer(selection, synchronizer, localScreens))
				.ToArray();

			RelicModel?[] selectedRelics = await Task.WhenAll(selectionTasks);
			for (int i = 0; i < pendingSelections.Count; i++)
			{
				PendingRuneSelection selection = pendingSelections[i];
				RelicModel? selectedRelic = selectedRelics[i];
				if (selectedRelic == null)
				{
					MarkKeystoneSelectionHandled(selection.Player);
					changed = true;
					Log.Info($"[{ModInfo.Id}] Keystone selection skipped: player={selection.Player.NetId} choiceId={selection.ChoiceId}");
					continue;
				}

				await RelicCmd.Obtain(selectedRelic, selection.Player);
				MarkKeystoneSelectionHandled(selection.Player);
				changed = true;
			}
			return changed;
		}
		finally
		{
			foreach (KeystoneRuneSelectionScreen screen in localScreens)
			{
				screen.CloseSelectionScreen();
			}
		}
	}

	private static async Task<bool> EnsureKeystoneRunesSelectedAiTeammateHostControlled(IReadOnlyList<Player> players)
	{
		Log.Info($"[{ModInfo.Id}][AITeammateCompat] Host-controlled keystone selection started.");
		KeystoneAiTeammateCompat.TryGetHostPlayerId(out ulong hostPlayerId);
		IReadOnlyList<Player> orderedPlayers = players
			.OrderBy(player => hostPlayerId != 0UL
				? (player.NetId == hostPlayerId ? 0 : 1)
				: (KeystoneAiTeammateCompat.IsAiPlayer(player) ? 1 : 0))
			.ThenBy(static player => player.NetId)
			.ToList();

		bool changed = false;
		foreach (Player player in orderedPlayers)
		{
			RemoveRunesFromGrabBags(player);
			if (!NeedsKeystoneSelection(player))
			{
				continue;
			}

			List<RelicModel> options = ModInfo.GetCanonicalRunes()
				.Select(static relic => relic.ToMutable())
				.ToList();
			foreach (RelicModel relic in options)
			{
				SaveManager.Instance.MarkRelicAsSeen(relic);
			}

			bool isAiPlayer = KeystoneAiTeammateCompat.IsAiPlayer(player);
			string? titleOverride = isAiPlayer ? FormatAiTeammateSelectionTitle(player) : null;
			RelicModel? selected = await SelectRuneWithLocalScreen(options, titleOverride);
			if (selected == null)
			{
				MarkKeystoneSelectionHandled(player);
				changed = true;
				Log.Info($"[{ModInfo.Id}][AITeammateCompat] Host-controlled selection skipped: player={player.NetId} ai={isAiPlayer}");
				continue;
			}

			await RelicCmd.Obtain(selected, player);
			MarkKeystoneSelectionHandled(player);
			changed = true;
			Log.Info($"[{ModInfo.Id}][AITeammateCompat] Host-controlled obtained: player={player.NetId} ai={isAiPlayer} relic={(selected.CanonicalInstance?.Id ?? selected.Id).Entry}");
		}

		Log.Info($"[{ModInfo.Id}][AITeammateCompat] Host-controlled keystone selection complete.");
		return changed;
	}

	private static async Task<bool> EnsureKeystoneRuneSelected(Player player)
	{
		RemoveRunesFromGrabBags(player);

		if (!NeedsKeystoneSelection(player))
		{
			return false;
		}

		List<RelicModel> options = ModInfo.GetCanonicalRunes()
			.Select(static relic => relic.ToMutable())
			.ToList();

		RelicModel? selected = await SelectRune(player, options);
		if (selected == null)
		{
			MarkKeystoneSelectionHandled(player);
			Log.Info($"[{ModInfo.Id}] Keystone selection skipped: player={player.NetId}");
			return true;
		}

		await RelicCmd.Obtain(selected, player);
		MarkKeystoneSelectionHandled(player);
		return true;
	}

	private static async Task<RelicModel?> SelectRune(Player player, IReadOnlyList<RelicModel> options)
	{
		foreach (RelicModel relic in options)
		{
			SaveManager.Instance.MarkRelicAsSeen(relic);
		}

		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			return await SelectRuneWithLocalScreen(options);
		}

		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		if (synchronizer == null)
		{
			Log.Warn($"[{ModInfo.Id}] Keystone multiplayer selection skipped for player={player.NetId}: PlayerChoiceSynchronizer is not available.");
			return null;
		}

		uint choiceId = synchronizer.ReserveChoiceId(player);
		if (IsLocalPlayer(runManager, player))
		{
			RelicModel? selectedRelic = await SelectRuneWithLocalScreen(options);
			int selectedIndex = selectedRelic == null ? -1 : options.IndexOf(selectedRelic);
			synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(selectedIndex));
			return selectedRelic;
		}

		if (KeystoneAiTeammateCompat.ShouldAutoSelectRune(player))
		{
			Log.Info($"[{ModInfo.Id}][AITeammateCompat] Keystone choice AI auto-select: player={player.NetId} choiceId={choiceId}");
			int selectedIndex = KeystoneAiTeammateCompat.PickRandomRuneIndex(player, options);
			return selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : null;
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(player, choiceId);
		int index = remoteChoice.AsIndex();
		return index >= 0 && index < options.Count ? options[index] : null;
	}

	private static async Task<RelicModel?> SelectRuneMultiplayer(
		PendingRuneSelection selection,
		PlayerChoiceSynchronizer synchronizer,
		ICollection<KeystoneRuneSelectionScreen> localScreens)
	{
		if (selection.IsLocal)
		{
			KeystoneRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(selection.Options);
			localScreens.Add(screen);
			RelicModel? selectedRelic = (await screen.RelicsSelected(closeOnSelection: false)).FirstOrDefault();
			int selectedIndex = selectedRelic == null ? -1 : selection.Options.IndexOf(selectedRelic);
			synchronizer.SyncLocalChoice(selection.Player, selection.ChoiceId, PlayerChoiceResult.FromIndex(selectedIndex));
			return selectedRelic;
		}

		if (KeystoneAiTeammateCompat.ShouldAutoSelectRune(selection.Player))
		{
			Log.Info($"[{ModInfo.Id}][AITeammateCompat] Keystone choice AI auto-select: player={selection.Player.NetId} choiceId={selection.ChoiceId}");
			int selectedIndex = KeystoneAiTeammateCompat.PickRandomRuneIndex(selection.Player, selection.Options);
			return selectedIndex >= 0 && selectedIndex < selection.Options.Count ? selection.Options[selectedIndex] : null;
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(selection.Player, selection.ChoiceId);
		int index = remoteChoice.AsIndex();
		return index >= 0 && index < selection.Options.Count ? selection.Options[index] : null;
	}

	private static async Task<RelicModel?> SelectRuneWithLocalScreen(IReadOnlyList<RelicModel> options, string? titleOverride = null)
	{
		KeystoneRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(options, titleOverride);
		return (await screen.RelicsSelected()).FirstOrDefault();
	}

	private static async Task<PlayerChoiceSynchronizer?> WaitForPlayerChoiceSynchronizerAsync(RunManager runManager)
	{
		for (int i = 0; i < 200; i++)
		{
			if (runManager.PlayerChoiceSynchronizer != null)
			{
				return runManager.PlayerChoiceSynchronizer;
			}

			await Task.Delay(50);
		}

		return runManager.PlayerChoiceSynchronizer;
	}

	private static bool IsLocalPlayer(RunManager runManager, Player player)
	{
		return player.NetId == runManager.NetService.NetId;
	}

	private static async Task<KeystoneRuneSelectionScreen> CreateRuneSelectionScreenAsync(IReadOnlyList<RelicModel> relics, string? titleOverride = null)
	{
		for (int i = 0; i < 60; i++)
		{
			if (NOverlayStack.Instance != null)
			{
				break;
			}

			await Task.Yield();
		}

		KeystoneRuneSelectionScreen selectionScreen = KeystoneRuneSelectionScreen.Create(relics, titleOverride);

		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for rune selection.");
		}

		NOverlayStack.Instance.Push(selectionScreen);
		return selectionScreen;
	}

	private static string FormatAiTeammateSelectionTitle(Player player)
	{
		string template = new LocString("relic_collection", "KEYSTONE_SELECTION_TITLE_FOR_PLAYER").GetRawText();
		if (string.IsNullOrWhiteSpace(template) || template == "KEYSTONE_SELECTION_TITLE_FOR_PLAYER")
		{
			template = "为{PlayerName}选择一枚基石符文";
		}

		return template.Replace("{PlayerName}", KeystoneAiTeammateCompat.GetDisplayName(player), StringComparison.Ordinal);
	}

	private static void RemoveRunesFromGrabBags(Player player)
	{
		foreach (RelicModel relic in ModInfo.GetCanonicalRunes())
		{
			player.RelicGrabBag.Remove(relic);
			player.RunState.SharedRelicGrabBag.Remove(relic);
		}
	}

	private static bool NeedsKeystoneSelection(Player player)
	{
		return !player.Relics.Any(ModInfo.IsKeystoneRelic) && !IsKeystoneSelectionHandled(player);
	}

	private static bool IsKeystoneSelectionHandled(Player player)
	{
		return _selectionStates.TryGetValue(player, out KeystoneSelectionRuntimeState? state) && state.Handled;
	}

	private static void MarkKeystoneSelectionHandled(Player player)
	{
		_selectionStates.GetOrCreateValue(player).Handled = true;
	}

	private static void PlayerFromSerializablePostfix(SerializablePlayer save, Player __result)
	{
		if (HasSelectionHandledMarker(save))
		{
			MarkKeystoneSelectionHandled(__result);
		}
	}

	private static void PlayerToSerializablePostfix(Player __instance, SerializablePlayer __result)
	{
		if (IsKeystoneSelectionHandled(__instance))
		{
			AddSelectionHandledMarker(__result);
		}
	}

	private static bool HasSelectionHandledMarker(SerializablePlayer save)
	{
		return (save.Deck?.Any(static card => HasSelectionHandledMarker(card.Props)) == true)
			|| (save.Relics?.Any(static relic => HasSelectionHandledMarker(relic.Props)) == true);
	}

	private static bool HasSelectionHandledMarker(SavedProperties? props)
	{
		return props?.bools?.Any(static property => property.name == SelectionHandledPropertyName && property.value) == true;
	}

	private static void AddSelectionHandledMarker(SerializablePlayer save)
	{
		if (save.Deck?.FirstOrDefault() is SerializableCard card)
		{
			card.Props = AddSelectionHandledMarker(card.Props);
			return;
		}

		if (save.Relics?.FirstOrDefault() is SerializableRelic relic)
		{
			relic.Props = AddSelectionHandledMarker(relic.Props);
		}
	}

	private static SavedProperties AddSelectionHandledMarker(SavedProperties? props)
	{
		SavedProperties updatedProps = props ?? new SavedProperties();
		updatedProps.bools ??= new List<SavedProperties.SavedProperty<bool>>();

		for (int i = 0; i < updatedProps.bools.Count; i++)
		{
			if (updatedProps.bools[i].name == SelectionHandledPropertyName)
			{
				updatedProps.bools[i] = new SavedProperties.SavedProperty<bool>(SelectionHandledPropertyName, true);
				return updatedProps;
			}
		}

		updatedProps.bools.Add(new SavedProperties.SavedProperty<bool>(SelectionHandledPropertyName, true));
		return updatedProps;
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}

	private sealed class KeystoneSelectionRuntimeState
	{
		public bool Handled { get; set; }
	}

	private enum SelectionTrigger
	{
		NewRun,
		LoadedRun
	}
}
