using System.Reflection;
using System.Runtime.Loader;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
using MonoMod.RuntimeDetour;

namespace KeystoneRunes;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private static Hook? _finalizeStartingRelicsHook;

	private static Hook? _startRunHook;

	private delegate Task OrigFinalizeStartingRelics(RunManager self);

	private delegate Task OrigStartRun(NGame self, RunState runState);

	public static void Initialize()
	{
		PreloadDependencyAssemblies();
		InjectSavedPropertyCaches();
		RegisterModels();
		InstallHooks();
		AssetHooks.Install();
		CollectionHooks.Install();
		Log.Info($"[{ModInfo.Id}] Loaded for Slay the Spire 2 {ModInfo.TargetGameVersion}.");
	}

	private static void InjectSavedPropertyCaches()
	{
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ElectrocuteRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(FirstStrikeRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(UndyingGraspRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ConquerorRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(SummonAeryRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PressTheAttackRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PhaseRushRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(UnsealedSpellbookRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HailOfBladesRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(FleetFootworkRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ArcaneCometRune));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(DarkHarvestRune));
	}

	private static void RegisterModels()
	{
		ModHelper.AddModelToPool<SharedRelicPool, ElectrocuteRune>();
		ModHelper.AddModelToPool<SharedRelicPool, FirstStrikeRune>();
		ModHelper.AddModelToPool<SharedRelicPool, UndyingGraspRune>();
		ModHelper.AddModelToPool<SharedRelicPool, ConquerorRune>();
		ModHelper.AddModelToPool<SharedRelicPool, SummonAeryRune>();
		ModHelper.AddModelToPool<SharedRelicPool, PressTheAttackRune>();
		ModHelper.AddModelToPool<SharedRelicPool, PhaseRushRune>();
		ModHelper.AddModelToPool<SharedRelicPool, UnsealedSpellbookRune>();
		ModHelper.AddModelToPool<SharedRelicPool, HailOfBladesRune>();
		ModHelper.AddModelToPool<SharedRelicPool, FleetFootworkRune>();
		ModHelper.AddModelToPool<SharedRelicPool, ArcaneCometRune>();
		ModHelper.AddModelToPool<SharedRelicPool, DarkHarvestRune>();
	}

	private static void InstallHooks()
	{
		_finalizeStartingRelicsHook = new Hook(
			RequireMethod(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics), BindingFlags.Instance | BindingFlags.Public),
			FinalizeStartingRelicsDetour);
		_startRunHook = new Hook(
			RequireMethod(typeof(NGame), "StartRun", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(RunState)),
			StartRunDetour);
	}

	private static async Task FinalizeStartingRelicsDetour(OrigFinalizeStartingRelics orig, RunManager self)
	{
		await orig(self);

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

	private static async Task StartRunDetour(OrigStartRun orig, NGame self, RunState runState)
	{
		await orig(self, runState);

		foreach (Player player in runState.Players)
		{
			await EnsureKeystoneRuneSelected(player);
		}
	}

	private static async Task EnsureKeystoneRuneSelected(Player player)
	{
		RemoveRunesFromGrabBags(player);

		if (player.Relics.Any(ModInfo.IsKeystoneRelic))
		{
			return;
		}

		List<RelicModel> options = ModInfo.GetCanonicalRunes()
			.Select(static relic => relic.ToMutable())
			.ToList();

		RelicModel? selected = await SelectRune(player, options);
		selected ??= options[0];
		await RelicCmd.Obtain(selected, player);
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
			KeystoneRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(options);
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
			KeystoneRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(options);
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			int selectedIndex = selectedRelic == null ? -1 : options.IndexOf(selectedRelic);
			synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(selectedIndex));
			return selectedRelic;
		}

		PlayerChoiceResult remoteChoice = await synchronizer.WaitForRemoteChoice(player, choiceId);
		int index = remoteChoice.AsIndex();
		return index >= 0 && index < options.Count ? options[index] : null;
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

	private static async Task<KeystoneRuneSelectionScreen> CreateRuneSelectionScreenAsync(IReadOnlyList<RelicModel> relics)
	{
		for (int i = 0; i < 60; i++)
		{
			if (NOverlayStack.Instance != null)
			{
				break;
			}

			await Task.Yield();
		}

		KeystoneRuneSelectionScreen selectionScreen = KeystoneRuneSelectionScreen.Create(relics);

		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for rune selection.");
		}

		NOverlayStack.Instance.Push(selectionScreen);
		return selectionScreen;
	}

	private static void RemoveRunesFromGrabBags(Player player)
	{
		foreach (RelicModel relic in ModInfo.GetCanonicalRunes())
		{
			player.RelicGrabBag.Remove(relic);
			player.RunState.SharedRelicGrabBag.Remove(relic);
		}
	}

	private static void PreloadDependencyAssemblies()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string? modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		string selfPath = assembly.Location;
		AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (string dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null)
			?? throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
	}
}
