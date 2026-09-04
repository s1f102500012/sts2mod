using IntegratedStrategyEvents.Encounters;
using IntegratedStrategyEvents.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class SpecialFinaleCoordinator
{
	public static bool IsAtEternalDustFirstEventPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.EternalDust &&
			IntegratedStrategyEternalDustFinaleActMap.IsFirstEventCoord(state.CurrentMapCoord);
	}

	public static bool IsAtEternalDustSecondEventPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.EternalDust &&
			IntegratedStrategyEternalDustFinaleActMap.IsSecondEventCoord(state.CurrentMapCoord);
	}

	public static bool IsAtRadiantApexCombatPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.RadiantApex &&
			IntegratedStrategyRadiantApexFinaleActMap.IsCombatCoord(state.CurrentMapCoord);
	}

	public static bool IsAtAbyssalJungleSublimationEventPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.AbyssalJungle &&
			IntegratedStrategyAbyssalJungleFinaleActMap.IsEventCoord(state.CurrentMapCoord);
	}

	public static bool IsAtAbyssalJungleOdeEventPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.AbyssalJungleIsharmla &&
			IntegratedStrategyAbyssalJungleFinaleActMap.IsEventCoord(state.CurrentMapCoord);
	}

	public static bool IsAtProphetHornFragmentEventPoint(RunState state)
	{
		return TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			session.Kind == SpecialFinaleKind.ProphetHornFragment &&
			IntegratedStrategyProphetHornFragmentActMap.IsEventCoord(state.CurrentMapCoord);
	}

	public static bool HandleEnterNextAct(RunManager runManager, ref Task result)
	{
		RunState? state = runManager.DebugOnlyGetState();
		if (state == null)
		{
			return true;
		}

		if (TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession finaleSession))
		{
			if (finaleSession.Kind == SpecialFinaleKind.ProphetHornFragment &&
				IsEndlessFinaleBossComplete(state, finaleSession))
			{
				result = ReturnFromProphetHornFragment(runManager, state, finaleSession);
				return false;
			}

			if (IsEndlessFinaleBossComplete(state, finaleSession))
			{
				TreeHoleSessionManager.RestoreOriginalMapForArchitect(state, finaleSession);
			}

			return true;
		}

		if (ShouldCompleteArchitectAfterEndlessFinale(state))
		{
			if (TryCompleteArchitectAfterEndlessFinale(runManager, state, out Task? completionTask))
			{
				result = completionTask;
				return false;
			}

			return true;
		}

		SpecialFinaleKind? finaleKind = GetSpecialFinaleEntryKind(state);
		if (finaleKind == null)
		{
			return true;
		}

		IntegratedStrategyTemporaryMapAction.EnqueueSpecialFinaleEntry(
			GetLocalActionOwner(runManager, state),
			finaleKind.Value);
		result = Task.CompletedTask;
		return false;
	}

	public static void SuppressArchitectActChangeOptions(EventModel eventModel)
	{
		if (!ShouldSuppressArchitectActChangeOptions(eventModel))
		{
			return;
		}

		if (eventModel.CurrentOptions is not List<EventOption> options || options.Count <= 1)
		{
			return;
		}

		int originalCount = options.Count;
		options.RemoveAll(static option => !IsBaseArchitectOption(option));
		int removedCount = originalCount - options.Count;
		if (removedCount > 0)
		{
			Log.Info($"{ModInfo.LogPrefix} Suppressed {removedCount} non-vanilla Architect option(s) after endless finale.");
		}
	}

	public static IEnumerable<EventOption> FilterArchitectActChangeOptionsForDisplay(
		EventModel eventModel,
		IEnumerable<EventOption> options)
	{
		if (!ShouldSuppressArchitectActChangeOptions(eventModel))
		{
			return options;
		}

		List<EventOption> optionList = options.ToList();
		List<EventOption> filteredOptions = optionList.Where(IsBaseArchitectOption).ToList();
		int removedCount = optionList.Count - filteredOptions.Count;
		if (removedCount > 0)
		{
			Log.Info($"{ModInfo.LogPrefix} Hid {removedCount} non-vanilla Architect option button(s) after endless finale.");
		}

		return filteredOptions;
	}

	public static bool ShouldChooseArchitectOption(EventModel eventModel, EventOption option)
	{
		if (!ShouldSuppressArchitectActChangeOptions(eventModel) || IsBaseArchitectOption(option))
		{
			return true;
		}

		Log.Warn(
			$"{ModInfo.LogPrefix} Blocked non-vanilla Architect option '{option.TextKey}' " +
			"after endless finale.");
		SuppressArchitectActChangeOptions(eventModel);
		return false;
	}

	public static Task EnterProphetHornFragmentFromEvent(Player owner, string destinationActName, string stageLabel)
	{
		IntegratedStrategyTemporaryMapAction.EnqueueProphetHornFragmentEntry(owner, destinationActName, stageLabel);
		return Task.CompletedTask;
	}

	public static bool HandleCreateRoom(RoomType roomType, AbstractModel? model, ref AbstractRoom result)
	{
		if (!ShouldForceEndlessFinaleBoss(roomType, model, out _))
		{
			return true;
		}

		result = CreateEndlessFinaleBossRoom(model);
		return false;
	}

	public static void EnsureCreatedRoomIsEndlessFinaleBoss(
		RoomType roomType,
		AbstractModel? model,
		ref AbstractRoom result)
	{
		if (!ShouldForceEndlessFinaleBoss(roomType, model, out SpecialFinaleKind finaleKind) ||
			IsExpectedFinaleBossRoom(result, finaleKind))
		{
			return;
		}

		AbstractModel? replacedModel = result is CombatRoom combatRoom
			? combatRoom.Encounter
			: model;
		result = CreateEndlessFinaleBossRoom(replacedModel);
	}

	public static BossNodeRenderSwap? BeginEndlessFinaleBossNodeRender(MapPoint point)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state == null ||
			!TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) ||
			!point.coord.Equals(session.FinaleMap.BossMapPoint.coord))
		{
			return null;
		}

		SerializableActModel originalActSave = state.Act.ToSave();
		state.Act.SetBossEncounter(GetFinaleBossEncounter(session.Kind));
		state.Act.SetSecondBossEncounter(null);
		return new BossNodeRenderSwap(state, originalActSave);
	}

	public static void EndEndlessFinaleBossNodeRender(BossNodeRenderSwap? swap)
	{
		if (swap == null)
		{
			return;
		}

		TreeHoleRunAccessor.RestoreActRooms(swap.State, swap.OriginalActSave);
	}

	public static void OnRoomEntered()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state != null &&
			TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession finaleSession))
		{
			TreeHoleFinaleMusicCoordinator.PlayForEnteredRoom(finaleSession);
		}
	}

	private static SpecialFinaleKind? GetSpecialFinaleEntryKind(RunState state)
	{
		if (TreeHoleSessionManager.IsActive(state) ||
			state.CurrentActIndex < state.Acts.Count - 1 ||
			state.CurrentRoom is not CombatRoom { RoomType: RoomType.Boss, IsVictoryRoom: false })
		{
			return null;
		}

		if (HasTatteredDoll(state))
		{
			return SpecialFinaleKind.DesireHall;
		}

		if (HasAnasaKarma(state))
		{
			return SpecialFinaleKind.CarefreeVihara;
		}

		if (HasTimeAndLight(state))
		{
			return SpecialFinaleKind.RadiantApex;
		}

		if (HasDimensionalFluid(state))
		{
			return SpecialFinaleKind.EternalDust;
		}

		if (HasBishopResearch(state))
		{
			return SpecialFinaleKind.AbyssalJungleIsharmla;
		}

		if (HasDeepBlueMemory(state))
		{
			return SpecialFinaleKind.AbyssalJungle;
		}

		return HasEndlessKey(state) ? SpecialFinaleKind.EndlessFinale : null;
	}

	internal static Task EnterSpecialFinaleFromSyncedAction(Player owner, SpecialFinaleKind finaleKind)
	{
		if (!IntegratedStrategyPatcher.IsAvailable("temporary-map")) throw new InvalidOperationException("Temporary maps unavailable.");
		if (owner.RunState is not RunState state)
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to enter special finale without a run state.");
			return Task.CompletedTask;
		}

		if (GetSpecialFinaleEntryKind(state) != finaleKind)
		{
			Log.Warn($"{ModInfo.LogPrefix} Special finale synced entry was ignored because the run state changed.");
			return Task.CompletedTask;
		}

		if (!TreeHoleSessionManager.AddPendingFinaleEntry(state))
		{
			return Task.CompletedTask;
		}

		return EnterSpecialFinale(RunManager.Instance, state, finaleKind);
	}

	internal static Task EnterProphetHornFragmentFromSyncedAction(
		Player owner,
		string destinationActName,
		string stageLabel)
	{
		return EnterProphetHornFragmentFromEventDeferred(owner, destinationActName, stageLabel);
	}

	private static bool HasEndlessKey(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is EndlessKeyRelic));
	}

	private static Player GetLocalActionOwner(RunManager runManager, RunState state)
	{
		return state.Players.FirstOrDefault(player => player.NetId == runManager.NetService.NetId) ??
			state.Players.First();
	}

	private static bool HasDimensionalFluid(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is DimensionalFluidRelic));
	}

	private static bool HasTimeAndLight(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is TimeAndLightRelic));
	}

	private static bool HasTatteredDoll(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is TatteredDollRelic));
	}

	private static bool HasAnasaKarma(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is AnasaKarmaRelic));
	}

	private static bool HasDeepBlueMemory(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is DeepBlueMemoryRelic));
	}

	private static bool HasBishopResearch(RunState state)
	{
		return state.Players.Any(static player =>
			player.Relics.Any(static relic => !relic.IsMelted && relic is BishopResearchRelic));
	}

	private static async Task EnterSpecialFinale(
		RunManager runManager,
		RunState state,
		SpecialFinaleKind finaleKind)
	{
		try
		{
			if (!IntegratedStrategyPatcher.IsAvailable("temporary-map"))
				throw new InvalidOperationException("Temporary maps are unavailable; see the startup compatibility report.");
			await TreeHoleSessionManager.AwaitNextProcessFrame();
			if (!ReferenceEquals(runManager.DebugOnlyGetState(), state) ||
				GetSpecialFinaleEntryKind(state) != finaleKind)
			{
				Log.Warn($"{ModInfo.LogPrefix} Special finale entry was cancelled because the run state changed.");
				return;
			}


			TreeHoleRunAccessor.ClearScreens(runManager);
			ActMap finaleMap = CreateFinaleMap(finaleKind, state);
			EndlessFinaleSession session = new(
				state.Map,
				state.VisitedMapCoords.ToList(),
				state.MapPointHistory.Select(static history => history.ToList()).ToList(),
				state.ActFloor,
				state.Act.ToSave(),
				GetFinaleStageLabel(finaleKind),
				GetFinaleActName(finaleKind),
				finaleMap,
				finaleKind);

			TreeHoleSessionManager.SetFinaleSession(state, session);
			state.Map = finaleMap;
			state.ClearVisitedMapCoordsDebug();
			state.AddVisitedMapCoord(finaleMap.StartingMapPoint.coord);
			if (finaleKind == SpecialFinaleKind.EndlessFinale)
			{
				await HealPlayersToFull(state);
			}
			TreeHoleSessionManager.RefreshLocationSynchronizers(state);
			await TreeHoleRunAccessor.FadeOut();
			TreeHoleSessionManager.SetMapScreen(finaleMap, state, initMarker: false);

			Log.Info($"{ModInfo.LogPrefix} Entering {session.DestinationActName} finale act.");
			await runManager.EnterRoom(new MapRoom());
			TreeHoleFinaleMusicCoordinator.PlayAfterFinaleEntry(finaleKind);
			await TreeHoleRunAccessor.PersistCurrentRunTransition(
				runManager,
				$"{session.DestinationActName} temporary map entry");

			await TreeHoleRunAccessor.FadeIn(runManager, showTransition: true);
		}
		finally
		{
			TreeHoleSessionManager.RemovePendingFinaleEntry(state);
		}
	}

	private static async Task EnterProphetHornFragmentFromEventDeferred(
		Player owner,
		string destinationActName,
		string stageLabel)
	{
		if (!IntegratedStrategyPatcher.IsAvailable("temporary-map"))
			throw new InvalidOperationException("Temporary maps are unavailable; see the startup compatibility report.");
		RunManager runManager = RunManager.Instance;
		if (owner.RunState is not RunState state)
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to enter Prophet Horn fragment without a run state.");
			return;
		}

		if (TreeHoleSessionManager.IsActive(state))
		{
			Log.Warn($"{ModInfo.LogPrefix} Tried to enter Prophet Horn fragment while a temporary map is already active.");
			return;
		}

		if (!TreeHoleSessionManager.AddPendingFinaleEntry(state))
		{
			return;
		}

		try
		{
			await TreeHoleSessionManager.AwaitNextProcessFrame();
			if (!ReferenceEquals(runManager.DebugOnlyGetState(), state))
			{
				Log.Warn($"{ModInfo.LogPrefix} Prophet Horn fragment entry was cancelled because the run state changed.");
				return;
			}

			if (!await TreeHoleEntryCoordinator.WaitForEventOptionSettlement(state, destinationActName)) return;


			Log.Info($"{ModInfo.LogPrefix} Preparing to enter {destinationActName} Prophet Horn fragment.");
			await TreeHoleRunAccessor.ExitCurrentRooms(runManager);
			TreeHoleRunAccessor.ClearScreens(runManager);
			IntegratedStrategyProphetHornFragmentActMap fragmentMap = new();
			EndlessFinaleSession session = new(
				state.Map,
				state.VisitedMapCoords.ToList(),
				state.MapPointHistory.Select(static history => history.ToList()).ToList(),
				state.ActFloor,
				state.Act.ToSave(),
				stageLabel,
				destinationActName,
				fragmentMap,
				SpecialFinaleKind.ProphetHornFragment);

			TreeHoleSessionManager.SetFinaleSession(state, session);
			state.Map = fragmentMap;
			state.ClearVisitedMapCoordsDebug();
			state.AddVisitedMapCoord(fragmentMap.StartingMapPoint.coord);
			TreeHoleSessionManager.RefreshLocationSynchronizers(state);
			await TreeHoleRunAccessor.FadeOut();
			TreeHoleSessionManager.SetMapScreen(fragmentMap, state, initMarker: false);

			Log.Info($"{ModInfo.LogPrefix} Entering {destinationActName} Prophet Horn fragment.");
			await TreeHoleRunAccessor.EnterRoomInternal(runManager, new MapRoom());
			await TreeHoleRunAccessor.PersistCurrentRunTransition(
				runManager,
				$"{destinationActName} temporary map entry");
			await TreeHoleRunAccessor.FadeIn(runManager, showTransition: true);
		}
		finally
		{
			TreeHoleSessionManager.RemovePendingFinaleEntry(state);
		}
	}

	private static async Task ReturnFromProphetHornFragment(
		RunManager runManager,
		RunState state,
		EndlessFinaleSession session)
	{

		TreeHoleRunAccessor.ClearScreens(runManager);
		TreeHoleSessionManager.RestoreOriginalMapFromFinale(state, session);
		await runManager.EnterRoom(new MapRoom());
		await TreeHoleRunAccessor.PersistCurrentRunTransition(
			runManager,
			$"return from {session.DestinationActName} finale");
		await TreeHoleRunAccessor.FadeIn(runManager, showTransition: true);
	}

	internal static async Task PersistArchitectHandoffAfterEnterNextAct(
		RunManager runManager,
		RunState expectedState,
		Task enterNextActTask)
	{
		await enterNextActTask;
		if (!ReferenceEquals(runManager.DebugOnlyGetState(), expectedState) ||
			!TreeHoleSessionManager.HasPendingArchitectCompletion(expectedState) ||
			expectedState.CurrentRoom is not EventRoom { CanonicalEvent: TheArchitect })
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not persist the finale Architect handoff because the room changed.");
			return;
		}

		await TreeHoleRunAccessor.PersistCurrentRunTransition(
			runManager,
			"special finale Architect handoff",
			TreeHoleResumeRoom.Architect);
	}

	private static async Task HealPlayersToFull(RunState state)
	{
		foreach (Player player in state.Players)
		{
			await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
		}
	}

	private static bool IsEndlessFinaleBossComplete(RunState state, EndlessFinaleSession session)
	{
		return state.CurrentRoom is CombatRoom combatRoom &&
			!combatRoom.IsVictoryRoom &&
			IsAtEndlessFinaleBossPoint(state, session) &&
			IsExpectedFinaleEncounter(combatRoom.Encounter, session.Kind);
	}

	private static bool IsAtEndlessFinaleBossPoint(RunState state, EndlessFinaleSession session)
	{
		return state.CurrentMapCoord.HasValue &&
			state.CurrentMapCoord.Value.Equals(session.FinaleMap.BossMapPoint.coord);
	}

	private static bool ShouldForceEndlessFinaleBoss(
		RoomType roomType,
		AbstractModel? model,
		out SpecialFinaleKind finaleKind)
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		if (state != null &&
			roomType == RoomType.Boss &&
			TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session) &&
			IsAtEndlessFinaleBossPoint(state, session))
		{
			finaleKind = session.Kind;
			return true;
		}

		finaleKind = default;
		return false;
	}

	private static CombatRoom CreateEndlessFinaleBossRoom(AbstractModel? replacedModel)
	{
		RunState state = RunManager.Instance.DebugOnlyGetState() ??
			throw new InvalidOperationException("Cannot create endless finale boss room without an active run state.");
		if (!TreeHoleSessionManager.TryGetFinaleSession(state, out EndlessFinaleSession session))
		{
			throw new InvalidOperationException("Cannot create special finale boss room without an active finale session.");
		}

		EncounterModel encounter = GetFinaleBossEncounter(session.Kind).ToMutable();
		CombatRoom room = new(encounter, state);
		if (replacedModel is EncounterModel incomingEncounter)
		{
			Log.Info(
				$"{ModInfo.LogPrefix} Forced {session.DestinationActName} finale boss encounter to " +
				$"{encounter.Id} instead of {incomingEncounter.Id}.");
		}

		return room;
	}

	private static bool IsExpectedFinaleBossRoom(AbstractRoom result, SpecialFinaleKind finaleKind)
	{
		return result is CombatRoom combatRoom &&
			IsExpectedFinaleEncounter(combatRoom.Encounter, finaleKind);
	}

	// 每种终局一行：BOSS 遭遇类型、遭遇实例、终局图工厂、阶段标签、幕名。
	// 新增终局 = 在这里加一行（外加 SpecialFinaleKind 枚举值与入口逻辑），
	// 不再需要同步维护多个平行 switch。
	private sealed record FinaleProfile(
		Type BossEncounterType,
		Func<EncounterModel> GetBossEncounter,
		Func<RunState, ActMap> CreateMap,
		string StageLabel,
		string ActName);

	private static readonly IReadOnlyDictionary<SpecialFinaleKind, FinaleProfile> FinaleProfiles =
		new Dictionary<SpecialFinaleKind, FinaleProfile>
		{
			[SpecialFinaleKind.EndlessFinale] = new(
				typeof(FurnaceFinaleAmiyaEncounter),
				static () => ModelDb.Encounter<FurnaceFinaleAmiyaEncounter>(),
				static _ => new IntegratedStrategyEndlessFinaleActMap(),
				TreeHoleConstants.EndlessFinaleStageLabel,
				TreeHoleConstants.EndlessFinaleActName),
			[SpecialFinaleKind.EternalDust] = new(
				typeof(CalendarKingsPincerBossEncounter),
				static () => ModelDb.Encounter<CalendarKingsPincerBossEncounter>(),
				static _ => new IntegratedStrategyEternalDustFinaleActMap(),
				TreeHoleConstants.EternalDustFinaleStageLabel,
				TreeHoleConstants.EternalDustFinaleActName),
			[SpecialFinaleKind.RadiantApex] = new(
				typeof(BozhokastiSaintguardGunnerBossEncounter),
				static () => ModelDb.Encounter<BozhokastiSaintguardGunnerBossEncounter>(),
				CreateRadiantApexFinaleMap,
				TreeHoleConstants.RadiantApexFinaleStageLabel,
				TreeHoleConstants.RadiantApexFinaleActName),
			[SpecialFinaleKind.CarefreeVihara] = new(
				typeof(KuilongMahasattvaAvatarBossEncounter),
				static () => ModelDb.Encounter<KuilongMahasattvaAvatarBossEncounter>(),
				CreateCarefreeViharaFinaleMap,
				TreeHoleConstants.CarefreeViharaFinaleStageLabel,
				TreeHoleConstants.CarefreeViharaFinaleActName),
			[SpecialFinaleKind.AbyssalJungle] = new(
				typeof(IzumikEcologicalFountainBossEncounter),
				static () => ModelDb.Encounter<IzumikEcologicalFountainBossEncounter>(),
				static _ => new IntegratedStrategyAbyssalJungleFinaleActMap(),
				TreeHoleConstants.AbyssalJungleFinaleStageLabel,
				TreeHoleConstants.AbyssalJungleFinaleActName),
			[SpecialFinaleKind.AbyssalJungleIsharmla] = new(
				typeof(IsharmlaCorruptedHeartBossEncounter),
				static () => ModelDb.Encounter<IsharmlaCorruptedHeartBossEncounter>(),
				static _ => new IntegratedStrategyAbyssalJungleFinaleActMap(),
				TreeHoleConstants.AbyssalJungleFinaleStageLabel,
				TreeHoleConstants.AbyssalJungleFinaleActName),
			[SpecialFinaleKind.ProphetHornFragment] = new(
				typeof(FrostNovaWinterScarBossEncounter),
				static () => ModelDb.Encounter<FrostNovaWinterScarBossEncounter>(),
				static _ => new IntegratedStrategyProphetHornFragmentActMap(),
				TreeHoleConstants.StrangeFragmentStageLabel,
				TreeHoleConstants.StrangeFragmentActName),
			[SpecialFinaleKind.DesireHall] = new(
				typeof(SorrowfulLockBossEncounter),
				static () => ModelDb.Encounter<SorrowfulLockBossEncounter>(),
				CreateDesireHallFinaleMap,
				TreeHoleConstants.DesireHallFinaleStageLabel,
				TreeHoleConstants.DesireHallFinaleActName)
		};

	private static FinaleProfile GetFinaleProfile(SpecialFinaleKind finaleKind)
	{
		return FinaleProfiles.TryGetValue(finaleKind, out FinaleProfile? profile)
			? profile
			: throw new ArgumentOutOfRangeException(nameof(finaleKind), finaleKind, null);
	}

	private static bool IsExpectedFinaleEncounter(EncounterModel encounter, SpecialFinaleKind finaleKind)
	{
		if (!FinaleProfiles.TryGetValue(finaleKind, out FinaleProfile? profile))
		{
			return false;
		}

		Type bossType = profile.BossEncounterType;
		return bossType.IsInstanceOfType(encounter) ||
			(encounter.CanonicalInstance is { } canonical && bossType.IsInstanceOfType(canonical));
	}

	private static ActMap CreateFinaleMap(SpecialFinaleKind finaleKind, RunState state)
	{
		return GetFinaleProfile(finaleKind).CreateMap(state);
	}

	private static IntegratedStrategyRadiantApexFinaleActMap CreateRadiantApexFinaleMap(RunState state)
	{
		Rng rng = new(TreeHoleSeedFactory.CreateRadiantApexCombatNodeSeed(state), "integrated_strategy_radiant_apex_combat_nodes");
		MapPointType firstCombatPointType = RollRadiantApexCombatPointType(rng);
		MapPointType secondCombatPointType = RollRadiantApexCombatPointType(rng);
		Log.Info(
			$"{ModInfo.LogPrefix} Radiant Apex combat nodes generated as " +
			$"{firstCombatPointType} and {secondCombatPointType}.");
		return new IntegratedStrategyRadiantApexFinaleActMap(firstCombatPointType, secondCombatPointType);
	}

	private static IntegratedStrategyDesireHallFinaleActMap CreateDesireHallFinaleMap(RunState state)
	{
		Rng rng = new(TreeHoleSeedFactory.CreateRadiantApexCombatNodeSeed(state), "integrated_strategy_desire_hall_combat_nodes");
		MapPointType firstCombatPointType = RollRadiantApexCombatPointType(rng);
		MapPointType secondCombatPointType = RollRadiantApexCombatPointType(rng);
		Log.Info(
			$"{ModInfo.LogPrefix} Desire Hall combat nodes generated as " +
			$"{firstCombatPointType} and {secondCombatPointType}.");
		return new IntegratedStrategyDesireHallFinaleActMap(firstCombatPointType, secondCombatPointType);
	}

	private static IntegratedStrategyCarefreeViharaFinaleActMap CreateCarefreeViharaFinaleMap(RunState state)
	{
		Rng rng = new(TreeHoleSeedFactory.CreateRadiantApexCombatNodeSeed(state), "integrated_strategy_carefree_vihara_combat_nodes");
		MapPointType firstCombatPointType = RollRadiantApexCombatPointType(rng);
		MapPointType secondCombatPointType = RollRadiantApexCombatPointType(rng);
		Log.Info(
			$"{ModInfo.LogPrefix} Carefree Vihara combat nodes generated as " +
			$"{firstCombatPointType} and {secondCombatPointType}.");
		return new IntegratedStrategyCarefreeViharaFinaleActMap(firstCombatPointType, secondCombatPointType);
	}

	private static MapPointType RollRadiantApexCombatPointType(Rng rng)
	{
		return rng.NextBool() ? MapPointType.Elite : MapPointType.Monster;
	}

	private static string GetFinaleStageLabel(SpecialFinaleKind finaleKind)
	{
		return GetFinaleProfile(finaleKind).StageLabel;
	}

	private static string GetFinaleActName(SpecialFinaleKind finaleKind)
	{
		return GetFinaleProfile(finaleKind).ActName;
	}

	private static EncounterModel GetFinaleBossEncounter(SpecialFinaleKind finaleKind)
	{
		return GetFinaleProfile(finaleKind).GetBossEncounter();
	}

	public static bool ShouldAllowRepeatedActTransition()
	{
		RunState? state = RunManager.Instance.DebugOnlyGetState();
		return state != null &&
			(TreeHoleSessionManager.TryGetFinaleSession(state, out _) ||
			 TreeHoleSessionManager.HasPendingArchitectCompletion(state));
	}

	private static bool ShouldCompleteArchitectAfterEndlessFinale(RunState state)
	{
		return TreeHoleSessionManager.HasPendingArchitectCompletion(state) &&
			state.CurrentRoom?.IsVictoryRoom == true;
	}

	private static bool TryCompleteArchitectAfterEndlessFinale(
		RunManager runManager,
		RunState state,
		out Task completionTask)
	{
		completionTask = Task.CompletedTask;
		if (!TreeHoleRunAccessor.TryWinRun(runManager, out Task task))
		{
			Log.Warn($"{ModInfo.LogPrefix} Could not complete endless finale Architect handoff via RunManager.WinRun.");
			return false;
		}

		completionTask = FinishArchitectCompletion(state, task);
		Log.Info($"{ModInfo.LogPrefix} Completing run from endless finale Architect handoff.");
		return true;
	}

	private static async Task FinishArchitectCompletion(RunState state, Task completionTask)
	{
		await completionTask;
		TreeHoleSessionManager.RemovePendingArchitectCompletion(state);
		IntegratedStrategyTreeHoleSaveStateStore.Clear(state);
	}

	private static bool IsBaseArchitectOption(EventOption option)
	{
		return option.TextKey == "PROCEED" ||
			option.TextKey.StartsWith("THE_ARCHITECT.dialogue.", StringComparison.Ordinal);
	}

	private static bool ShouldSuppressArchitectActChangeOptions(EventModel eventModel)
	{
		return eventModel is TheArchitect &&
			eventModel.Owner?.RunState is RunState state &&
			TreeHoleSessionManager.HasPendingArchitectCompletion(state);
	}
}
