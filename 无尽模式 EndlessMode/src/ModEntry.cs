using System.Globalization;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
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

namespace EndlessMode;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string ModId = "EndlessMode";
	private const string HarmonyId = "Natsuki.EndlessMode";
	private const string ArchitectEventId = "THE_ARCHITECT";
	private const string EndlessOptionTextKey = "ENDLESS_MODE.enter";
	private const string EndlessOptionTitleKey = "ENDLESS_MODE.enter.title";
	private const string HextechMayhemModifierTypeName = "HextechRunes.HextechMayhemModifier";
	private const string HextechResetForEndlessLoopMethodName = "ResetForEndlessLoop";
	private const string SeedAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	private const int EndlessChoiceMagic = 0x454E44;
	private const int ChoiceKindLoopRewardConfig = 1;
	private static readonly TimeSpan EndlessTransitionRetryDelay = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan RemoteEndlessChoiceWarnInterval = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan RemoteEndlessChoiceMaxWait = TimeSpan.FromMinutes(10);
	private static readonly FieldInfo MapPointHistoryField = RequireField(typeof(RunState), "_mapPointHistory");
	private static readonly FieldInfo VisitedEventIdsField = RequireField(typeof(RunState), "_visitedEventIds");
	private static readonly MethodInfo RunStateActsSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Acts), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateRngSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Rng), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateOddsSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Odds), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo RunStateMapSetter = RequirePropertySetter(typeof(RunState), nameof(RunState.Map), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly MethodInfo? RunManagerClearScreensMethod = TryGetMethod(typeof(RunManager), "ClearScreens", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? RunManagerExitCurrentRoomsMethod = TryGetMethod(typeof(RunManager), "ExitCurrentRooms", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? RunManagerActEnteredField = TryGetField(typeof(RunManager), "m_ActEntered") ?? TryGetField(typeof(RunManager), "ActEntered");
	private static readonly FieldInfo MapScreenBossPointNodeField = RequireField(typeof(NMapScreen), "_bossPointNode");
	private static readonly FieldInfo MapScreenSecondBossPointNodeField = RequireField(typeof(NMapScreen), "_secondBossPointNode");
	private static readonly FieldInfo MapScreenStartingPointNodeField = RequireField(typeof(NMapScreen), "_startingPointNode");
	private static readonly FieldInfo? RelicInventoryNodesField = TryGetField(typeof(NRelicInventory), "_relicNodes");
	private static readonly FieldInfo? InspectRelicScreenUnlockedRelicsField = TryGetField(typeof(NInspectRelicScreen), "_allUnlockedRelics");
	private static readonly FieldInfo? InspectRelicScreenRelicsField = TryGetField(typeof(NInspectRelicScreen), "_relics");
	private static readonly FieldInfo? InspectRelicScreenIndexField = TryGetField(typeof(NInspectRelicScreen), "_index");
	private static readonly FieldInfo RelicCanonicalInstanceField = RequireField(typeof(RelicModel), "_canonicalInstance");
	private static readonly MethodInfo? InspectRelicScreenUpdateRelicDisplayMethod = TryGetMethod(typeof(NInspectRelicScreen), "UpdateRelicDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? InspectRelicScreenSetRelicMethod = TryGetMethod(typeof(NInspectRelicScreen), "SetRelic", BindingFlags.Instance | BindingFlags.NonPublic, typeof(int));
	private static readonly FieldInfo? InspectRelicScreenNameLabelField = TryGetField(typeof(NInspectRelicScreen), "_nameLabel");
	private static readonly FieldInfo? InspectRelicScreenRarityLabelField = TryGetField(typeof(NInspectRelicScreen), "_rarityLabel");
	private static readonly FieldInfo? InspectRelicScreenDescriptionField = TryGetField(typeof(NInspectRelicScreen), "_description");
	private static readonly FieldInfo? InspectRelicScreenFlavorField = TryGetField(typeof(NInspectRelicScreen), "_flavor");
	private static readonly FieldInfo? InspectRelicScreenImageField = TryGetField(typeof(NInspectRelicScreen), "_relicImage");
	private static readonly FieldInfo? InspectRelicScreenHoverTipRectField = TryGetField(typeof(NInspectRelicScreen), "_hoverTipRect");
	private static readonly MethodInfo? InspectRelicScreenSetRarityVisualsMethod = TryGetMethod(typeof(NInspectRelicScreen), "SetRarityVisuals", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicRarity));
	private static readonly FieldInfo? RewardsScreenRewardsContainerField = TryGetField(typeof(NRewardsScreen), "_rewardsContainer");
	private static readonly FieldInfo? RewardsScreenRewardButtonsField = TryGetField(typeof(NRewardsScreen), "_rewardButtons");
	private static readonly FieldInfo? RewardsScreenSkippedRewardButtonsField = TryGetField(typeof(NRewardsScreen), "_skippedRewardButtons");
	private static readonly FieldInfo? RewardsScreenIsCompleteField = TryGetField(typeof(NRewardsScreen), "<IsComplete>k__BackingField");

	private static Harmony? _harmony;
	private static bool _hooksInstalled;
	private static readonly HashSet<Creature> ScaledEnemyCreatures = new();
	private static readonly Dictionary<string, Texture2D> ManualTextureCache = new();
	private static readonly object StartedEndlessTransitionKeysLock = new();
	private static readonly Dictionary<string, EndlessTransitionRecord> StartedEndlessTransitionKeys = new(StringComparer.Ordinal);
	private static readonly HashSet<NRewardsScreen> DetachedRewardScreens = new(ReferenceEqualityComparer.Instance);
	private static readonly HashSet<NCardRewardSelectionScreen> DetachedCardRewardSelectionScreens = new(ReferenceEqualityComparer.Instance);

	public static void Initialize()
	{
		InjectSavedPropertyCaches();
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		InstallHooks(harmony);
		Log.Info("[EndlessMode] Loaded.");
	}

#if STS2_109_OR_NEWER
	// 0.109 起 SavedPropertiesTypeCache 被官方删除，net-id 规范化收编进
	// ModelIdSerializationCache：ExecuteEssential 的 Init() 会从 ModelDb.All 确定性
	// 排序建全量映射（本模组遗物是正规 ModelDb 模型，自动覆盖），PropertyIdBitSize
	// 也由官方计算，旧的注入与位宽 hack 均不再需要。这里仅做幂等补注入兜底。
	private static void InjectSavedPropertyCaches()
	{
		try
		{
			ModelIdSerializationCache.CacheSavedPropertiesForTypeDebug(typeof(PlagueSpear));
			ModelIdSerializationCache.CacheSavedPropertiesForTypeDebug(typeof(PlagueShield));
			ModelIdSerializationCache.CacheSavedPropertiesForTypeDebug(typeof(HorribleTrophy));
			Log.Info($"[EndlessMode] Saved property cache verified via ModelIdSerializationCache (bitSize={ModelIdSerializationCache.PropertyIdBitSize}).");
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] ModelIdSerializationCache verification failed: {ex.Message}");
		}
	}
#else
	private static void InjectSavedPropertyCaches()
	{
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PlagueSpear));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(PlagueShield));
		SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(HorribleTrophy));
		EnsureSavedPropertyNetIdBitSize();
	}

	private static void EnsureSavedPropertyNetIdBitSize()
	{
		const int minimumBitSize = 16;
		const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

		FieldInfo? mapField = typeof(SavedPropertiesTypeCache).GetField("_netIdToPropertyNameMap", flags);
		int propertyNameCount = (mapField?.GetValue(null) as System.Collections.ICollection)?.Count ?? 0;
		int targetBitSize = Math.Max(minimumBitSize, GetRequiredBitSize(propertyNameCount));
		int currentBitSize = SavedPropertiesTypeCache.NetIdBitSize;
		if (currentBitSize >= targetBitSize)
		{
			Log.Info($"[EndlessMode] SavedPropertiesTypeCache NetIdBitSize unchanged: bitSize={currentBitSize} propertyNames={propertyNameCount}");
			return;
		}

		FieldInfo? backingField = typeof(SavedPropertiesTypeCache).GetField("<NetIdBitSize>k__BackingField", flags);
		if (backingField == null)
		{
			Log.Warn("[EndlessMode] SavedPropertiesTypeCache NetIdBitSize backing field not found; custom saved properties may desync in multiplayer.");
			return;
		}

		backingField.SetValue(null, targetBitSize);
		Log.Info($"[EndlessMode] SavedPropertiesTypeCache NetIdBitSize updated: old={currentBitSize} new={targetBitSize} propertyNames={propertyNameCount}");
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
#endif

	private static void InstallHooks(Harmony harmony)
	{
		if (_hooksInstalled)
		{
			return;
		}

		harmony.Patch(
			RequireMethod(typeof(EventModel), "SetEventState", BindingFlags.Instance | BindingFlags.NonPublic, typeof(LocString), typeof(IEnumerable<EventOption>)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(SetEventStatePrefix)));
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(GainMaxHpPrefix)));
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(SetMaxHpPrefix)));
		harmony.Patch(
			RequireMethod(typeof(CreatureCmd), nameof(CreatureCmd.Heal), BindingFlags.Public | BindingFlags.Static, typeof(Creature), typeof(decimal), typeof(bool)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(HealPrefix)));
		InstallPowerCmdApplyHook(harmony);
		harmony.Patch(
			RequirePropertyGetter(typeof(RunState), nameof(RunState.CurrentMapPointHistoryEntry), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(GetCurrentMapPointHistoryEntryPrefix)));
		harmony.Patch(
			RequireMethod(typeof(NMapScreen), nameof(NMapScreen.SetMap), BindingFlags.Instance | BindingFlags.Public, typeof(ActMap), typeof(uint), typeof(bool)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(NMapScreenSetMapPrefix)));
		harmony.Patch(
			RequireMethod(typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayClosed), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(NRewardsScreenAfterOverlayClosedPrefix)));
		harmony.Patch(
			RequireMethod(typeof(NRewardsScreen), "OnProceedButtonPressed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(NButton)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(NRewardsScreenOnProceedButtonPressedPrefix)));
		InstallCardRewardSelectionScreenSafetyHooks(harmony);
		harmony.Patch(
			RequirePropertyGetter(typeof(UnlockState), nameof(UnlockState.Relics), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(GetUnlockStateRelicsPostfix)));
		harmony.Patch(
			RequireMethod(typeof(SaveManager), nameof(SaveManager.IsRelicSeen), BindingFlags.Instance | BindingFlags.Public, typeof(RelicModel)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(IsRelicSeenPrefix)));
		harmony.Patch(
			RequireMethod(typeof(CombatState), nameof(CombatState.CreateCreature), BindingFlags.Instance | BindingFlags.Public, typeof(MonsterModel), typeof(CombatSide), typeof(string)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(CreateCreaturePostfix)));
		harmony.Patch(
			RequireMethod(typeof(CombatState), nameof(CombatState.AddCreature), BindingFlags.Instance | BindingFlags.Public, typeof(Creature)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(AddCreaturePostfix)));
		InstallInspectRelicScreenHooks(harmony);
		if (TryGetMethod(typeof(NRelicInventory), "OnRelicClicked", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicModel)) is { } relicClickedMethod)
		{
			harmony.Patch(
				relicClickedMethod,
				prefix: new HarmonyMethod(typeof(ModEntry), nameof(RelicInventoryOnRelicClickedPrefix)));
		}
		else
		{
			Log.Warn("[EndlessMode][Inspect] NRelicInventory.OnRelicClicked not found; endless relic inspect shortcut disabled.");
		}
		harmony.Patch(
			RequireMethod(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix), BindingFlags.Static | BindingFlags.Public, typeof(AbstractModel)),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(EnergyIconHelperGetPrefixPrefix)));
		if (TryGetMethod(typeof(NRelic), "Reload", BindingFlags.Instance | BindingFlags.NonPublic) is { } relicReloadMethod)
		{
			harmony.Patch(
				relicReloadMethod,
				postfix: new HarmonyMethod(typeof(ModEntry), nameof(NRelicReloadPostfix)));
		}
		else
		{
			Log.Warn("[EndlessMode] NRelic.Reload not found; manual relic icon refresh hook disabled.");
		}
		harmony.Patch(
			RequirePropertyGetter(typeof(RelicModel), nameof(RelicModel.Icon), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(RelicModelTextureGetterPrefix)));
		harmony.Patch(
			RequirePropertyGetter(typeof(RelicModel), nameof(RelicModel.IconOutline), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(RelicModelTextureGetterPrefix)));
		harmony.Patch(
			RequirePropertyGetter(typeof(RelicModel), nameof(RelicModel.BigIcon), BindingFlags.Instance | BindingFlags.Public),
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(RelicModelTextureGetterPrefix)));
		harmony.Patch(
			RequireMethod(typeof(RunManager), nameof(RunManager.Launch), BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(ResetStaticStatePostfix)));
		harmony.Patch(
			RequireMethod(typeof(RunManager), nameof(RunManager.CleanUp), BindingFlags.Instance | BindingFlags.Public, typeof(bool)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(ResetStaticStatePostfix)));
		if (TryGetMethod(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready), BindingFlags.Instance | BindingFlags.Public) is { } characterSelectReadyMethod)
		{
			harmony.Patch(
				characterSelectReadyMethod,
				postfix: new HarmonyMethod(typeof(EndlessModeConfigUi), nameof(EndlessModeConfigUi.CharacterSelectReadyPostfix)));
		}
		else
		{
			Log.Warn("[EndlessMode][Config] NCharacterSelectScreen._Ready not found; config window disabled on this build.");
		}
		_hooksInstalled = true;
	}

	private static void InstallPowerCmdApplyHook(Harmony harmony)
	{
		int patchedCount = 0;
		if (TryPatchPowerCmdApplyOverload(
			harmony,
			typeof(PlayerChoiceContext),
			typeof(PowerModel),
			typeof(Creature),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel),
			typeof(bool)))
		{
			patchedCount++;
		}

		if (TryPatchPowerCmdApplyOverload(
			harmony,
			typeof(PowerModel),
			typeof(Creature),
			typeof(decimal),
			typeof(Creature),
			typeof(CardModel),
			typeof(bool)))
		{
			patchedCount++;
		}

		if (patchedCount == 0)
		{
			Log.Warn("[EndlessMode] PowerCmd.Apply(PowerModel, ...) hook not found; endless enemy power scaling compatibility disabled for this build.");
		}
	}

	private static bool TryPatchPowerCmdApplyOverload(Harmony harmony, params Type[] parameterTypes)
	{
		MethodInfo? target = TryGetMethod(typeof(PowerCmd), nameof(PowerCmd.Apply), BindingFlags.Public | BindingFlags.Static, parameterTypes);
		if (target == null)
		{
			return false;
		}

		try
		{
			harmony.Patch(
				target,
				prefix: new HarmonyMethod(typeof(ModEntry), nameof(PowerCmdApplyPowerPrefix)));
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] PowerCmd.Apply hook failed: {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	private static void InstallCardRewardSelectionScreenSafetyHooks(Harmony harmony)
	{
		if (TryGetMethod(typeof(NCardRewardSelectionScreen), "SelectCard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(NCardHolder)) is { } selectCardMethod)
		{
			harmony.Patch(
				selectCardMethod,
				prefix: new HarmonyMethod(typeof(ModEntry), nameof(NCardRewardSelectionScreenSelectCardPrefix)));
		}
		else
		{
			Log.Warn("[EndlessMode] NCardRewardSelectionScreen.SelectCard not found; detached card reward selection safety hook disabled.");
		}

		if (TryGetMethod(typeof(NCardRewardSelectionScreen), "OnAlternateRewardSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, typeof(PostAlternateCardRewardAction)) is { } alternateRewardMethod)
		{
			harmony.Patch(
				alternateRewardMethod,
				prefix: new HarmonyMethod(typeof(ModEntry), nameof(NCardRewardSelectionScreenAlternateRewardPrefix)));
		}
		else
		{
			Log.Warn("[EndlessMode] NCardRewardSelectionScreen.OnAlternateRewardSelected not found; detached alternate reward safety hook disabled.");
		}
	}

	private static void InstallInspectRelicScreenHooks(Harmony harmony)
	{
		MethodInfo? inspectOpenMethod = TryGetMethod(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), BindingFlags.Instance | BindingFlags.Public, typeof(IReadOnlyList<RelicModel>), typeof(RelicModel));
		if (!CanPatchInspectRelicScreen(inspectOpenMethod))
		{
			Log.Warn("[EndlessMode][Inspect] Inspect relic screen hooks disabled because this game build is missing one or more private UI members.");
			return;
		}

		harmony.Patch(
			inspectOpenMethod!,
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(InspectRelicScreenOpenPrefix)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(InspectRelicScreenOpenPostfix)));
		harmony.Patch(
			InspectRelicScreenUpdateRelicDisplayMethod!,
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(InspectRelicScreenUpdateRelicDisplayPrefix)));
	}

	private static bool CanPatchInspectRelicScreen(MethodInfo? inspectOpenMethod)
	{
		return inspectOpenMethod != null
			&& InspectRelicScreenUnlockedRelicsField != null
			&& InspectRelicScreenRelicsField != null
			&& InspectRelicScreenIndexField != null
			&& InspectRelicScreenUpdateRelicDisplayMethod != null
			&& InspectRelicScreenSetRelicMethod != null
			&& InspectRelicScreenNameLabelField != null
			&& InspectRelicScreenRarityLabelField != null
			&& InspectRelicScreenDescriptionField != null
			&& InspectRelicScreenFlavorField != null
			&& InspectRelicScreenImageField != null
			&& InspectRelicScreenHoverTipRectField != null
			&& InspectRelicScreenSetRarityVisualsMethod != null;
	}

	private static void SetEventStatePrefix(EventModel __instance, ref IEnumerable<EventOption> eventOptions)
	{
		eventOptions = BuildEventOptions(__instance, eventOptions);
	}

	// 本方法在所有事件的 SetEventState 上运行；贴图/本地化加载是本地行为，
	// 任何异常若外泄会单端中断事件状态推进造成联机分叉（休息室图标分叉同款），
	// 所以整体兜底：失败时原样返回传入的选项。
	private static IEnumerable<EventOption> BuildEventOptions(EventModel eventModel, IEnumerable<EventOption> eventOptions)
	{
		List<EventOption> options = eventOptions.ToList();
		try
		{
			if (!ShouldAddEndlessOption(eventModel, options))
			{
				return options;
			}

			Player owner = eventModel.Owner!;
			int rewardTier = GetNextRewardTier(owner);
			int nextLoopIndex = GetCompletedLoopCount(owner) + 1;
			List<IHoverTip> hoverTips = BuildRewardHoverTips(rewardTier);
			EndlessTransitionContext? transitionContext = BuildEndlessTransitionContextForCurrentRun();
			EventOption endlessOption = new(
				eventModel,
				() => QueueEndlessModeTransition(eventModel, transitionContext),
				new LocString("events", EndlessOptionTitleKey),
				GetEndlessDescription(rewardTier, nextLoopIndex),
				EndlessOptionTextKey,
				hoverTips);
			endlessOption.ThatWontSaveToChoiceHistory();
			options.Add(endlessOption);
			return options;
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] BuildEventOptions failed; keeping vanilla options: {ex}");
			return options;
		}
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

	private static Task QueueEndlessModeTransition(EventModel eventModel, EndlessTransitionContext? capturedTransitionContext)
	{
		Log.Info("[EndlessMode] Queued endless transition after current event option task.");
		TaskHelper.RunSafely(RunQueuedEndlessModeTransition(eventModel, capturedTransitionContext));
		return Task.CompletedTask;
	}

	private static async Task RunQueuedEndlessModeTransition(EventModel eventModel, EndlessTransitionContext? capturedTransitionContext)
	{
		await AwaitProcessFramesAsync(1);
		await EnterEndlessModeAsync(eventModel, capturedTransitionContext);
	}

	private static async Task EnterEndlessModeAsync(EventModel eventModel, EndlessTransitionContext? capturedTransitionContext)
	{
		Player? owner = eventModel.Owner;
		RunManager? runManager = RunManager.Instance;
		if (owner == null || runManager?.DebugOnlyGetState() is not RunState state)
		{
			return;
		}

		int nextLoopIndex = capturedTransitionContext?.LoopIndex ?? GetNextLoopIndex(state);
		if (!IsRunAtArchitectEvent(state))
		{
			Log.Info($"[EndlessMode] Ignored stale endless transition request loop={nextLoopIndex} owner={owner.NetId} currentRoom={state.CurrentRoom?.GetType().Name ?? "null"}.");
			return;
		}

		string transitionKey = capturedTransitionContext?.Key ?? BuildEndlessTransitionKey(state, nextLoopIndex);
		if (!TryBeginEndlessTransition(transitionKey, nextLoopIndex, state, out string beginReason))
		{
			Log.Info($"[EndlessMode] Ignored duplicate endless transition request loop={nextLoopIndex} owner={owner.NetId} reason={beginReason}.");
			return;
		}

		if (beginReason != "started")
		{
			Log.Warn($"[EndlessMode] Retrying endless transition loop={nextLoopIndex} owner={owner.NetId} reason={beginReason}.");
		}

		try
		{
			string loopSeed = CreateDeterministicLoopSeed(state, nextLoopIndex);
			await PrepareScreensForEndlessTransitionAsync(nextLoopIndex);
			EndlessLoopConfigSnapshot loopConfig = await ResolveLoopConfigAsync(state, runManager, nextLoopIndex);
			await AwardLoopRewardsAsync(state, loopConfig, nextLoopIndex);
			PrepareRunForLoop(state, loopSeed);
			ResetHextechMayhemForEndlessLoop(state, nextLoopIndex);
			RebuildActsForLoop(state, loopSeed);
			runManager.GenerateRooms();
			LogLoopRolls(state, loopSeed);
			await WaitForMapTransitionFrameAsync();
			await EnterLoopFirstActWithoutRoomFadeAsync(runManager, state, nextLoopIndex);
			CompleteEndlessTransition(transitionKey, nextLoopIndex);
		}
		catch (TimeoutException ex)
		{
			ForgetEndlessTransition(transitionKey);
			Log.Warn($"[EndlessMode] Endless transition aborted and can be retried loop={nextLoopIndex} owner={owner.NetId}: {ex.Message}");
		}
		catch
		{
			ForgetEndlessTransition(transitionKey);
			throw;
		}
	}

	private static async Task<EndlessLoopConfigSnapshot> ResolveLoopConfigAsync(RunState state, RunManager runManager, int nextLoopIndex)
	{
		EndlessLoopConfigSnapshot localConfig = GetLocalLoopConfigSnapshot();
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None or NetGameType.Replay)
		{
			return localConfig;
		}

		PlayerChoiceSynchronizer? synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);
		Player? authorityPlayer = GetLoopRewardConfigAuthorityPlayer(runManager, state);
		if (synchronizer == null || authorityPlayer == null)
		{
			EndlessLoopConfigSnapshot fallback = GetDefaultLoopConfigSnapshot();
			Log.Warn($"[EndlessMode] Loop config sync unavailable; using compile-time defaults loop={nextLoopIndex} {fallback} synchronizer={synchronizer != null} authority={authorityPlayer?.NetId}.");
			return fallback;
		}

		uint choiceId = synchronizer.ReserveChoiceId(authorityPlayer);
		if (gameType == NetGameType.Host)
		{
			synchronizer.SyncLocalChoice(authorityPlayer, choiceId, CreateLoopConfigChoiceResult(nextLoopIndex, localConfig));
			Log.Info($"[EndlessMode] Loop config host sync loop={nextLoopIndex} choiceId={choiceId} authority={authorityPlayer.NetId} {localConfig}.");
			return localConfig;
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = await WaitForRemoteEndlessChoice(
			synchronizer,
			authorityPlayer,
			choiceId,
			result => TryDecodeLoopConfigChoiceResult(result, nextLoopIndex, out _),
			$"loop-config loop={nextLoopIndex}");
		if (!TryDecodeLoopConfigChoiceResult(remoteChoice, nextLoopIndex, out EndlessLoopConfigSnapshot syncedConfig))
		{
			EndlessLoopConfigSnapshot fallback = GetDefaultLoopConfigSnapshot();
			Log.Warn($"[EndlessMode] Loop config sync malformed; using compile-time defaults loop={nextLoopIndex} choiceId={receivedChoiceId} {fallback}.");
			return fallback;
		}

		Log.Info($"[EndlessMode] Loop config client sync loop={nextLoopIndex} choiceId={receivedChoiceId} authority={authorityPlayer.NetId} {syncedConfig} local={localConfig}.");
		return syncedConfig;
	}

	private static EndlessLoopConfigSnapshot GetDefaultLoopConfigSnapshot()
	{
		return new EndlessLoopConfigSnapshot(
			EndlessModeConfig.GetDefaultEnabledRewardFlags(),
			EndlessModeConfig.DefaultPlagueSpearPercent,
			EndlessModeConfig.DefaultPlagueShieldPercent);
	}

	private static EndlessLoopConfigSnapshot GetLocalLoopConfigSnapshot()
	{
		return new EndlessLoopConfigSnapshot(
			EndlessModeConfig.GetEnabledRewardFlags(),
			EndlessModeConfig.CurrentPlagueSpearPercent,
			EndlessModeConfig.CurrentPlagueShieldPercent);
	}

	private static async Task AwardLoopRewardsAsync(RunState state, EndlessLoopConfigSnapshot loopConfig, int loopIndex)
	{
		int rewardTier = Math.Min(loopIndex, 4);
		foreach (Player player in state.Players)
		{
			await AwardLoopRewardsAsync(player, rewardTier, loopConfig, loopIndex);
		}
	}

	private static async Task AwardLoopRewardsAsync(Player player, int rewardTier, EndlessLoopConfigSnapshot loopConfig, int loopIndex)
	{
		await GrantOrSetPlagueRelicAsync<PlagueSpear>(player, loopIndex, loopConfig.PlagueSpearPercent);
		await GrantOrSetPlagueRelicAsync<PlagueShield>(player, loopIndex, loopConfig.PlagueShieldPercent);

		switch (rewardTier)
		{
			case 1:
				if (EndlessModeConfig.IsRewardEnabled(loopConfig.EnabledRewardFlags, EndlessOptionalReward.MimicInfestation))
				{
					await GrantUniqueRelicAsync<MimicInfestation>(player);
				}
				break;
			case 2:
				if (EndlessModeConfig.IsRewardEnabled(loopConfig.EnabledRewardFlags, EndlessOptionalReward.TimeMaze))
				{
					await GrantUniqueRelicAsync<TimeMaze>(player);
				}
				break;
			case 3:
				if (EndlessModeConfig.IsRewardEnabled(loopConfig.EnabledRewardFlags, EndlessOptionalReward.Muzzle))
				{
					await GrantUniqueRelicAsync<Muzzle>(player);
				}
				break;
			default:
				if (EndlessModeConfig.IsRewardEnabled(loopConfig.EnabledRewardFlags, EndlessOptionalReward.HorribleTrophy))
				{
					await GrantOrSetTrophyAsync(player, Math.Max(1, loopIndex - 3));
				}
				break;
		}
	}

	private static void PrepareRunForLoop(RunState state, string loopSeed)
	{
		AccumulateFloorsBeforeLoop(state);

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
		// 0.109 移除了 (uint seed, ...) 双参构造；改用 ulong 单参。种子映射跨版本不同
		// 无碍：幕重建只要求联机两端（同版本+同模组）一致，重建结果本身会持久化进存档。
#if STS2_109_OR_NEWER
		Rng actRng = new((ulong)(uint)StringHelper.GetDeterministicHashCode(loopSeed));
#else
		Rng actRng = new((uint)StringHelper.GetDeterministicHashCode(loopSeed), 0);
#endif
		List<ActModel> rebuiltActs = ActModel.GetRandomList(actRng, state.UnlockState, state.Players.Count > 1)
			.Select(static act => act.ToMutable())
			.ToList();
		RunStateActsSetter.Invoke(state, new object[] { rebuiltActs });
	}

	private static string CreateDeterministicLoopSeed(RunState state, int nextLoopIndex)
	{
		string actIds = string.Join(",", state.Acts.Select(static act => act.Id.Entry));
		string basis = string.Join(
			"|",
			state.Rng.StringSeed ?? string.Empty,
			nextLoopIndex.ToString(CultureInfo.InvariantCulture),
			state.Players.Count.ToString(CultureInfo.InvariantCulture),
			state.CurrentActIndex.ToString(CultureInfo.InvariantCulture),
			actIds);
		uint high = (uint)StringHelper.GetDeterministicHashCode(basis);
		uint low = (uint)StringHelper.GetDeterministicHashCode(basis + "|EndlessMode");
		return ToSeedString(((ulong)high << 32) | low);
	}

	private static string ToSeedString(ulong value)
	{
		char[] seed = new char[10];
		for (int index = 0; index < seed.Length; index++)
		{
			seed[index] = SeedAlphabet[(int)(value % (ulong)SeedAlphabet.Length)];
			value /= (ulong)SeedAlphabet.Length;
		}

		return new string(seed);
	}

	private static async Task PrepareScreensForEndlessTransitionAsync(int loopIndex)
	{
		PurgeDetachedScreenSets();
		NOverlayStack? overlayStack = NOverlayStack.Instance;
		Node? root = GetUiSearchRoot(overlayStack);
		if (root == null)
		{
			return;
		}

		int overlayCount = overlayStack != null && GodotObject.IsInstanceValid(overlayStack) ? overlayStack.ScreenCount : 0;
		List<NRewardsScreen> rewardScreens = FindNodesOfType<NRewardsScreen>(root);
		List<NCardRewardSelectionScreen> cardRewardScreens = FindNodesOfType<NCardRewardSelectionScreen>(root);
		int detachedRewardCount = 0;
		foreach (NRewardsScreen rewardsScreen in rewardScreens)
		{
			DetachedRewardScreens.Add(rewardsScreen);
			DisableDetachedScreen(rewardsScreen);
			detachedRewardCount += DetachRewardsFromScreen(rewardsScreen);
		}

		foreach (NCardRewardSelectionScreen cardRewardScreen in cardRewardScreens)
		{
			DetachedCardRewardSelectionScreens.Add(cardRewardScreen);
			DisableDetachedScreen(cardRewardScreen);
		}

		if (overlayCount <= 0 && rewardScreens.Count == 0 && cardRewardScreens.Count == 0)
		{
			return;
		}

		Log.Info($"[EndlessMode] Clearing reward screens before endless loop transition loop={loopIndex} overlays={overlayCount} rewardScreens={rewardScreens.Count} cardRewardScreens={cardRewardScreens.Count} detachedRewards={detachedRewardCount}.");
		overlayStack?.Clear();
		RemoveDetachedScreensFromTree(rewardScreens);
		RemoveDetachedScreensFromTree(cardRewardScreens);
		await AwaitProcessFramesAsync(2);
	}

	private static Node? GetUiSearchRoot(NOverlayStack? overlayStack)
	{
		if (overlayStack != null && GodotObject.IsInstanceValid(overlayStack))
		{
			Node? root = overlayStack.GetTree()?.Root;
			return root ?? overlayStack;
		}

		return null;
	}

	private static List<T> FindNodesOfType<T>(Node root)
		where T : Node
	{
		List<T> result = new();
		HashSet<T> seen = new(ReferenceEqualityComparer.Instance);
		CollectNodesOfType(root, result, seen);
		return result;
	}

	private static void CollectNodesOfType<T>(Node node, List<T> result, HashSet<T> seen)
		where T : Node
	{
		if (node is T typed && seen.Add(typed))
		{
			result.Add(typed);
		}

		foreach (Node child in node.GetChildren())
		{
			CollectNodesOfType(child, result, seen);
		}
	}

	private static void DisableDetachedScreen(Control screen)
	{
		screen.Visible = false;
		screen.MouseFilter = Control.MouseFilterEnum.Ignore;
		screen.ProcessMode = Node.ProcessModeEnum.Disabled;
	}

	private static void RemoveDetachedScreensFromTree<T>(IEnumerable<T> screens)
		where T : Node
	{
		foreach (T screen in screens)
		{
			if (!GodotObject.IsInstanceValid(screen))
			{
				continue;
			}

			Node? parent = screen.GetParent();
			if (parent == null)
			{
				continue;
			}

			parent.RemoveChildSafely(screen);
			screen.QueueFreeSafely();
		}
	}

	private static int DetachRewardsFromScreen(NRewardsScreen rewardsScreen)
	{
		int detachedRewardCount = 0;
		if (RewardsScreenRewardsContainerField?.GetValue(rewardsScreen) is Control rewardsContainer)
		{
			foreach (Node child in rewardsContainer.GetChildren().ToList())
			{
				if (child is NRewardButton or NLinkedRewardSet)
				{
					rewardsContainer.RemoveChildSafely(child);
					child.QueueFreeSafely();
					detachedRewardCount++;
				}
			}
		}

		if (RewardsScreenRewardButtonsField?.GetValue(rewardsScreen) is IList<Control> rewardButtons)
		{
			rewardButtons.Clear();
		}

		if (RewardsScreenSkippedRewardButtonsField?.GetValue(rewardsScreen) is IList<Control> skippedRewardButtons)
		{
			skippedRewardButtons.Clear();
		}

		return detachedRewardCount;
	}

	private static bool NRewardsScreenAfterOverlayClosedPrefix(NRewardsScreen __instance)
	{
		if (!IsDetachedRewardScreen(__instance))
		{
			return true;
		}

		Log.Info("[EndlessMode] Suppressed detached rewards screen close after endless loop transition.");
		DetachRewardsFromScreen(__instance);
		MarkRewardsScreenComplete(__instance);
		return false;
	}

	private static bool NRewardsScreenOnProceedButtonPressedPrefix(NRewardsScreen __instance)
	{
		if (!IsDetachedRewardScreen(__instance))
		{
			return true;
		}

		Log.Info("[EndlessMode] Ignored detached rewards screen proceed after endless loop transition.");
		DetachRewardsFromScreen(__instance);
		MarkRewardsScreenComplete(__instance);
		return false;
	}

	private static void MarkRewardsScreenComplete(NRewardsScreen rewardsScreen)
	{
		RewardsScreenIsCompleteField?.SetValue(rewardsScreen, true);
	}

	private static bool NCardRewardSelectionScreenSelectCardPrefix(NCardRewardSelectionScreen __instance)
	{
		if (!IsDetachedCardRewardSelectionScreen(__instance))
		{
			return true;
		}

		Log.Info("[EndlessMode] Ignored detached card reward selection after endless loop transition.");
		return false;
	}

	private static bool NCardRewardSelectionScreenAlternateRewardPrefix(NCardRewardSelectionScreen __instance)
	{
		if (!IsDetachedCardRewardSelectionScreen(__instance))
		{
			return true;
		}

		Log.Info("[EndlessMode] Ignored detached alternate card reward after endless loop transition.");
		return false;
	}

	private static bool IsDetachedRewardScreen(NRewardsScreen rewardsScreen)
	{
		PurgeDetachedScreenSets();
		return DetachedRewardScreens.Contains(rewardsScreen);
	}

	private static bool IsDetachedCardRewardSelectionScreen(NCardRewardSelectionScreen screen)
	{
		PurgeDetachedScreenSets();
		return DetachedCardRewardSelectionScreens.Contains(screen);
	}

	private static void PurgeDetachedScreenSets()
	{
		DetachedRewardScreens.RemoveWhere(static screen => !GodotObject.IsInstanceValid(screen));
		DetachedCardRewardSelectionScreens.RemoveWhere(static screen => !GodotObject.IsInstanceValid(screen));
	}

	private static void ResetHextechMayhemForEndlessLoop(RunState state, int nextLoopIndex)
	{
		foreach (object modifier in state.Modifiers)
		{
			Type modifierType = modifier.GetType();
			if (!string.Equals(modifierType.FullName, HextechMayhemModifierTypeName, StringComparison.Ordinal))
			{
				continue;
			}

			MethodInfo? resetMethod = modifierType.GetMethod(
				HextechResetForEndlessLoopMethodName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				binder: null,
				types: new[] { typeof(string) },
				modifiers: null);
			if (resetMethod == null)
			{
				Log.Warn("[EndlessMode] HextechRunes endless reset skipped: ResetForEndlessLoop(string) not found.");
				return;
			}

			try
			{
				resetMethod.Invoke(modifier, new object[] { $"EndlessMode loop {nextLoopIndex.ToString(CultureInfo.InvariantCulture)}" });
				Log.Info($"[EndlessMode] Reset HextechRunes endless loop state loop={nextLoopIndex}.");
			}
			catch (Exception ex)
			{
				Log.Warn($"[EndlessMode] HextechRunes endless reset failed: {ex}");
			}

			return;
		}
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

	private static async Task WaitForMapTransitionFrameAsync()
	{
		await AwaitProcessFramesAsync(1);

		Log.Info("[EndlessMode] Preserved NMapScreen before endless loop act transition.");
	}

	private static async Task EnterLoopFirstActWithoutRoomFadeAsync(RunManager runManager, RunState state, int loopIndex)
	{
		if (RunManagerClearScreensMethod == null || RunManagerExitCurrentRoomsMethod == null)
		{
			Log.Warn("[EndlessMode] RunManager fast act transition helpers not found; falling back to EnterAct.");
			await runManager.EnterAct(0);
			return;
		}

		Log.Info($"[EndlessMode] Entering endless loop first act without room fade loop={loopIndex}.");
		RunManagerClearScreensMethod.Invoke(runManager, null);
		await InvokeRunManagerTask(RunManagerExitCurrentRoomsMethod, runManager);
		Log.Info($"[EndlessMode] Cleared current rooms before endless loop act transition loop={loopIndex} roomCount={state.CurrentRoomCount}.");
		await runManager.SetActInternal(0);
		Log.Info($"[EndlessMode] Set endless loop act index loop={loopIndex} act={state.CurrentActIndex} mapReady={state.Map != null}.");
		await runManager.EnterRoomWithoutExitingCurrentRoom(new MapRoom(), fadeToBlack: false);
		if (RunManagerActEnteredField?.GetValue(runManager) is Action actEntered)
		{
			actEntered.Invoke();
		}
		else
		{
			Log.Warn("[EndlessMode] RunManager.ActEntered backing field not found; continuing with Hook.AfterActEntered only.");
		}

		await Hook.AfterActEntered(state);
		ResetActChangeGuardForLoopRollback(runManager, loopIndex);
		Log.Info($"[EndlessMode] Entered endless loop first act loop={loopIndex} act={state.CurrentActIndex} room={state.CurrentRoom?.GetType().Name ?? "null"}.");
	}

	// 0.108 起 ActChangeSynchronizer.OnPlayerReady 新增 _lastTransitioningActIndex 守卫：
	// 非胜利房且 actIndex <= 该字段的转换投票会被忽略。无尽循环把幕索引倒回 0 后，
	// 上一轮遗留的守卫值会吞掉新一轮的正常转幕投票（表现为一幕 BOSS 后点继续无反应）。
	// 每次循环过渡后重置为 -1；0.107.1 无此字段时静默跳过。
	private static void ResetActChangeGuardForLoopRollback(RunManager runManager, int loopIndex)
	{
		try
		{
			ActChangeSynchronizer? synchronizer = runManager.ActChangeSynchronizer;
			if (synchronizer == null)
			{
				return;
			}

			FieldInfo? guardField = typeof(ActChangeSynchronizer).GetField(
				"_lastTransitioningActIndex",
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (guardField == null)
			{
				return;
			}

			guardField.SetValue(synchronizer, -1);
			Log.Info($"[EndlessMode] Reset act change guard after endless loop rollback loop={loopIndex}.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode] Failed to reset act change guard loop={loopIndex}: {ex.Message}");
		}
	}

	private static async Task InvokeRunManagerTask(MethodInfo method, RunManager runManager)
	{
		if (method.Invoke(runManager, null) is Task task)
		{
			await task;
			return;
		}

		throw new InvalidOperationException($"RunManager.{method.Name} did not return a Task.");
	}

	private static async Task AwaitProcessFramesAsync(int frameCount)
	{
		for (int i = 0; i < frameCount; i++)
		{
			if (NGame.Instance != null && GodotObject.IsInstanceValid(NGame.Instance))
			{
				await NodeUtil.AwaitProcessFrame(NGame.Instance);
			}
		}
	}

	private static async Task GrantUniqueRelicAsync<TRelic>(Player player) where TRelic : RelicModel
	{
		if (player.Relics.OfType<TRelic>().Any())
		{
			return;
		}

		await RelicCmd.Obtain<TRelic>(player);
	}

	private static async Task GrantOrSetStackAsync<TRelic>(Player player, int targetStack) where TRelic : EndlessStackingRelicBase
	{
		TRelic? relic = player.Relics.OfType<TRelic>().FirstOrDefault();
		if (relic == null)
		{
			await RelicCmd.Obtain<TRelic>(player);
			relic = player.Relics.OfType<TRelic>().FirstOrDefault();
		}

		relic?.EnsureStackCount(targetStack);
	}

	private static async Task GrantOrSetPlagueRelicAsync<TRelic>(Player player, int targetStack, int effectPercent)
		where TRelic : EndlessPlagueRelicBase
	{
		await GrantOrSetStackAsync<TRelic>(player, targetStack);
		TRelic? relic = player.Relics.OfType<TRelic>().FirstOrDefault();
		relic?.SetEffectPercent(effectPercent);
	}

	private static async Task GrantOrSetTrophyAsync(Player player, int targetStack)
	{
		HorribleTrophy? trophy = player.Relics.OfType<HorribleTrophy>().FirstOrDefault();
		int before = trophy?.StackCount ?? 0;
		await GrantOrSetStackAsync<HorribleTrophy>(player, targetStack);
		trophy = player.Relics.OfType<HorribleTrophy>().FirstOrDefault();
		if (trophy == null)
		{
			return;
		}

		int cursesToAdd = before == 0
			? Math.Max(0, trophy.StackCount - 1)
			: Math.Max(0, trophy.StackCount - before);
		for (int i = 0; i < cursesToAdd; i++)
		{
			await GrantEnthralledAsync(player);
		}
	}

	private static async Task GrantEnthralledAsync(Player player)
	{
		await CardPileCmd.AddCurseToDeck<Enthralled>(player);
	}

	private static int GetNextRewardTier(Player player)
	{
		return Math.Min(GetCompletedLoopCount(player) + 1, 4);
	}

	private static int GetNextLoopIndex(RunState state)
	{
		return GetCompletedLoopCount(state) + 1;
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
				AddOptionalRewardHoverTip(hoverTips, EndlessOptionalReward.MimicInfestation, ModelDb.Relic<MimicInfestation>());
				break;
			case 2:
				AddOptionalRewardHoverTip(hoverTips, EndlessOptionalReward.TimeMaze, ModelDb.Relic<TimeMaze>());
				break;
			case 3:
				AddOptionalRewardHoverTip(hoverTips, EndlessOptionalReward.Muzzle, ModelDb.Relic<Muzzle>());
				break;
			default:
				AddOptionalRewardHoverTip(hoverTips, EndlessOptionalReward.HorribleTrophy, ModelDb.Relic<HorribleTrophy>());
				break;
		}

		return hoverTips;
	}

	private static void AddOptionalRewardHoverTip(List<IHoverTip> hoverTips, EndlessOptionalReward reward, RelicModel relic)
	{
		if (EndlessModeConfig.IsRewardEnabled(reward))
		{
			hoverTips.Add(CreateRewardHoverTip(relic));
		}
	}

	private static HoverTip CreateRewardHoverTip(RelicModel relic)
	{
		Texture2D? icon = relic.BigIcon;
		if (TryLoadManualTexture(relic, out Texture2D? manualTexture) && manualTexture != null)
		{
			icon = manualTexture;
		}

		HoverTip tip = new(relic.Title, GetSafeRelicDescription(relic), icon)
		{
			Id = relic.Id.Entry
		};
		tip.SetCanonicalModel(relic.CanonicalInstance ?? relic);
		return tip;
	}

	private static string GetSafeRelicDescription(RelicModel relic)
	{
		if (!IsEndlessRelic(relic))
		{
			return relic.DynamicDescription.GetFormattedText();
		}

		LocString description = new("relics", relic.Id.Entry + ".description");
		relic.DynamicVars.AddTo(description);
		description.Add("energyPrefix", "red");
		description.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/star_icon.png[/img]");
		return description.GetFormattedText();
	}

	// run 边界（开新局/清理旧局）重置所有跨局静态状态：
	// ScaledEnemyCreatures 只增不清会跨局泄漏 Creature 引用；
	// StartedEndlessTransitionKeys 的 completed 记录同理；Detached 屏幕集合顺带清理。
	private static void ResetStaticStatePostfix()
	{
		ScaledEnemyCreatures.Clear();
		lock (StartedEndlessTransitionKeysLock)
		{
			StartedEndlessTransitionKeys.Clear();
		}

		DetachedRewardScreens.Clear();
		DetachedCardRewardSelectionScreens.Clear();
	}

	private static void AccumulateFloorsBeforeLoop(RunState state)
	{
		int cumulative = GetTotalFloorsBeforeCurrentLoop(state) + Math.Max(0, state.TotalFloor);
		int written = 0;
		foreach (Player player in state.Players)
		{
			foreach (EndlessPlagueRelicBase relic in player.Relics.OfType<EndlessPlagueRelicBase>())
			{
				relic.SavedFloorsBeforeLoop = cumulative;
				written++;
			}
		}

		Log.Info($"[EndlessMode] Accumulated floors before loop cumulative={cumulative} relics={written}.");
	}

	internal static int GetTotalFloorsBeforeCurrentLoop(RunState state)
	{
		int max = 0;
		foreach (Player player in state.Players)
		{
			foreach (EndlessPlagueRelicBase relic in player.Relics.OfType<EndlessPlagueRelicBase>())
			{
				max = Math.Max(max, relic.SavedFloorsBeforeLoop);
			}
		}

		return max;
	}

	private static int GetCompletedLoopCount(Player player)
	{
		return player.Relics.OfType<PlagueSpear>().FirstOrDefault()?.StackCount
			?? player.Relics.OfType<PlagueShield>().FirstOrDefault()?.StackCount
			?? 0;
	}

	internal static int GetCompletedLoopCountForInterop(RunState state)
	{
		return GetCompletedLoopCount(state);
	}

	private static int GetCompletedLoopCount(RunState state)
	{
		if (state.Players.Count == 0)
		{
			return 0;
		}

		return state.Players.Min(static player => GetCompletedLoopCount(player));
	}

	private static bool TryBeginEndlessTransition(string key, int loopIndex, RunState state, out string reason)
	{
		lock (StartedEndlessTransitionKeysLock)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (StartedEndlessTransitionKeys.TryGetValue(key, out EndlessTransitionRecord? existing))
			{
				TimeSpan age = utcNow - existing.StartedUtc;
				if (!existing.Completed && age < EndlessTransitionRetryDelay)
				{
					reason = $"transition-in-progress-{age.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s";
					return false;
				}

				reason = existing.Completed ? "completed-record-rolled-back" : "stale-in-progress-timeout";
				StartedEndlessTransitionKeys.Remove(key);
			}
			else
			{
				reason = "started";
			}

			StartedEndlessTransitionKeys[key] = new EndlessTransitionRecord(loopIndex, utcNow);
			return true;
		}
	}

	private static void ForgetEndlessTransition(string key)
	{
		lock (StartedEndlessTransitionKeysLock)
		{
			StartedEndlessTransitionKeys.Remove(key);
		}
	}

	private static void CompleteEndlessTransition(string key, int loopIndex)
	{
		lock (StartedEndlessTransitionKeysLock)
		{
			if (StartedEndlessTransitionKeys.TryGetValue(key, out EndlessTransitionRecord? existing))
			{
				existing.MarkCompleted(loopIndex);
			}
			else
			{
				EndlessTransitionRecord completed = new(loopIndex, DateTime.UtcNow);
				completed.MarkCompleted(loopIndex);
				StartedEndlessTransitionKeys[key] = completed;
			}
		}
	}

	private static string BuildEndlessTransitionKey(RunState state, int nextLoopIndex)
	{
		return string.Join(
			"|",
			state.Rng.StringSeed ?? string.Empty,
			nextLoopIndex.ToString(CultureInfo.InvariantCulture),
			state.CurrentActIndex.ToString(CultureInfo.InvariantCulture));
	}

	private static EndlessTransitionContext? BuildEndlessTransitionContextForCurrentRun()
	{
		if (RunManager.Instance?.DebugOnlyGetState() is not RunState state)
		{
			return null;
		}

		int loopIndex = GetNextLoopIndex(state);
		return new EndlessTransitionContext(BuildEndlessTransitionKey(state, loopIndex), loopIndex);
	}

	private static bool IsRunAtArchitectEvent(RunState state)
	{
		if (state.CurrentRoom is not EventRoom eventRoom)
		{
			return false;
		}

		try
		{
			return eventRoom.CanonicalEvent.Id.Entry == ArchitectEventId;
		}
		catch (Exception ex) when (IsExpectedGodotLifecycleException(ex))
		{
			return false;
		}
	}

	private static PlayerChoiceResult CreateLoopConfigChoiceResult(int nextLoopIndex, EndlessLoopConfigSnapshot loopConfig)
	{
		return PlayerChoiceResult.FromIndexes([
			EndlessChoiceMagic,
			ChoiceKindLoopRewardConfig,
			nextLoopIndex,
			loopConfig.EnabledRewardFlags,
			loopConfig.PlagueSpearPercent,
			loopConfig.PlagueShieldPercent
		]);
	}

	private static bool TryDecodeLoopConfigChoiceResult(PlayerChoiceResult result, int expectedLoopIndex, out EndlessLoopConfigSnapshot loopConfig)
	{
		loopConfig = default;
		if (!TryGetIndexPayload(result, out List<int>? indexes)
			|| indexes.Count < 6
			|| indexes[0] != EndlessChoiceMagic
			|| indexes[1] != ChoiceKindLoopRewardConfig
			|| indexes[2] != expectedLoopIndex)
		{
			return false;
		}

		loopConfig = new EndlessLoopConfigSnapshot(
			Math.Max(0, indexes[3]),
			EndlessModeConfig.ClampPlagueScalingPercent(indexes[4]),
			EndlessModeConfig.ClampPlagueScalingPercent(indexes[5]));
		return true;
	}

	// Task.Yield 只是微任务让渡，等不了真实时间；改为逐帧等待最多 300 帧（约 5 秒）。
	private static async Task<PlayerChoiceSynchronizer?> WaitForPlayerChoiceSynchronizerAsync(RunManager runManager)
	{
		for (int i = 0; i < 300; i++)
		{
			if (runManager.PlayerChoiceSynchronizer != null)
			{
				return runManager.PlayerChoiceSynchronizer;
			}

			await AwaitProcessFramesAsync(1);
		}

		return runManager.PlayerChoiceSynchronizer;
	}

	// 权威玩家只是 choice 流的键，必须主客两端选出同一个人。
	// Players 槽位顺序是共享运行状态，槽位 0 在两端一致；
	// 旧实现房主按自己 NetId、客户端取第一个，房主不在首位时客户端会在错误的流上等待直到超时。
	private static Player? GetLoopRewardConfigAuthorityPlayer(RunManager runManager, RunState state)
	{
		return state.Players.FirstOrDefault();
	}

	private static async Task<(PlayerChoiceResult Result, uint ChoiceId)> WaitForRemoteEndlessChoice(
		PlayerChoiceSynchronizer synchronizer,
		Player player,
		uint initialChoiceId,
		Func<PlayerChoiceResult, bool> isExpected,
		string context)
	{
		uint choiceId = initialChoiceId;
		int skipped = 0;
		TimeSpan waited = TimeSpan.Zero;
		while (true)
		{
			// 房主发完配置就继续推进了；客户端这里若轻易超时放弃，房主已进新一轮而
			// 客户端留在事件房，直接永久分叉。所以只按周期告警、拉长到硬上限才放弃
			// （此时连接多半已死，走可重试的 abort 路径）。
			Task<PlayerChoiceResult> remoteChoiceTask = synchronizer.WaitForRemoteChoice(player, choiceId);
			while (true)
			{
				Task finishedTask = await Task.WhenAny(remoteChoiceTask, Task.Delay(RemoteEndlessChoiceWarnInterval));
				if (ReferenceEquals(finishedTask, remoteChoiceTask))
				{
					break;
				}

				waited += RemoteEndlessChoiceWarnInterval;
				if (waited >= RemoteEndlessChoiceMaxWait)
				{
					throw new TimeoutException($"Timed out waiting for endless multiplayer choice context={context} player={player.NetId} choiceId={choiceId} waited={waited.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s.");
				}

				Log.Warn($"[EndlessMode] Still waiting for endless multiplayer choice context={context} player={player.NetId} choiceId={choiceId} waited={waited.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s.");
			}

			PlayerChoiceResult remoteChoice = await remoteChoiceTask;
			if (isExpected(remoteChoice))
			{
				if (skipped > 0)
				{
					Log.Info($"[EndlessMode] WaitForRemoteEndlessChoice accepted after skipping foreign choices context={context} player={player.NetId} choiceId={choiceId} skipped={skipped}.");
				}

				return (remoteChoice, choiceId);
			}

			skipped++;
			Log.Warn($"[EndlessMode] WaitForRemoteEndlessChoice skipped non-endless choice context={context} player={player.NetId} choiceId={choiceId} skipped={skipped} type={remoteChoice.ChoiceType} result={remoteChoice}.");
			choiceId = synchronizer.ReserveChoiceId(player);
		}
	}

	private static bool TryGetIndexPayload(PlayerChoiceResult result, out List<int> payload)
	{
		payload = [];
		try
		{
			List<int>? indexes = result.AsIndexes();
			if (indexes == null)
			{
				return false;
			}

			payload = indexes;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static LocString GetEndlessDescription(int rewardTier, int nextLoopIndex)
	{
		LocString description = new("events", "ENDLESS_MODE.enter.description");
		description.Add("optional_reward", GetOptionalRewardDescription(rewardTier));
		description.Add("loop", nextLoopIndex);
		return description;
	}

	private static string GetOptionalRewardDescription(int rewardTier)
	{
		EndlessOptionalReward reward = EndlessModeConfig.GetOptionalRewardForTier(rewardTier);
		if (!EndlessModeConfig.IsRewardEnabled(reward))
		{
			return string.Empty;
		}

		string key = reward switch
		{
			EndlessOptionalReward.MimicInfestation => "ENDLESS_MODE.enter.optional.mimic",
			EndlessOptionalReward.TimeMaze => "ENDLESS_MODE.enter.optional.time_maze",
			EndlessOptionalReward.Muzzle => "ENDLESS_MODE.enter.optional.muzzle",
			EndlessOptionalReward.HorribleTrophy => "ENDLESS_MODE.enter.optional.horrible_trophy",
			_ => string.Empty
		};
		return string.IsNullOrEmpty(key) ? string.Empty : new LocString("events", key).GetFormattedText();
	}

	private static bool GainMaxHpPrefix(Creature creature, decimal amount, ref Task __result)
	{
		if (ShouldPreventMaxHpIncrease(creature, creature.MaxHp + amount))
		{
			__result = Task.CompletedTask;
			return false;
		}

		return true;
	}

	private static bool SetMaxHpPrefix(Creature creature, decimal amount, ref Task<decimal> __result)
	{
		if (ShouldPreventMaxHpIncrease(creature, amount))
		{
			__result = Task.FromResult(0m);
			return false;
		}

		return true;
	}

	private static void HealPrefix(Creature creature, ref decimal amount)
	{
		amount = ModifyHealAmount(creature, amount);
	}

	private static void PowerCmdApplyPowerPrefix(PowerModel power, Creature target, ref decimal amount)
	{
		if (amount <= 0m || target.Side != CombatSide.Enemy)
		{
			return;
		}

		if ((target.Monster is SkulkingColony && power is HardenedShellPower)
			|| (target.Monster is Exoskeleton && power is HardToKillPower))
		{
			decimal multiplier = GetEnemyEndlessScalingMultiplier(GetCurrentRunState(target));
			if (multiplier > 1m)
			{
				amount = Math.Ceiling(amount * multiplier);
			}
		}
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

		decimal multiplier = GetEnemyEndlessScalingMultiplier(GetCurrentRunState(creature));
		if (creature.Side == CombatSide.Enemy && multiplier > 1m)
		{
			modifiedAmount *= multiplier;
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

	private static bool GetCurrentMapPointHistoryEntryPrefix(RunState __instance, ref MapPointHistoryEntry? __result)
	{
		if (MapPointHistoryField.GetValue(__instance) is not List<List<MapPointHistoryEntry>> mapPointHistory)
		{
			return true;
		}

		if (__instance.CurrentActIndex < 0 || __instance.CurrentActIndex >= mapPointHistory.Count)
		{
			__result = null;
			return false;
		}

		List<MapPointHistoryEntry> currentActHistory = mapPointHistory[__instance.CurrentActIndex];
		if (currentActHistory.Count == 0)
		{
			__result = null;
			return false;
		}

		__result = currentActHistory[^1];
		return false;
	}

	private static void NMapScreenSetMapPrefix(NMapScreen __instance)
	{
		MapScreenBossPointNodeField.SetValue(__instance, null);
		MapScreenSecondBossPointNodeField.SetValue(__instance, null);
		MapScreenStartingPointNodeField.SetValue(__instance, null);
	}

	private static void GetUnlockStateRelicsPostfix(ref IEnumerable<RelicModel> __result)
	{
		__result = __result.Concat(GetCustomCanonicalRelics()).Distinct();
	}

	private static bool IsRelicSeenPrefix(RelicModel relic, ref bool __result)
	{
		if (IsEndlessRelic(relic))
		{
			__result = true;
			return false;
		}

		return true;
	}

	private static void CreateCreaturePostfix(CombatState __instance, Creature __result)
	{
		ApplyPlagueShieldScaling(__result, __instance.RunState);
	}

	private static void AddCreaturePostfix(CombatState __instance, Creature creature)
	{
		ApplyPlagueShieldScaling(creature, __instance.RunState);
	}

	private static bool RelicInventoryOnRelicClickedPrefix(NRelicInventory __instance, RelicModel model)
	{
		List<RelicModel> relics = new();
		if (RelicInventoryNodesField?.GetValue(__instance) is IEnumerable<NRelicInventoryHolder> holders)
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
		return false;
	}

	private static bool EnergyIconHelperGetPrefixPrefix(AbstractModel model, ref string __result)
	{
		if (model is RelicModel relic && IsEndlessRelic(relic))
		{
			__result = "red";
			return false;
		}

		return true;
	}

	private static void NRelicReloadPostfix(NRelic __instance)
	{
		try
		{
			RelicModel? model = __instance.Model;

			if (model != null && TryLoadManualTexture(model, out Texture2D? texture) && texture != null)
			{
				ApplyManualRelicTexture(__instance, model, texture);
			}
		}
		catch (Exception ex) when (ex is InvalidOperationException || IsExpectedGodotLifecycleException(ex))
		{
			Log.Warn($"[EndlessMode][Inspect] Skipped relic texture reload because the node was no longer valid: {ex.GetType().Name}");
		}
	}

	private static bool RelicModelTextureGetterPrefix(RelicModel __instance, ref Texture2D __result)
	{
		if (TryLoadManualTexture(__instance, out Texture2D? texture) && texture != null)
		{
			__result = texture;
			return false;
		}

		return true;
	}

	private static void InspectRelicScreenOpenPrefix(ref IReadOnlyList<RelicModel> relics, RelicModel relic)
	{
		List<RelicModel> correctedRelics = relics.ToList();
		int correctedIndex = correctedRelics.FindIndex(candidate => ReferenceEquals(candidate, relic) || candidate.Id == relic.Id);
		if (correctedIndex < 0)
		{
			correctedRelics.Add(relic);
			correctedIndex = correctedRelics.Count - 1;
		}

		relics = correctedRelics;
	}

	private static void InspectRelicScreenOpenPostfix(NInspectRelicScreen __instance, IReadOnlyList<RelicModel> relics, RelicModel relic)
	{
		EnsureInspectRelicsUnlocked(__instance, relics);
		int correctedIndex = relics.ToList().FindIndex(candidate => ReferenceEquals(candidate, relic) || candidate.Id == relic.Id);
		if (correctedIndex >= 0)
		{
			InspectRelicScreenRelicsField!.SetValue(__instance, relics);
			InspectRelicScreenSetRelicMethod!.Invoke(__instance, new object[] { correctedIndex });
		}

		Log.Info($"[EndlessMode][Inspect] Open relic={relic.Id.Entry} correctedIndex={correctedIndex} correctedList=[{string.Join(", ", relics.Select(static r => r.Id.Entry))}]");
		InspectRelicScreenUpdateRelicDisplayMethod!.Invoke(__instance, null);
	}

	private static bool InspectRelicScreenUpdateRelicDisplayPrefix(NInspectRelicScreen __instance)
	{
		if (InspectRelicScreenRelicsField?.GetValue(__instance) is IReadOnlyList<RelicModel> relics
			&& InspectRelicScreenIndexField?.GetValue(__instance) is int index
			&& index >= 0
			&& index < relics.Count)
		{
			RelicModel relic = relics[index];
			if (IsEndlessRelic(relic))
			{
				Log.Info($"[EndlessMode][Inspect] Force render relic={relic.Id.Entry} index={index}");
				RenderEndlessInspect(__instance, relic);
				return false;
			}
		}

		return true;
	}

	private static void ApplyPlagueShieldScaling(Creature creature, IRunState runState)
	{
		if (creature.Side != CombatSide.Enemy || !ScaledEnemyCreatures.Add(creature))
		{
			return;
		}

		decimal multiplier = GetEnemyEndlessScalingMultiplier(runState);
		if (multiplier <= 1m)
		{
			return;
		}

		decimal scaledHp = Math.Ceiling(creature.MaxHp * multiplier);
		creature.SetMaxHpInternal(scaledHp);
		creature.SetCurrentHpInternal(scaledHp);
	}

	private static decimal GetEnemyEndlessScalingMultiplier(IRunState? runState)
	{
		PlagueShield? primaryRelic = GetPrimarySharedRelic<PlagueShield>(runState);
		int stackCount = primaryRelic?.StackCount ?? GetPlagueShieldStackCount(runState);
		int effectPercent = primaryRelic?.EffectPercent ?? EndlessModeConfig.DefaultPlagueShieldPercent;
		return GetEndlessScalingMultiplier(stackCount, effectPercent);
	}

	internal static decimal GetSharedPlagueSpearDamageMultiplier(PlagueSpear relic)
	{
		return GetEndlessScalingMultiplier(
			GetSharedPlagueSpearStackCount(relic),
			GetSharedPlagueEffectPercent(relic));
	}

	internal static decimal GetSharedPlagueShieldMultiplier(PlagueShield relic)
	{
		return GetEndlessScalingMultiplier(
			GetSharedPlagueShieldStackCount(relic),
			GetSharedPlagueEffectPercent(relic));
	}

	private static decimal GetEndlessScalingMultiplier(int stackCount, int effectPercent)
	{
		return 1m + EndlessModeConfig.ClampPlagueScalingPercent(effectPercent) / 100m * Math.Max(0, stackCount);
	}

	internal static bool ShouldApplyPlagueSpearEffect(PlagueSpear relic)
	{
		return IsPrimarySharedRelic(relic, GetRunStateForRelic(relic));
	}

	internal static bool ShouldApplyPlagueShieldEffect(PlagueShield relic)
	{
		return IsPrimarySharedRelic(relic, GetRunStateForRelic(relic));
	}

	internal static int GetSharedPlagueSpearStackCount(PlagueSpear relic)
	{
		return GetSharedStackCount(relic, GetPlagueSpearStackCount);
	}

	internal static int GetSharedPlagueShieldStackCount(PlagueShield relic)
	{
		return GetSharedStackCount(relic, GetPlagueShieldStackCount);
	}

	private static IRunState? GetCurrentRunState(Creature creature)
	{
		return creature.Player?.RunState ?? RunManager.Instance?.DebugOnlyGetState();
	}

	private static IRunState? GetRunStateForRelic(RelicModel relic)
	{
		return relic.Owner?.RunState ?? RunManager.Instance?.DebugOnlyGetState();
	}

	private static bool IsRestSiteHeal(Creature creature)
	{
		return GetCurrentRunState(creature)?.CurrentRoom is RestSiteRoom;
	}

	private static int GetPlagueShieldStackCount(IRunState? runState)
	{
		return GetMaxRelicStackCount<PlagueShield>(runState);
	}

	private static int GetPlagueSpearStackCount(IRunState? runState)
	{
		return GetMaxRelicStackCount<PlagueSpear>(runState);
	}

	private static int GetMaxRelicStackCount<TRelic>(IRunState? runState) where TRelic : EndlessStackingRelicBase
	{
		if (runState == null)
		{
			return 0;
		}

		return runState.Players
			.SelectMany(static player => player.Relics)
			.OfType<TRelic>()
			.Select(static relic => relic.StackCount)
			.DefaultIfEmpty(0)
			.Max();
	}

	private static int GetSharedStackCount<TRelic>(TRelic relic, Func<IRunState?, int> getStackCount) where TRelic : EndlessStackingRelicBase
	{
		int stackCount = getStackCount(GetRunStateForRelic(relic));
		return stackCount > 0 ? stackCount : relic.StackCount;
	}

	private static bool IsPrimarySharedRelic<TRelic>(TRelic relic, IRunState? runState) where TRelic : EndlessStackingRelicBase
	{
		if (runState == null)
		{
			return true;
		}

		TRelic? primaryRelic = GetPrimarySharedRelic<TRelic>(runState);
		return primaryRelic == null || ReferenceEquals(primaryRelic, relic);
	}

	private static int GetSharedPlagueEffectPercent<TRelic>(TRelic relic) where TRelic : EndlessPlagueRelicBase
	{
		return GetPrimarySharedRelic<TRelic>(GetRunStateForRelic(relic))?.EffectPercent
			?? relic.EffectPercent;
	}

	private static TRelic? GetPrimarySharedRelic<TRelic>(IRunState? runState) where TRelic : EndlessStackingRelicBase
	{
		if (runState == null)
		{
			return null;
		}

		return runState.Players
			.SelectMany(static player => player.Relics
				.OfType<TRelic>()
				.Select(relic => (Player: player, Relic: relic)))
			.OrderByDescending(static entry => entry.Relic.StackCount)
			.ThenBy(static entry => entry.Player.NetId)
			.Select(static entry => entry.Relic)
			.FirstOrDefault();
	}

	private static bool IsEndlessRelic(RelicModel relic)
	{
		return relic.CanonicalInstance is EndlessRelicBase || relic is EndlessRelicBase;
	}

	private static void EnsureInspectRelicsUnlocked(NInspectRelicScreen screen, IReadOnlyList<RelicModel> relics)
	{
		if (InspectRelicScreenUnlockedRelicsField?.GetValue(screen) is not HashSet<RelicModel> unlockedRelics)
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
		MegaLabel nameLabel = (MegaLabel)InspectRelicScreenNameLabelField!.GetValue(screen)!;
		MegaLabel rarityLabel = (MegaLabel)InspectRelicScreenRarityLabelField!.GetValue(screen)!;
		MegaRichTextLabel description = (MegaRichTextLabel)InspectRelicScreenDescriptionField!.GetValue(screen)!;
		MegaRichTextLabel flavor = (MegaRichTextLabel)InspectRelicScreenFlavorField!.GetValue(screen)!;
		TextureRect image = (TextureRect)InspectRelicScreenImageField!.GetValue(screen)!;
		Control hoverTipRect = (Control)InspectRelicScreenHoverTipRectField!.GetValue(screen)!;

		nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
		LocString rarityText = new("gameplay_ui", "RELIC_RARITY." + relic.Rarity.ToString().ToUpperInvariant());
		rarityLabel.SetTextAutoSize(rarityText.GetFormattedText());
		image.SelfModulate = Colors.White;
		description.SetTextAutoSize(GetSafeRelicDescription(relic));
		flavor.SetTextAutoSize(relic.Flavor.GetFormattedText());
		InspectRelicScreenSetRarityVisualsMethod!.Invoke(screen, new object[] { relic.Rarity });
		Texture2D? texture = relic.BigIcon;
		if (TryLoadManualTexture(relic, out Texture2D? manualTexture) && manualTexture != null)
		{
			texture = manualTexture;
		}

		image.Texture = texture;
		Log.Info($"[EndlessMode][Inspect] Render title={relic.Title.GetFormattedText()} rarity={relic.Rarity} texture={texture?.ResourcePath ?? "<null>"}");

		NHoverTipSet.Clear();
		if (NHoverTipSet.CreateAndShow(screen, relic.HoverTipsExcludingRelic) is { } hoverTipSet)
		{
			hoverTipSet.SetAlignment(hoverTipRect, HoverTip.GetHoverTipAlignment(screen));
		}
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
			if (IsTextureUsable(cachedTexture))
			{
				texture = cachedTexture;
				return true;
			}

			ManualTextureCache.Remove(path);
		}

		string resourceName = endlessRelic.EmbeddedIconResourceName;
		if (TryLoadEmbeddedTexture(resourceName, out texture) && texture != null)
		{
			ManualTextureCache[path] = texture;
			return true;
		}

		try
		{
			if (ResourceLoader.Exists(path))
			{
				texture = ResourceLoader.Load<Texture2D>(path, cacheMode: ResourceLoader.CacheMode.Reuse);
			}

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

	private static bool TryLoadEmbeddedTexture(string resourceName, out Texture2D? texture)
	{
		texture = null;
		try
		{
			using Stream? stream = typeof(ModEntry).Assembly.GetManifestResourceStream(resourceName);
			if (stream == null)
			{
				return false;
			}

			using MemoryStream buffer = new();
			stream.CopyTo(buffer);
			Image image = new();
			Error error = image.LoadPngFromBuffer(buffer.ToArray());
			if (error != Error.Ok)
			{
				Log.Warn($"[EndlessMode][Inspect] Embedded texture load failed for {resourceName}: {error}.");
				return false;
			}

			texture = ImageTexture.CreateFromImage(image);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[EndlessMode][Inspect] Embedded texture load failed for {resourceName}: {ex.Message}");
			return false;
		}
	}

	private static void ApplyManualRelicTexture(NRelic relicNode, RelicModel model, Texture2D texture)
	{
		if (model is not EndlessRelicBase endlessRelic)
		{
			return;
		}

		string path = endlessRelic.PackedIconPath;
		if (!TryApplyManualRelicTexture(relicNode, texture, path))
		{
			ManualTextureCache.Remove(path);
			if (TryLoadManualTexture(model, out Texture2D? freshTexture) && freshTexture != null)
			{
				TryApplyManualRelicTexture(relicNode, freshTexture, path);
			}
		}
	}

	private static bool TryApplyManualRelicTexture(NRelic relicNode, Texture2D texture, string path)
	{
		try
		{
			if (!IsTextureUsable(texture))
			{
				ManualTextureCache.Remove(path);
				return false;
			}

			if (relicNode.Icon is not { } icon)
			{
				return false;
			}

			icon.Texture = texture;
			if (relicNode.Outline is { } outline)
			{
				outline.Visible = false;
			}
			return true;
		}
		catch (Exception ex) when (IsExpectedGodotLifecycleException(ex))
		{
			ManualTextureCache.Remove(path);
			return false;
		}
	}

	private static bool IsTextureUsable(Texture2D texture)
	{
		try
		{
			return GodotObject.IsInstanceValid(texture) && texture.GetWidth() > 0;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		catch (NullReferenceException)
		{
			return false;
		}
	}

	private static bool IsExpectedGodotLifecycleException(Exception ex)
	{
		return ex is InvalidOperationException or ObjectDisposedException or NullReferenceException;
	}

	private readonly record struct EndlessTransitionContext(string Key, int LoopIndex);

	private readonly record struct EndlessLoopConfigSnapshot(int EnabledRewardFlags, int PlagueSpearPercent, int PlagueShieldPercent);

	private sealed class EndlessTransitionRecord
	{
		public EndlessTransitionRecord(int loopIndex, DateTime startedUtc)
		{
			LoopIndex = loopIndex;
			StartedUtc = startedUtc;
		}

		public int LoopIndex { get; private set; }

		public DateTime StartedUtc { get; }

		public bool Completed { get; private set; }

		public void MarkCompleted(int loopIndex)
		{
			LoopIndex = loopIndex;
			Completed = true;
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
		MethodInfo? method = TryGetMethod(type, name, flags, parameters);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find required method {type.FullName}.{name}.");
		}

		return method;
	}

	private static MethodInfo? TryGetMethod(Type type, string name, BindingFlags flags, params Type[] parameters)
	{
		return type.GetMethod(name, flags, binder: null, parameters, modifiers: null);
	}

	private static MethodInfo RequirePropertyGetter(Type type, string propertyName, BindingFlags flags)
	{
		MethodInfo? getter = TryGetPropertyGetter(type, propertyName, flags);
		if (getter == null)
		{
			throw new InvalidOperationException($"Could not find required getter {type.FullName}.{propertyName}.");
		}

		return getter;
	}

	private static MethodInfo? TryGetPropertyGetter(Type type, string propertyName, BindingFlags flags)
	{
		return type.GetProperty(propertyName, flags)?.GetMethod;
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
		FieldInfo? field = TryGetField(type, name);
		if (field == null)
		{
			throw new InvalidOperationException($"Could not find required field {type.FullName}.{name}.");
		}

		return field;
	}

	private static FieldInfo? TryGetField(Type type, string name)
	{
		return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
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

	public string EmbeddedIconResourceName => $"{ModEntryConstants.ModId}.images.relics.{_iconFileName}";

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
			EnsureStackCount(value, flash: false);
		}
	}

	public override bool IsStackable => true;

	public override bool ShowCounter => true;

	public override int DisplayAmount => !base.IsCanonical ? StackCount : 0;

	public void AddStack()
	{
		EnsureStackCount(StackCount + 1);
	}

	public int EnsureStackCount(int targetStack, bool flash = true)
	{
		int target = Math.Max(1, targetStack);
		int before = StackCount;
		while (StackCount < target)
		{
			IncrementStackCount();
		}

		if (StackCount != before)
		{
			InvokeDisplayAmountChanged();
			if (flash)
			{
				Flash();
			}
		}

		return StackCount - before;
	}
}

public abstract class EndlessPlagueRelicBase : EndlessStackingRelicBase
{
	private int? _effectPercent;

	protected EndlessPlagueRelicBase(string iconFileName)
		: base(iconFileName)
	{
	}

	protected abstract int DefaultEffectPercent { get; }

	public int EffectPercent => _effectPercent ?? DefaultEffectPercent;

	protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Percent", EffectPercent)];

	[SavedProperty]
	public int SavedEffectPercent
	{
		get => EffectPercent;
		set => SetEffectPercent(value);
	}

	// 进入新一轮时 mapPointHistory 会被清空、RunState.TotalFloor 归零；
	// 这里累计"往轮已走过的总楼层"，供跨轮连续的楼层计数（EndlessModeInterop）使用。
	// 两端在同一确定性转换流程里写入相同值；旧档缺省为 0（从当前轮起算，自然回退）。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedFloorsBeforeLoop { get; set; }

	public void SetEffectPercent(int percent)
	{
		int clamped = EndlessModeConfig.ClampPlagueScalingPercent(percent);
		if (_effectPercent == clamped)
		{
			return;
		}

		_effectPercent = clamped;
		if (DynamicVars.TryGetValue("Percent", out DynamicVar? percentVar))
		{
			percentVar.BaseValue = clamped;
		}
	}
}

internal static class ModEntryConstants
{
	public const string ModId = "EndlessMode";

	public const int TimeMazeCardLimit = 15;
}

public sealed class PlagueSpear : EndlessPlagueRelicBase
{
	public PlagueSpear()
		: base("spear.png")
	{
	}

	protected override int DefaultEffectPercent => EndlessModeConfig.DefaultPlagueSpearPercent;

#if STS2_108_OR_NEWER
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
#endif
	{
		if (dealer?.Side != CombatSide.Enemy || amount <= 0m || !props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered))
		{
			return 1m;
		}

		if (!ModEntry.ShouldApplyPlagueSpearEffect(this))
		{
			return 1m;
		}

		return ModEntry.GetSharedPlagueSpearDamageMultiplier(this);
	}
}

public sealed class PlagueShield : EndlessPlagueRelicBase
{
	public PlagueShield()
		: base("shield.png")
	{
	}

	protected override int DefaultEffectPercent => EndlessModeConfig.DefaultPlagueShieldPercent;

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (target.Side != CombatSide.Enemy || block <= 0m)
		{
			return 1m;
		}

		if (!ModEntry.ShouldApplyPlagueShieldEffect(this))
		{
			return 1m;
		}

		return ModEntry.GetSharedPlagueShieldMultiplier(this);
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
	private int _trackedRoundNumber = -1;

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

		ResetForCurrentRoundIfNeeded();
		return !ShouldPreventCardPlay;
	}

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner == Owner)
		{
			ResetForCurrentRoundIfNeeded();
			_cardsPlayedThisTurn++;
			InvokeDisplayAmountChanged();
		}

		return Task.CompletedTask;
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is CombatRoom)
		{
			ResetCounter(GetCurrentRoundNumber());
		}

		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetCounter(-1);
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner)
		{
			ResetCounter(GetCurrentRoundNumber());
		}

		return Task.CompletedTask;
	}

	private void ResetForCurrentRoundIfNeeded()
	{
		int roundNumber = GetCurrentRoundNumber();
		if (roundNumber >= 0 && roundNumber != _trackedRoundNumber)
		{
			ResetCounter(roundNumber);
		}
	}

	private void ResetCounter(int roundNumber)
	{
		bool changed = _cardsPlayedThisTurn != 0 || _trackedRoundNumber != roundNumber;
		_cardsPlayedThisTurn = 0;
		_trackedRoundNumber = roundNumber;
		if (changed)
		{
			InvokeDisplayAmountChanged();
		}
	}

	private int GetCurrentRoundNumber()
	{
		return Owner?.Creature?.CombatState?.RoundNumber ?? -1;
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

	protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
	{
		if (Owner?.RunState is not RunState runState)
		{
			return;
		}

		CardModel copy = runState.CreateCard<Pride>(Owner);
		await CardPileCmd.Add(copy, PileType.Discard, CardPilePosition.Top, this);
	}
}
