using System.Reflection;
using System.Runtime.Loader;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using MonoMod.RuntimeDetour;

namespace EndlessMode;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string ModId = "EndlessMode";
	private const string ArchitectEventId = "THE_ARCHITECT";
	private const string EndlessOptionTextKey = "ENDLESS_MODE.enter";
	private const string EndlessOptionTitleKey = "ENDLESS_MODE.enter.title";
	private static readonly FieldInfo MapPointHistoryField = RequireField(typeof(RunState), "_mapPointHistory");
	private static readonly FieldInfo VisitedEventIdsField = RequireField(typeof(RunState), "_visitedEventIds");
	private static readonly MethodInfo RunStateActsSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Acts), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateRngSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Rng), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateOddsSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Odds), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateMapSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Map), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly FieldInfo RelicInventoryNodesField = RequireField(typeof(NRelicInventory), "_relicNodes");
	private static readonly FieldInfo InspectRelicScreenUnlockedRelicsField = RequireField(typeof(NInspectRelicScreen), "_allUnlockedRelics");
	private static readonly FieldInfo InspectRelicScreenRelicsField = RequireField(typeof(NInspectRelicScreen), "_relics");
	private static readonly FieldInfo InspectRelicScreenIndexField = RequireField(typeof(NInspectRelicScreen), "_index");
	private static readonly FieldInfo RelicCanonicalInstanceField = RequireField(typeof(RelicModel), "_canonicalInstance");
	private static readonly MethodInfo InspectRelicScreenUpdateRelicDisplayMethod = RequireMethod(typeof(NInspectRelicScreen), "UpdateRelicDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo InspectRelicScreenSetRelicMethod = RequireMethod(typeof(NInspectRelicScreen), "SetRelic", BindingFlags.Instance | BindingFlags.NonPublic, typeof(int));
	private static readonly FieldInfo InspectRelicScreenNameLabelField = RequireField(typeof(NInspectRelicScreen), "_nameLabel");
	private static readonly FieldInfo InspectRelicScreenRarityLabelField = RequireField(typeof(NInspectRelicScreen), "_rarityLabel");
	private static readonly FieldInfo InspectRelicScreenDescriptionField = RequireField(typeof(NInspectRelicScreen), "_description");
	private static readonly FieldInfo InspectRelicScreenFlavorField = RequireField(typeof(NInspectRelicScreen), "_flavor");
	private static readonly FieldInfo InspectRelicScreenImageField = RequireField(typeof(NInspectRelicScreen), "_relicImage");
	private static readonly FieldInfo InspectRelicScreenHoverTipRectField = RequireField(typeof(NInspectRelicScreen), "_hoverTipRect");
	private static readonly MethodInfo InspectRelicScreenSetRarityVisualsMethod = RequireMethod(typeof(NInspectRelicScreen), "SetRarityVisuals", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicRarity));

	private static Hook? _setEventStateHook;
	private static Hook? _gainMaxHpHook;
	private static Hook? _setMaxHpHook;
	private static Hook? _healHook;
	private static Hook? _currentMapPointHistoryEntryHook;
	private static Hook? _unlockStateRelicsHook;
	private static Hook? _saveManagerIsRelicSeenHook;
	private static Hook? _createCreatureHook;
	private static Hook? _addCreatureHook;
	private static Hook? _inspectRelicScreenOpenHook;
	private static Hook? _inspectRelicScreenUpdateRelicDisplayHook;
	private static Hook? _relicInventoryOnRelicClickedHook;
	private static Hook? _energyIconPrefixHook;
	private static Hook? _nRelicReloadHook;

	private static readonly HashSet<Creature> ScaledEnemyCreatures = new();
	private static readonly Dictionary<string, Texture2D> ManualTextureCache = new();

	private delegate void OrigSetEventState(EventModel self, LocString description, IEnumerable<EventOption> eventOptions);
	private delegate Task OrigGainMaxHp(Creature creature, decimal amount);
	private delegate Task<decimal> OrigSetMaxHp(Creature creature, decimal amount);
	private delegate Task OrigHeal(Creature creature, decimal amount, bool playAnim);
	private delegate MapPointHistoryEntry? OrigGetCurrentMapPointHistoryEntry(RunState self);
	private delegate IEnumerable<RelicModel> OrigGetUnlockStateRelics(UnlockState self);
	private delegate bool OrigIsRelicSeen(SaveManager self, RelicModel relic);
	private delegate Creature OrigCreateCreature(CombatState self, MonsterModel monster, CombatSide side, string? slot);
	private delegate void OrigAddCreature(CombatState self, Creature creature);
	private delegate void OrigInspectRelicScreenOpen(NInspectRelicScreen self, IReadOnlyList<RelicModel> relics, RelicModel relic);
	private delegate void OrigInspectRelicScreenUpdateRelicDisplay(NInspectRelicScreen self);
	private delegate void OrigRelicInventoryOnRelicClicked(NRelicInventory self, RelicModel model);
	private delegate string OrigEnergyIconHelperGetPrefix(AbstractModel model);
	private delegate void OrigNRelicReload(NRelic self);

	public static void Initialize()
	{
		PreloadDependencyAssemblies();
		InjectSavedPropertyCaches();
		InstallHooks();
		Log.Info("[EndlessMode] Loaded.");
	}

	private static void InjectSavedPropertyCaches()
	{
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PlagueSpear));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PlagueShield));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HorribleTrophy));
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

	private static void InstallHooks()
	{
		_setEventStateHook = new Hook(
			RequireMethod(typeof(EventModel), "SetEventState", BindingFlags.Instance | BindingFlags.NonPublic, typeof(LocString), typeof(IEnumerable<EventOption>)),
			SetEventStateDetour);
		_gainMaxHpHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			GainMaxHpDetour);
		_setMaxHpHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			SetMaxHpDetour);
		_healHook = new Hook(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.Heal), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal), typeof(bool)),
			HealDetour);
		_currentMapPointHistoryEntryHook = new Hook(
			RequirePropertyGetter(typeof(RunState), nameof(RunState.CurrentMapPointHistoryEntry), BindingFlags.Instance | BindingFlags.Public),
			GetCurrentMapPointHistoryEntryDetour);
		_unlockStateRelicsHook = new Hook(
			RequirePropertyGetter(typeof(UnlockState), nameof(UnlockState.Relics), BindingFlags.Instance | BindingFlags.Public),
			GetUnlockStateRelicsDetour);
		_saveManagerIsRelicSeenHook = new Hook(
			RequireMethod(typeof(SaveManager), nameof(SaveManager.IsRelicSeen), BindingFlags.Instance | BindingFlags.Public, typeof(RelicModel)),
			IsRelicSeenDetour);
		_createCreatureHook = new Hook(
			RequireMethod(typeof(CombatState), nameof(CombatState.CreateCreature), BindingFlags.Instance | BindingFlags.Public, typeof(MonsterModel), typeof(CombatSide), typeof(string)),
			CreateCreatureDetour);
		_addCreatureHook = new Hook(
			RequireMethod(typeof(CombatState), nameof(CombatState.AddCreature), BindingFlags.Instance | BindingFlags.Public, typeof(Creature)),
			AddCreatureDetour);
		_inspectRelicScreenOpenHook = new Hook(
			RequireMethod(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), BindingFlags.Instance | BindingFlags.Public, typeof(IReadOnlyList<RelicModel>), typeof(RelicModel)),
			InspectRelicScreenOpenDetour);
		_inspectRelicScreenUpdateRelicDisplayHook = new Hook(
			InspectRelicScreenUpdateRelicDisplayMethod,
			InspectRelicScreenUpdateRelicDisplayDetour);
		_relicInventoryOnRelicClickedHook = new Hook(
			RequireMethod(typeof(NRelicInventory), "OnRelicClicked", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicModel)),
			RelicInventoryOnRelicClickedDetour);
		_energyIconPrefixHook = new Hook(
			RequireMethod(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix), BindingFlags.Static | BindingFlags.Public, typeof(AbstractModel)),
			EnergyIconHelperGetPrefixDetour);
		_nRelicReloadHook = new Hook(
			RequireMethod(typeof(NRelic), "Reload", BindingFlags.Instance | BindingFlags.NonPublic),
			NRelicReloadDetour);
	}

	private static void SetEventStateDetour(OrigSetEventState orig, EventModel self, LocString description, IEnumerable<EventOption> eventOptions)
	{
		orig(self, description, BuildEventOptions(self, eventOptions));
	}

	private static IEnumerable<EventOption> BuildEventOptions(EventModel eventModel, IEnumerable<EventOption> eventOptions)
	{
		List<EventOption> options = eventOptions.ToList();
		if (!ShouldAddEndlessOption(eventModel, options))
		{
			return options;
		}

		Player owner = eventModel.Owner!;
		if (owner.RunState.Players.Count > 1)
		{
			EventOption disabledOption = new(
				eventModel,
				null,
				new LocString("events", EndlessOptionTitleKey),
				new LocString("events", "ENDLESS_MODE.enter.multiplayer.description"),
				EndlessOptionTextKey,
				Array.Empty<IHoverTip>());
			disabledOption.ThatWontSaveToChoiceHistory();
			options.Add(disabledOption);
			return options;
		}

		int rewardTier = GetNextRewardTier(owner);
		List<IHoverTip> hoverTips = BuildRewardHoverTips(rewardTier);
		EventOption endlessOption = new(
			eventModel,
			() => EnterEndlessModeAsync(eventModel, rewardTier),
			new LocString("events", EndlessOptionTitleKey),
			new LocString("events", GetEndlessDescriptionKey(rewardTier)),
			EndlessOptionTextKey,
			hoverTips);
		endlessOption.ThatWontSaveToChoiceHistory();
		options.Add(endlessOption);
		return options;
	}

	private static bool ShouldAddEndlessOption(EventModel eventModel, List<EventOption> options)
	{
		if (eventModel.Id.Entry != ArchitectEventId || eventModel.Owner == null)
		{
			return false;
		}

		if (options.Count == 0)
		{
			return false;
		}

		return !options.Any(option => option.TextKey == EndlessOptionTextKey);
	}

	private static async Task EnterEndlessModeAsync(EventModel eventModel, int rewardTier)
	{
		Player? owner = eventModel.Owner;
		RunManager? runManager = RunManager.Instance;
		if (owner == null || runManager?.DebugOnlyGetState() is not RunState state)
		{
			return;
		}

		await AwardLoopRewardsAsync(owner, rewardTier);
		string loopSeed = SeedHelper.GetRandomSeed(10);
		PrepareRunForLoop(state, loopSeed);
		RebuildActsForLoop(state, loopSeed);
		runManager.GenerateRooms();
		LogLoopRolls(state, loopSeed);
		await ClearMapScreenForLoopTransitionAsync();
		await runManager.EnterAct(0);
	}

	private static async Task AwardLoopRewardsAsync(Player player, int rewardTier)
	{
		await GrantOrIncrementSpearAsync(player);
		await GrantOrIncrementShieldAsync(player);

		switch (rewardTier)
		{
			case 1:
				await GrantUniqueRelicAsync<MimicInfestation>(player);
				break;
			case 2:
				await GrantUniqueRelicAsync<TimeMaze>(player);
				break;
			case 3:
				await GrantUniqueRelicAsync<Muzzle>(player);
				break;
			default:
				await GrantOrIncrementTrophyAsync(player);
				break;
		}
	}

	private static void PrepareRunForLoop(RunState state, string loopSeed)
	{
		if (VisitedEventIdsField.GetValue(state) is HashSet<ModelId> visitedEventIds)
		{
			visitedEventIds.Clear();
		}

		if (MapPointHistoryField.GetValue(state) is List<List<MapPointHistoryEntry>> mapPointHistory)
		{
			mapPointHistory.Clear();
		}

		RunRngSet loopRng = new(loopSeed);
		RunOddsSet loopOdds = new(loopRng.UnknownMapPoint);
		RunStateRngSetter.Invoke(state, new object[] { loopRng });
		RunStateOddsSetter.Invoke(state, new object[] { loopOdds });
		RunStateMapSetter.Invoke(state, new object?[] { null });

		state.ClearVisitedMapCoordsDebug();
		state.ExtraFields.StartedWithNeow = false;
	}

	private static void RebuildActsForLoop(RunState state, string loopSeed)
	{
		Rng actRng = new((uint)StringHelper.GetDeterministicHashCode(loopSeed), 0);
		List<ActModel> rebuiltActs = ActModel.GetRandomList(actRng, state.UnlockState, state.Players.Count > 1)
			.Select(static act => act.ToMutable())
			.ToList();
		RunStateActsSetter.Invoke(state, new object[] { rebuiltActs });
	}

	private static void LogLoopRolls(RunState state, string loopSeed)
	{
		string summary = string.Join(
			" | ",
			state.Acts.Select(
				static (act, index) =>
					$"A{index + 1}:{act.Id.Entry} ancient={act.Ancient.Id.Entry} boss={act.BossEncounter.Id.Entry}" +
					(act.SecondBossEncounter == null ? string.Empty : $"/{act.SecondBossEncounter.Id.Entry}")));
		Log.Info($"[EndlessMode] Prepared endless loop seed={loopSeed} {summary}");
	}

	private static async Task ClearMapScreenForLoopTransitionAsync()
	{
		NMapScreen? mapScreen = NMapScreen.Instance;
		if (mapScreen == null || !GodotObject.IsInstanceValid(mapScreen))
		{
			return;
		}

		mapScreen.QueueFree();
		if (NGame.Instance != null && GodotObject.IsInstanceValid(NGame.Instance))
		{
			await NodeUtil.AwaitProcessFrame(NGame.Instance);
			await NodeUtil.AwaitProcessFrame(NGame.Instance);
		}

		Log.Info("[EndlessMode] Freed NMapScreen before endless loop act transition.");
	}

	private static async Task GrantUniqueRelicAsync<TRelic>(Player player) where TRelic : RelicModel
	{
		if (player.Relics.OfType<TRelic>().Any())
		{
			return;
		}

		await RelicCmd.Obtain<TRelic>(player);
	}

	private static async Task GrantOrIncrementSpearAsync(Player player)
	{
		if (player.Relics.OfType<PlagueSpear>().FirstOrDefault() is { } spear)
		{
			spear.AddStack();
			return;
		}

		await RelicCmd.Obtain<PlagueSpear>(player);
	}

	private static async Task GrantOrIncrementShieldAsync(Player player)
	{
		if (player.Relics.OfType<PlagueShield>().FirstOrDefault() is { } shield)
		{
			shield.AddStack();
			return;
		}

		await RelicCmd.Obtain<PlagueShield>(player);
	}

	private static async Task GrantOrIncrementTrophyAsync(Player player)
	{
		if (player.Relics.OfType<HorribleTrophy>().FirstOrDefault() is { } trophy)
		{
			trophy.AddStack();
			await GrantEnthralledAsync(player);
			return;
		}

		await RelicCmd.Obtain<HorribleTrophy>(player);
	}

	private static async Task GrantEnthralledAsync(Player player)
	{
		await CardPileCmd.AddCurseToDeck<Enthralled>(player);
	}

	private static int GetNextRewardTier(Player player)
	{
		return Math.Min(GetCompletedLoopCount(player) + 1, 4);
	}

	private static List<IHoverTip> BuildRewardHoverTips(int rewardTier)
	{
		List<IHoverTip> hoverTips =
		[
			CreateRewardHoverTip(ModelDb.Relic<PlagueSpear>()),
			CreateRewardHoverTip(ModelDb.Relic<PlagueShield>())
		];

		switch (rewardTier)
		{
			case 1:
				hoverTips.Add(CreateRewardHoverTip(ModelDb.Relic<MimicInfestation>()));
				break;
			case 2:
				hoverTips.Add(CreateRewardHoverTip(ModelDb.Relic<TimeMaze>()));
				break;
			case 3:
				hoverTips.Add(CreateRewardHoverTip(ModelDb.Relic<Muzzle>()));
				break;
			default:
				hoverTips.Add(CreateRewardHoverTip(ModelDb.Relic<HorribleTrophy>()));
				break;
		}

		return hoverTips;
	}

	private static HoverTip CreateRewardHoverTip(RelicModel relic)
	{
		Texture2D? icon = relic.BigIcon;
		if (TryLoadManualTexture(relic, out Texture2D? manualTexture) && manualTexture != null)
		{
			icon = manualTexture;
		}

		HoverTip tip = new(relic.Title, relic.DynamicDescription.GetFormattedText(), icon)
		{
			Id = relic.Id.Entry
		};
		tip.SetCanonicalModel(relic.CanonicalInstance ?? relic);
		return tip;
	}

	private static int GetCompletedLoopCount(Player player)
	{
		return player.Relics.OfType<PlagueSpear>().FirstOrDefault()?.StackCount
			?? player.Relics.OfType<PlagueShield>().FirstOrDefault()?.StackCount
			?? 0;
	}

	private static string GetEndlessDescriptionKey(int rewardTier)
	{
		return $"ENDLESS_MODE.enter.{Math.Clamp(rewardTier, 1, 4)}.description";
	}

	private static async Task GainMaxHpDetour(OrigGainMaxHp orig, Creature creature, decimal amount)
	{
		if (ShouldPreventMaxHpIncrease(creature, creature.MaxHp + amount))
		{
			return;
		}

		await orig(creature, amount);
	}

	private static async Task<decimal> SetMaxHpDetour(OrigSetMaxHp orig, Creature creature, decimal amount)
	{
		if (ShouldPreventMaxHpIncrease(creature, amount))
		{
			return 0m;
		}

		return await orig(creature, amount);
	}

	private static async Task HealDetour(OrigHeal orig, Creature creature, decimal amount, bool playAnim)
	{
		await orig(creature, ModifyHealAmount(creature, amount), playAnim);
	}

	private static decimal ModifyHealAmount(Creature creature, decimal amount)
	{
		if (amount <= 0m || IsRestSiteHeal(creature))
		{
			return amount;
		}

		decimal modifiedAmount = amount;
		if (creature.Player?.Relics.OfType<Muzzle>().Any() == true)
		{
			modifiedAmount *= 0.5m;
		}

		int plagueShieldStackCount = GetPlagueShieldStackCount(GetCurrentRunState(creature));
		if (creature.Side == CombatSide.Enemy && plagueShieldStackCount > 0)
		{
			modifiedAmount *= 1m + 0.75m * plagueShieldStackCount;
		}

		return modifiedAmount;
	}

	private static bool ShouldPreventMaxHpIncrease(Creature creature, decimal newMaxHp)
	{
		if (creature.Player?.Relics.OfType<Muzzle>().Any() != true)
		{
			return false;
		}

		return newMaxHp > creature.MaxHp;
	}

	private static MapPointHistoryEntry? GetCurrentMapPointHistoryEntryDetour(OrigGetCurrentMapPointHistoryEntry orig, RunState self)
	{
		if (MapPointHistoryField.GetValue(self) is not List<List<MapPointHistoryEntry>> mapPointHistory)
		{
			return orig(self);
		}

		if (self.CurrentActIndex < 0 || self.CurrentActIndex >= mapPointHistory.Count)
		{
			return null;
		}

		List<MapPointHistoryEntry> currentActHistory = mapPointHistory[self.CurrentActIndex];
		if (currentActHistory.Count == 0)
		{
			return null;
		}

		return currentActHistory[^1];
	}

	private static IEnumerable<RelicModel> GetUnlockStateRelicsDetour(OrigGetUnlockStateRelics orig, UnlockState self)
	{
		return orig(self).Concat(GetCustomCanonicalRelics()).Distinct();
	}

	private static bool IsRelicSeenDetour(OrigIsRelicSeen orig, SaveManager self, RelicModel relic)
	{
		if (IsEndlessRelic(relic))
		{
			return true;
		}

		return orig(self, relic);
	}

	private static Creature CreateCreatureDetour(OrigCreateCreature orig, CombatState self, MonsterModel monster, CombatSide side, string? slot)
	{
		Creature creature = orig(self, monster, side, slot);
		ApplyPlagueShieldScaling(creature, self.RunState);
		return creature;
	}

	private static void AddCreatureDetour(OrigAddCreature orig, CombatState self, Creature creature)
	{
		orig(self, creature);
		ApplyPlagueShieldScaling(creature, self.RunState);
	}

	private static void RelicInventoryOnRelicClickedDetour(OrigRelicInventoryOnRelicClicked orig, NRelicInventory self, RelicModel model)
	{
		List<RelicModel> relics = new();
		if (RelicInventoryNodesField.GetValue(self) is IEnumerable<NRelicInventoryHolder> holders)
		{
			foreach (NRelicInventoryHolder holder in holders)
			{
				relics.Add(holder.Relic.Model);
			}
		}

		int index = relics.FindIndex(candidate => ReferenceEquals(candidate, model) || candidate.Id == model.Id);
		if (index < 0)
		{
			relics.Add(model);
			index = relics.Count - 1;
		}

		Log.Info($"[EndlessMode][Inspect] Click relic={model.Id.Entry} resolvedIndex={index} list=[{string.Join(", ", relics.Select(static r => r.Id.Entry))}]");
		NGame.Instance?.GetInspectRelicScreen().Open(relics, relics[index]);
	}

	private static string EnergyIconHelperGetPrefixDetour(OrigEnergyIconHelperGetPrefix orig, AbstractModel model)
	{
		if (model is RelicModel relic && IsEndlessRelic(relic))
		{
			return "red";
		}

		return orig(model);
	}

	private static void NRelicReloadDetour(OrigNRelicReload orig, NRelic self)
	{
		orig(self);
		RelicModel? model;
		try
		{
			model = self.Model;
		}
		catch (InvalidOperationException)
		{
			return;
		}

		if (model != null && TryLoadManualTexture(model, out Texture2D? texture) && texture != null)
		{
			self.Icon.Texture = texture;
			self.Outline.Visible = false;
		}
	}

	private static void InspectRelicScreenOpenDetour(OrigInspectRelicScreenOpen orig, NInspectRelicScreen self, IReadOnlyList<RelicModel> relics, RelicModel relic)
	{
		List<RelicModel> correctedRelics = relics.ToList();
		int correctedIndex = correctedRelics.FindIndex(candidate => ReferenceEquals(candidate, relic) || candidate.Id == relic.Id);
		if (correctedIndex < 0)
		{
			correctedRelics.Add(relic);
			correctedIndex = correctedRelics.Count - 1;
		}

		orig(self, correctedRelics, correctedRelics[correctedIndex]);
		EnsureInspectRelicsUnlocked(self, relics);
		InspectRelicScreenRelicsField.SetValue(self, correctedRelics);
		InspectRelicScreenSetRelicMethod.Invoke(self, new object[] { correctedIndex });
		Log.Info($"[EndlessMode][Inspect] Open relic={relic.Id.Entry} correctedIndex={correctedIndex} correctedList=[{string.Join(", ", correctedRelics.Select(static r => r.Id.Entry))}]");
		InspectRelicScreenUpdateRelicDisplayMethod.Invoke(self, null);
	}

	private static void InspectRelicScreenUpdateRelicDisplayDetour(OrigInspectRelicScreenUpdateRelicDisplay orig, NInspectRelicScreen self)
	{
		if (InspectRelicScreenRelicsField.GetValue(self) is IReadOnlyList<RelicModel> relics
			&& InspectRelicScreenIndexField.GetValue(self) is int index
			&& index >= 0
			&& index < relics.Count)
		{
			RelicModel relic = relics[index];
			if (IsEndlessRelic(relic))
			{
				Log.Info($"[EndlessMode][Inspect] Force render relic={relic.Id.Entry} index={index}");
				RenderEndlessInspect(self, relic);
				return;
			}
		}

		orig(self);
	}

	private static void ApplyPlagueShieldScaling(Creature creature, IRunState runState)
	{
		if (creature.Side != CombatSide.Enemy || !ScaledEnemyCreatures.Add(creature))
		{
			return;
		}

		int stackCount = GetPlagueShieldStackCount(runState);
		if (stackCount <= 0)
		{
			return;
		}

		decimal multiplier = 1m + 0.75m * stackCount;
		decimal scaledHp = Math.Ceiling(creature.MaxHp * multiplier);
		creature.SetMaxHpInternal(scaledHp);
		creature.SetCurrentHpInternal(scaledHp);
	}

	private static IRunState? GetCurrentRunState(Creature creature)
	{
		return creature.Player?.RunState ?? RunManager.Instance?.DebugOnlyGetState();
	}

	private static bool IsRestSiteHeal(Creature creature)
	{
		return GetCurrentRunState(creature)?.CurrentRoom is RestSiteRoom;
	}

	private static int GetPlagueShieldStackCount(IRunState? runState)
	{
		if (runState == null)
		{
			return 0;
		}

		return runState.Players
			.SelectMany(static player => player.Relics)
			.OfType<PlagueShield>()
			.Select(static relic => relic.StackCount)
			.DefaultIfEmpty(0)
			.Max();
	}

	private static bool IsEndlessRelic(RelicModel relic)
	{
		return relic.CanonicalInstance is EndlessRelicBase || relic is EndlessRelicBase;
	}

	private static void EnsureInspectRelicsUnlocked(NInspectRelicScreen screen, IReadOnlyList<RelicModel> relics)
	{
		if (InspectRelicScreenUnlockedRelicsField.GetValue(screen) is not HashSet<RelicModel> unlockedRelics)
		{
			return;
		}

		foreach (RelicModel canonicalRelic in GetCustomCanonicalRelics())
		{
			unlockedRelics.Add(canonicalRelic);
		}

		foreach (RelicModel relic in relics)
		{
			if (!IsEndlessRelic(relic))
			{
				continue;
			}

			unlockedRelics.Add(EnsureCanonicalInstance(relic));
		}
	}

	private static RelicModel EnsureCanonicalInstance(RelicModel relic)
	{
		if (relic.CanonicalInstance != null)
		{
			return relic.CanonicalInstance;
		}

		RelicModel canonical = ModelDb.GetById<RelicModel>(relic.Id);
		RelicCanonicalInstanceField.SetValue(relic, canonical);
		return canonical;
	}

	private static void RenderEndlessInspect(NInspectRelicScreen screen, RelicModel relic)
	{
		MegaLabel nameLabel = (MegaLabel)InspectRelicScreenNameLabelField.GetValue(screen)!;
		MegaLabel rarityLabel = (MegaLabel)InspectRelicScreenRarityLabelField.GetValue(screen)!;
		MegaRichTextLabel description = (MegaRichTextLabel)InspectRelicScreenDescriptionField.GetValue(screen)!;
		MegaRichTextLabel flavor = (MegaRichTextLabel)InspectRelicScreenFlavorField.GetValue(screen)!;
		TextureRect image = (TextureRect)InspectRelicScreenImageField.GetValue(screen)!;
		Control hoverTipRect = (Control)InspectRelicScreenHoverTipRectField.GetValue(screen)!;

		nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
		LocString rarityText = new("gameplay_ui", "RELIC_RARITY." + relic.Rarity.ToString().ToUpperInvariant());
		rarityLabel.SetTextAutoSize(rarityText.GetFormattedText());
		image.SelfModulate = Colors.White;
		description.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
		flavor.SetTextAutoSize(relic.Flavor.GetFormattedText());
		InspectRelicScreenSetRarityVisualsMethod.Invoke(screen, new object[] { relic.Rarity });
		Texture2D? texture = relic.BigIcon;
		if (TryLoadManualTexture(relic, out Texture2D? manualTexture) && manualTexture != null)
		{
			texture = manualTexture;
		}

		image.Texture = texture;
		Log.Info($"[EndlessMode][Inspect] Render title={relic.Title.GetFormattedText()} rarity={relic.Rarity} texture={texture?.ResourcePath ?? "<null>"}");

		NHoverTipSet.Clear();
		NHoverTipSet hoverTipSet = NHoverTipSet.CreateAndShow(screen, relic.HoverTipsExcludingRelic);
		hoverTipSet.SetAlignment(hoverTipRect, HoverTip.GetHoverTipAlignment(screen));
	}

	private static bool TryLoadManualTexture(RelicModel relic, out Texture2D? texture)
	{
		texture = null;
		if (relic is not EndlessRelicBase endlessRelic)
		{
			return false;
		}

		string path = endlessRelic.PackedIconPath;
		if (ManualTextureCache.TryGetValue(path, out Texture2D? cachedTexture))
		{
			texture = cachedTexture;
			return true;
		}

		try
		{
			texture = ResourceLoader.Load<Texture2D>(path, cacheMode: ResourceLoader.CacheMode.Reuse);
			if (texture == null)
			{
				string absolutePath = ProjectSettings.GlobalizePath(path);
				if (File.Exists(absolutePath))
				{
					Image image = Image.LoadFromFile(absolutePath);
					texture = ImageTexture.CreateFromImage(image);
				}
			}

			if (texture == null)
			{
				return false;
			}

			ManualTextureCache[path] = texture;
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode][Inspect] Manual texture load failed for {path}: {ex.Message}");
			return false;
		}
	}

	private static IEnumerable<RelicModel> GetCustomCanonicalRelics()
	{
		yield return ModelDb.Relic<PlagueSpear>();
		yield return ModelDb.Relic<PlagueShield>();
		yield return ModelDb.Relic<MimicInfestation>();
		yield return ModelDb.Relic<TimeMaze>();
		yield return ModelDb.Relic<Muzzle>();
		yield return ModelDb.Relic<HorribleTrophy>();
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		MethodInfo? method = type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
		}

		return method;
	}

	private static MethodInfo RequirePropertyGetter(Type type, string propertyName, BindingFlags flags)
	{
		MethodInfo? getter = type.GetProperty(propertyName, flags)?.GetMethod;
		if (getter == null)
		{
			throw new InvalidOperationException($"Could not find required getter {type.FullName}.{propertyName}.");
		}

		return getter;
	}

	private static MethodInfo RequirePropertySetter(Type type, string propertyName, BindingFlags flags)
	{
		MethodInfo? setter = type.GetProperty(propertyName, flags)?.SetMethod;
		if (setter == null)
		{
			throw new InvalidOperationException($"Could not find required setter {type.FullName}.{propertyName}.");
		}

		return setter;
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field == null)
		{
			throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
		}

		return field;
	}

}

public abstract class EndlessRelicBase : RelicModel
{
	private readonly string _iconFileName;

	protected EndlessRelicBase(string iconFileName)
	{
		_iconFileName = iconFileName;
	}

	public override RelicRarity Rarity => RelicRarity.Event;

	public override string PackedIconPath => $"res://{ModEntryConstants.ModId}/images/relics/{_iconFileName}";

	protected override string PackedIconOutlinePath => PackedIconPath;

	protected override string BigIconPath => PackedIconPath;
}

public abstract class EndlessStackingRelicBase : EndlessRelicBase
{
	protected EndlessStackingRelicBase(string iconFileName)
		: base(iconFileName)
	{
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedStackCount
	{
		get => StackCount;
		set
		{
			int target = Math.Max(1, value);
			while (StackCount < target)
			{
				IncrementStackCount();
			}

			InvokeDisplayAmountChanged();
		}
	}

	public override bool IsStackable => true;

	public override bool ShowCounter => true;

	public override int DisplayAmount => !base.IsCanonical ? StackCount : 0;

	public void AddStack()
	{
		IncrementStackCount();
		InvokeDisplayAmountChanged();
		Flash();
	}
}

internal static class ModEntryConstants
{
	public const string ModId = "EndlessMode";

	public const int TimeMazeCardLimit = 15;
}

public sealed class PlagueSpear : EndlessStackingRelicBase
{
	public PlagueSpear()
		: base("spear.png")
	{
	}

	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (dealer?.Side != CombatSide.Enemy || amount <= 0m || !props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered))
		{
			return 1m;
		}

		return 1m + 0.5m * StackCount;
	}
}

public sealed class PlagueShield : EndlessStackingRelicBase
{
	public PlagueShield()
		: base("shield.png")
	{
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (target.Side != CombatSide.Enemy || block <= 0m)
		{
			return 1m;
		}

		return 1m + 0.75m * StackCount;
	}
}

public sealed class MimicInfestation : EndlessRelicBase
{
	public MimicInfestation()
		: base("mimic.png")
	{
	}

	public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)
	{
		foreach (MapPoint point in map.GetAllMapPoints())
		{
			if (point.PointType == MapPointType.Treasure)
			{
				point.PointType = MapPointType.Elite;
			}
		}

		return map;
	}

	public override bool ShouldGenerateTreasure(Player player)
	{
		return player != Owner;
	}

	public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
	{
		if (player != Owner || room is not CombatRoom combatRoom || combatRoom.RoomType != RoomType.Elite)
		{
			return false;
		}

		return rewards.RemoveAll(static reward => reward is RelicReward) > 0;
	}
}

public sealed class TimeMaze : EndlessRelicBase
{
	private int _cardsPlayedThisTurn;

	public TimeMaze()
		: base("maze.png")
	{
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true;

	public override int DisplayAmount => !base.IsCanonical ? _cardsPlayedThisTurn : 0;

	private bool ShouldPreventCardPlay => _cardsPlayedThisTurn >= ModEntryConstants.TimeMazeCardLimit;

	public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
	{
		if (card.Owner != Owner)
		{
			return true;
		}

		return !ShouldPreventCardPlay;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner == Owner)
		{
			_cardsPlayedThisTurn++;
			InvokeDisplayAmountChanged();
		}

		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is CombatRoom)
		{
			_cardsPlayedThisTurn = 0;
			InvokeDisplayAmountChanged();
		}

		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_cardsPlayedThisTurn = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
	{
		if (Owner != null && side == Owner.Creature.Side)
		{
			_cardsPlayedThisTurn = 0;
			InvokeDisplayAmountChanged();
		}

		return Task.CompletedTask;
	}
}

public sealed class Muzzle : EndlessRelicBase
{
	public Muzzle()
		: base("muzzle.png")
	{
	}

	public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
	{
		if (Owner == null || creature != Owner.Creature)
		{
			return amount;
		}

		return amount * 0.5m;
	}
}

public sealed class HorribleTrophy : EndlessStackingRelicBase
{
	public HorribleTrophy()
		: base("trophy.png")
	{
	}

	public override async Task AfterObtained()
	{
		if (Owner != null)
		{
			await CardPileCmd.AddCurseToDeck<Enthralled>(Owner);
		}
	}
}

public sealed class Pride : CardModel
{
	public override int MaxUpgradeLevel => 0;

	public override CardPoolModel Pool => ModelDb.CardPool<CurseCardPool>();

	public override string PortraitPath => MissingPortraitPath;

	public override IEnumerable<string> AllPortraitPaths => new[] { MissingPortraitPath };

	public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
	{
		CardKeyword.Innate,
		CardKeyword.Unplayable
	};

	public Pride()
		: base(-1, CardType.Curse, CardRarity.Curse, TargetType.None, shouldShowInCardLibrary: false)
	{
	}

	public override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
	{
		if (Owner?.RunState is not RunState runState)
		{
			return;
		}

		CardModel copy = runState.CreateCard<Pride>(Owner);
		await CardPileCmd.Add(copy, PileType.Discard, CardPilePosition.Top, this);
	}
}
