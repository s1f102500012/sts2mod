using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private static Hook? _createForNewRunHook;

	private static Hook? _createMapHook;

	private static Hook? _generateRoomsHook;

	private delegate RunState OrigCreateForNewRun(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> players, IReadOnlyList<ActModel> acts, IReadOnlyList<ModifierModel> modifiers, GameMode gameMode, int ascensionLevel, string seed);

	private delegate MegaCrit.Sts2.Core.Map.ActMap OrigCreateMap(ActModel self, RunState runState, bool replaceTreasureWithElites);

	private delegate void OrigGenerateRooms(RunManager self);

	public static void Initialize()
	{
		DependencyLoader.PreloadSiblingAssemblies();
		AssetHooks.Install();
		SaveHooks.Install();
		Act4MultiplayerScalingHooks.Install();
		Act4MapHooks.Install();
		Act4RestSiteHooks.Install();
		Act4SteamHooks.Install();
		InstallHooks();
		Act4MusicController.Install();
		Log.Info($"[{ModInfo.Id}] Loaded.");
	}

	private static void InstallHooks()
	{
		MethodInfo createForNewRun = RequireMethod(typeof(RunState), nameof(RunState.CreateForNewRun), BindingFlags.Static | BindingFlags.Public, typeof(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player>), typeof(IReadOnlyList<ActModel>), typeof(IReadOnlyList<ModifierModel>), typeof(GameMode), typeof(int), typeof(string));
		MethodInfo createMap = RequireMethod(typeof(ActModel), nameof(ActModel.CreateMap), BindingFlags.Instance | BindingFlags.Public, typeof(RunState), typeof(bool));
		MethodInfo generateRooms = RequireMethod(typeof(RunManager), nameof(RunManager.GenerateRooms), BindingFlags.Instance | BindingFlags.Public);

		_createForNewRunHook = new Hook(createForNewRun, CreateForNewRunDetour);
		_createMapHook = new Hook(createMap, CreateMapDetour);
		_generateRoomsHook = new Hook(generateRooms, GenerateRoomsDetour);
	}

	private static RunState CreateForNewRunDetour(OrigCreateForNewRun orig, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> players, IReadOnlyList<ActModel> acts, IReadOnlyList<ModifierModel> modifiers, GameMode gameMode, int ascensionLevel, string seed)
	{
		List<ActModel> mutableActs = acts.ToList();
		ModelId endingActId = ModelDb.GetId<Sts1Act4>();
		if (!mutableActs.Any(act => act.Id == endingActId))
		{
			mutableActs.Add(ModelDb.GetById<ActModel>(endingActId).ToMutable());
		}

		RunState result = orig(players, mutableActs, modifiers, gameMode, ascensionLevel, seed);
		return result;
	}

	private static void GenerateRoomsDetour(OrigGenerateRooms orig, RunManager self)
	{
		orig(self);

		RunState? runState = self.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}

		ModelId endingActId = ModelDb.GetId<Sts1Act4>();
		int act4Index = runState.Acts.ToList().FindIndex(act => act.Id == endingActId);
		if (act4Index <= 0)
		{
			return;
		}

		ActModel act4 = runState.Acts[act4Index];
		if (act4.HasSecondBoss)
		{
			act4.SetSecondBossEncounter(null);
			Log.Info($"[{ModInfo.Id}] Cleared unexpected second boss from {act4.Id.Entry}.");
		}

		if (runState.AscensionLevel < 10)
		{
			return;
		}

		ActModel preEndingAct = runState.Acts[act4Index - 1];
		if (preEndingAct.HasSecondBoss)
		{
			return;
		}

		EncounterModel? secondBoss = preEndingAct.AllBossEncounters.FirstOrDefault(encounter => encounter.Id != preEndingAct.BossEncounter.Id);
		if (secondBoss == null)
		{
			Log.Warn($"[{ModInfo.Id}] Could not restore second boss for act {preEndingAct.Id.Entry}; no alternate boss was found.");
			return;
		}

		preEndingAct.SetSecondBossEncounter(secondBoss);
		Log.Info($"[{ModInfo.Id}] Restored second boss for act {preEndingAct.Id.Entry} at ascension {runState.AscensionLevel}: {secondBoss.Id.Entry}.");
	}

	private static MegaCrit.Sts2.Core.Map.ActMap CreateMapDetour(OrigCreateMap orig, ActModel self, RunState runState, bool replaceTreasureWithElites)
	{
		if (self.Id == ModelDb.GetId<Sts1Act4>())
		{
			return new FixedAct4Map();
		}

		return orig(self, runState, replaceTreasureWithElites);
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameterTypes)
	{
		return type.GetMethod(name, flags, binder: null, parameterTypes, modifiers: null)
			?? throw new InvalidOperationException($"Could not find method {type.FullName}.{name}.");
	}
}
