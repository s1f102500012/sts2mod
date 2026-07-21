using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.RunData;

namespace IntegratedStrategyEvents.TreeHoles;

internal static class IntegratedStrategyTreeHoleSaveStateStore
{
	private const int CurrentVersion = 5;
	private const string StateFileName = "integrated_strategy_tree_hole_state.json";
	private static readonly RunSavedData<TreeHolePersistedState> SavedState =
		RunSavedDataStore.For(ModInfo.ModId).Register(
			"tree_hole_state",
			static () => new TreeHolePersistedState(),
			new RunSavedDataOptions
			{
				SchemaVersion = 1,
				WritePolicy = RunSavedDataWritePolicy.WhenSet
			});
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	public static void Initialize()
	{
		// Force slot registration during mod initialization, before RitsuLib imports
		// any SerializableRun into a newly constructed RunState.
		_ = SavedState;
	}

	public static void Save(
		RunState state,
		SerializableRun run,
		TreeHoleSaveSnapshot snapshot,
		TreeHoleResumeRoom resumeRoom)
	{
		try
		{
			SerializableActMap temporaryMap = SerializableActMap.FromActMap(snapshot.CurrentMap);
			SerializableActMap originalMap = SerializableActMap.FromActMap(snapshot.OriginalMap);
			TreeHolePersistedState persistedState = new()
			{
				Version = CurrentVersion,
				StartTime = run.StartTime,
				ResumeRoom = resumeRoom.ToString(),
				Kind = snapshot.Kind.ToString(),
				CurrentActIndex = snapshot.CurrentActIndex,
				ParentActId = snapshot.ParentActId,
				CurrentActFloor = snapshot.CurrentActFloor,
				CurrentMapCoord = snapshot.CurrentMapCoord,
				CurrentMapPointHistoryCounts = snapshot.CurrentMapPointHistory
					.Select(static history => history.Count)
					.ToList(),
				TemporaryMap = temporaryMap,
				TemporaryUnmodifiableCoords = GetUnmodifiableCoords(temporaryMap),
				OriginalMap = originalMap,
				OriginalUnmodifiableCoords = GetUnmodifiableCoords(originalMap),
				OriginalVisitedMapCoords = snapshot.OriginalVisitedMapCoords.ToList(),
				OriginalMapPointHistoryCounts = snapshot.OriginalMapPointHistory
					.Select(static history => history.Count)
					.ToList(),
				OriginalActFloor = snapshot.OriginalActFloor,
				OriginalActSave = snapshot.OriginalActSave,
				TreeHoleMapSeed = snapshot.TreeHoleMapSeed,
				StageLabel = snapshot.StageLabel,
				DestinationActName = snapshot.DestinationActName,
				TerminalCoord = snapshot.TerminalCoord
			};

			SavedState.Set(state, persistedState);
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to save tree-hole restore state: {ex}");
		}
	}

	public static void SaveResumeRoom(
		RunState state,
		SerializableRun run,
		TreeHoleResumeRoom resumeRoom,
		ActMap currentMap)
	{
		try
		{
			SerializableActMap temporaryMap = SerializableActMap.FromActMap(currentMap);
			TreeHolePersistedState persistedState = new()
			{
				Version = CurrentVersion,
				StartTime = run.StartTime,
				ResumeRoom = resumeRoom.ToString(),
				PendingArchitectCompletion = resumeRoom == TreeHoleResumeRoom.Architect,
				CurrentMapCoord = run.VisitedMapCoords.Count == 0
					? null
					: run.VisitedMapCoords[^1],
				CurrentMapPointHistoryCounts = run.MapPointHistory
					.Select(static history => history.Count)
					.ToList(),
				TemporaryMap = temporaryMap,
				TemporaryUnmodifiableCoords = GetUnmodifiableCoords(temporaryMap)
			};
			SavedState.Set(state, persistedState);
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to save the requested resume room: {ex}");
		}
	}

	public static void RemoveFromSave(RunState state)
	{
		try
		{
			SavedState.Remove(state);
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to remove stale tree-hole restore state: {ex}");
		}
	}

	public static bool TryGetResumeRoom(
		SerializableRun save,
		RunState currentState,
		out TreeHoleResumeRoom resumeRoom,
		out SerializableActMap? expectedMap)
	{
		resumeRoom = TreeHoleResumeRoom.None;
		expectedMap = null;
		try
		{
			if (!TryGetState(save, currentState, out TreeHolePersistedState state))
			{
				return false;
			}

			if (state.PendingArchitectCompletion)
			{
				resumeRoom = TreeHoleResumeRoom.Architect;
				expectedMap = state.TemporaryMap;
				return true;
			}

			bool parsed = Enum.TryParse(state.ResumeRoom, out resumeRoom) &&
				resumeRoom != TreeHoleResumeRoom.None;
			if (parsed)
			{
				expectedMap = state.TemporaryMap;
			}

			return parsed;
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to load the requested resume room: {ex}");
			return false;
		}
	}

	public static TreeHoleSaveSnapshot? CreateSnapshot(RunState? state, TreeHoleSessionStore sessions)
	{
		if (state == null || !sessions.TryGetTreeHoleSession(state, out TreeHoleSession session))
		{
			if (state == null || !sessions.TryGetFinaleSession(state, out EndlessFinaleSession finaleSession))
			{
				return null;
			}

			TreeHoleSaveKind kind = finaleSession.Kind switch
			{
				SpecialFinaleKind.EternalDust => TreeHoleSaveKind.EternalDustFinale,
				SpecialFinaleKind.RadiantApex => TreeHoleSaveKind.RadiantApexFinale,
				SpecialFinaleKind.CarefreeVihara => TreeHoleSaveKind.CarefreeViharaFinale,
				SpecialFinaleKind.AbyssalJungle => TreeHoleSaveKind.AbyssalJungleFinale,
				SpecialFinaleKind.AbyssalJungleIsharmla => TreeHoleSaveKind.AbyssalJungleIsharmlaFinale,
				SpecialFinaleKind.ProphetHornFragment => TreeHoleSaveKind.ProphetHornFragment,
				SpecialFinaleKind.DesireHall => TreeHoleSaveKind.DesireHallFinale,
				_ => TreeHoleSaveKind.EndlessFinale
			};

			return new TreeHoleSaveSnapshot(
				kind,
				state.CurrentActIndex,
				state.Act.Id.Entry,
				state.Map,
				state.CurrentMapCoord,
				state.VisitedMapCoords.ToList(),
				state.MapPointHistory.Select(static history => history.ToList()).ToList(),
				state.ActFloor,
				finaleSession.OriginalMap,
				finaleSession.OriginalVisitedMapCoords,
				finaleSession.OriginalMapPointHistory,
				finaleSession.OriginalActFloor,
				finaleSession.OriginalActSave,
				0U,
				finaleSession.StageLabel,
				finaleSession.DestinationActName,
				finaleSession.FinaleMap.BossMapPoint.coord);
		}

		return new TreeHoleSaveSnapshot(
			TreeHoleSaveKind.TreeHole,
			state.CurrentActIndex,
			state.Act.Id.Entry,
			state.Map,
			state.CurrentMapCoord,
			state.VisitedMapCoords.ToList(),
			state.MapPointHistory.Select(static history => history.ToList()).ToList(),
			state.ActFloor,
			session.OriginalMap,
			session.OriginalVisitedMapCoords,
			session.OriginalMapPointHistory,
			session.OriginalActFloor,
			session.OriginalActSave,
			session.TreeHoleMapSeed,
			session.StageLabel,
			session.DestinationActName,
			session.TerminalCoord);
	}

	public static TreeHoleRestoreSnapshot? Load(SerializableRun save, RunState currentState)
	{
		try
		{
			if (!TryGetState(save, currentState, out TreeHolePersistedState state) ||
				state.PendingArchitectCompletion ||
				state.OriginalMap == null ||
				!Enum.TryParse(state.Kind, out TreeHoleSaveKind kind))
			{
				return null;
			}

			bool parentActMatches = !string.IsNullOrEmpty(state.ParentActId)
				? string.Equals(currentState.Act.Id.Entry, state.ParentActId, StringComparison.Ordinal)
				: state.CurrentActIndex == currentState.CurrentActIndex;
			if (!parentActMatches)
			{
				return null;
			}

			SerializableActModel? originalActSave = state.OriginalActSave;
			if (originalActSave == null && !string.IsNullOrEmpty(state.ParentActId))
			{
				// 幕身份优先按保存的 ActModel ID 精确匹配（第三方模组增删临时幕会使序号漂移）。
				originalActSave = save.Acts.FirstOrDefault(act =>
					string.Equals(act.Id?.Entry, state.ParentActId, StringComparison.Ordinal));
			}

			if (originalActSave == null &&
				state.CurrentActIndex >= 0 &&
				state.CurrentActIndex < save.Acts.Count)
			{
				originalActSave = save.Acts[state.CurrentActIndex];
			}

			if (originalActSave == null)
			{
				return null;
			}

			return new TreeHoleRestoreSnapshot(
				kind,
				state.CurrentActIndex,
				state.ParentActId ?? string.Empty,
				state.CurrentActFloor,
				state.CurrentMapCoord,
				state.TemporaryMap,
				state.OriginalMap,
				state.OriginalVisitedMapCoords ?? [],
				state.OriginalMapPointHistoryCounts ?? [],
				state.OriginalActFloor,
				originalActSave,
				state.TreeHoleMapSeed,
				state.StageLabel ?? string.Empty,
				state.DestinationActName ?? string.Empty,
				state.TerminalCoord);
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to load tree-hole restore state: {ex}");
			return null;
		}
	}

	public static void Clear(RunState state)
	{
		try
		{
			SavedState.TryGet(state, out TreeHolePersistedState? currentState);
			SavedState.Remove(state);
			if (currentState != null)
			{
				ClearLegacyState(currentState.StartTime);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"{ModInfo.LogPrefix} Failed to clear tree-hole restore state: {ex}");
		}
	}

	private static bool TryGetState(
		SerializableRun save,
		RunState currentState,
		out TreeHolePersistedState state)
	{
		if (!SavedState.TryGet(currentState, out state))
		{
			// A client must only trust the Host-provided RunData payload. Its local
			// profile sidecar may belong to a different singleplayer or multiplayer run.
			if (RunManager.Instance.NetService?.Type == NetGameType.Client)
			{
				state = null!;
				return false;
			}

			state = ReadLegacyState()!;
		}

		if (state == null ||
			state.Version < 1 ||
			state.Version > CurrentVersion ||
			state.StartTime != save.StartTime ||
			!SavedRunMatchesState(save, state))
		{
			state = null!;
			return false;
		}

		if (state.TemporaryUnmodifiableCoords == null &&
			Enum.TryParse(state.Kind, out TreeHoleSaveKind legacyKind) &&
			CreateLegacyTemporaryMap(legacyKind, state.TreeHoleMapSeed) is SerializableActMap legacyMap)
		{
			state.TemporaryUnmodifiableCoords = GetUnmodifiableCoords(legacyMap);
		}

		RestoreUnmodifiableCoords(state.TemporaryMap, state.TemporaryUnmodifiableCoords);
		RestoreUnmodifiableCoords(state.OriginalMap, state.OriginalUnmodifiableCoords);
		RestoreUnmodifiableCoords(state.OriginalActSave?.SavedMap, state.OriginalUnmodifiableCoords);
		if (save.CurrentActIndex >= 0 && save.CurrentActIndex < save.Acts.Count)
		{
			// RunManager.InitializeSavedRun consumes this object after FromSerializable
			// returns. Repair it now so disk JSON and multiplayer packet loads generate
			// the same temporary map before ModifyGeneratedMapLate hooks run.
			RestoreUnmodifiableCoords(
				save.Acts[save.CurrentActIndex].SavedMap,
				state.TemporaryUnmodifiableCoords);
		}

		// An old sidecar is imported into the live run bag. The next normal save
		// writes it into RitsuLib's atomic save/network payload.
		SavedState.Set(currentState, state);
		return true;
	}

	private static TreeHolePersistedState? ReadLegacyState()
	{
		string path = GetStatePath();
		return File.Exists(path)
			? JsonSerializer.Deserialize<TreeHolePersistedState>(File.ReadAllText(path), JsonOptions)
			: null;
	}

	private static void ClearLegacyState(long startTime)
	{
		string path = GetStatePath();
		TreeHolePersistedState? legacyState = ReadLegacyState();
		if (legacyState?.StartTime == startTime && File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private static bool SavedRunMatchesState(SerializableRun save, TreeHolePersistedState state)
	{
		if (state.Version < 4)
		{
			return true;
		}

		if (state.TemporaryMap == null ||
			save.CurrentActIndex < 0 ||
			save.CurrentActIndex >= save.Acts.Count)
		{
			return false;
		}

		SerializableActMap? savedMap = save.Acts[save.CurrentActIndex].SavedMap;
		MapCoord? savedCoord = save.VisitedMapCoords.Count == 0
			? null
			: save.VisitedMapCoords[^1];
		List<int> savedHistoryCounts = save.MapPointHistory
			.Select(static history => history.Count)
			.ToList();
		return savedMap != null &&
			SerializableMapsMatch(savedMap, state.TemporaryMap) &&
			Nullable.Equals(savedCoord, state.CurrentMapCoord) &&
			state.CurrentMapPointHistoryCounts != null &&
			savedHistoryCounts.SequenceEqual(state.CurrentMapPointHistoryCounts);
	}

	private static bool SerializableMapsMatch(
		SerializableActMap left,
		SerializableActMap right)
	{
		List<SerializableMapPoint> leftPoints = left.Points ?? [];
		List<SerializableMapPoint> rightPoints = right.Points ?? [];
		if (left.GridWidth != right.GridWidth ||
			left.GridHeight != right.GridHeight ||
			!SerializablePointsMatch(left.StartingPoint, right.StartingPoint) ||
			!SerializablePointsMatch(left.BossPoint, right.BossPoint) ||
			!SerializablePointsMatch(left.SecondBossPoint, right.SecondBossPoint) ||
			!CoordSetsMatch(left.StartMapPointCoords, right.StartMapPointCoords) ||
			leftPoints.Count != rightPoints.Count)
		{
			return false;
		}

		Dictionary<MapCoord, SerializableMapPoint> leftByCoord = leftPoints
			.ToDictionary(static point => point.Coord);
		return rightPoints.All(point =>
			leftByCoord.TryGetValue(point.Coord, out SerializableMapPoint? leftPoint) &&
			SerializablePointsMatch(leftPoint, point));
	}

	private static bool SerializablePointsMatch(
		SerializableMapPoint? left,
		SerializableMapPoint? right)
	{
		if (left == null || right == null)
		{
			return left == right;
		}

		return left.Coord.Equals(right.Coord) &&
			left.PointType == right.PointType &&
			CoordSetsMatch(left.ChildCoords, right.ChildCoords);
	}

	private static List<MapCoord> GetUnmodifiableCoords(SerializableActMap map)
	{
		HashSet<MapCoord> coords = [];
		foreach (SerializableMapPoint point in EnumerateMapPoints(map))
		{
			if (!point.CanBeModified)
			{
				coords.Add(point.Coord);
			}
		}

		return coords.Order().ToList();
	}

	internal static SerializableActMap? CreateLegacyTemporaryMap(
		TreeHoleSaveKind kind,
		uint treeHoleMapSeed)
	{
		ActMap? expectedMap = kind switch
		{
			TreeHoleSaveKind.TreeHole when treeHoleMapSeed != 0 =>
				IntegratedStrategyTreeHoleActMap.Create(new Rng(
					treeHoleMapSeed,
					TreeHoleSeedFactory.TreeHoleMapRngName)),
			TreeHoleSaveKind.EndlessFinale => new IntegratedStrategyEndlessFinaleActMap(),
			TreeHoleSaveKind.EternalDustFinale => new IntegratedStrategyEternalDustFinaleActMap(),
			TreeHoleSaveKind.RadiantApexFinale =>
				new IntegratedStrategyRadiantApexFinaleActMap(MapPointType.Monster, MapPointType.Monster),
			TreeHoleSaveKind.CarefreeViharaFinale =>
				new IntegratedStrategyCarefreeViharaFinaleActMap(MapPointType.Monster, MapPointType.Monster),
			TreeHoleSaveKind.AbyssalJungleFinale => new IntegratedStrategyAbyssalJungleFinaleActMap(),
			TreeHoleSaveKind.AbyssalJungleIsharmlaFinale => new IntegratedStrategyAbyssalJungleFinaleActMap(),
			TreeHoleSaveKind.ProphetHornFragment => new IntegratedStrategyProphetHornFragmentActMap(),
			TreeHoleSaveKind.DesireHallFinale =>
				new IntegratedStrategyDesireHallFinaleActMap(MapPointType.Monster, MapPointType.Monster),
			_ => null
		};
		return expectedMap == null ? null : SerializableActMap.FromActMap(expectedMap);
	}

	private static void RestoreUnmodifiableCoords(
		SerializableActMap? map,
		IReadOnlyCollection<MapCoord>? unmodifiableCoords)
	{
		if (map == null || unmodifiableCoords == null || unmodifiableCoords.Count == 0)
		{
			return;
		}

		HashSet<MapCoord> coordSet = unmodifiableCoords.ToHashSet();
		foreach (SerializableMapPoint point in EnumerateMapPoints(map))
		{
			if (coordSet.Contains(point.Coord))
			{
				point.CanBeModified = false;
			}
		}
	}

	private static IEnumerable<SerializableMapPoint> EnumerateMapPoints(SerializableActMap map)
	{
		HashSet<SerializableMapPoint> yielded = new(ReferenceEqualityComparer.Instance);
		foreach (SerializableMapPoint point in map.Points ?? [])
		{
			if (yielded.Add(point))
			{
				yield return point;
			}
		}

		if (map.StartingPoint != null && yielded.Add(map.StartingPoint))
		{
			yield return map.StartingPoint;
		}

		if (map.BossPoint != null && yielded.Add(map.BossPoint))
		{
			yield return map.BossPoint;
		}

		if (map.SecondBossPoint != null && yielded.Add(map.SecondBossPoint))
		{
			yield return map.SecondBossPoint;
		}
	}

	private static bool CoordSetsMatch(
		IReadOnlyCollection<MapCoord>? left,
		IReadOnlyCollection<MapCoord>? right)
	{
		int leftCount = left?.Count ?? 0;
		int rightCount = right?.Count ?? 0;
		return leftCount == rightCount &&
			(left == null || right != null && left.All(right.Contains));
	}

	private static string GetStatePath()
	{
		string godotPath = SaveManager.Instance.GetProfileScopedPath(
			Path.Combine("IntegratedStrategyEvents", StateFileName));
		return ProjectSettings.GlobalizePath(godotPath);
	}

}

internal sealed class TreeHolePersistedState
{
	public TreeHolePersistedState()
	{
	}

	public int Version { get; set; }

	public long StartTime { get; set; }

	public bool PendingArchitectCompletion { get; set; }

	public string? ResumeRoom { get; set; }

	public string Kind { get; set; } = string.Empty;

	public int CurrentActIndex { get; set; }

	public string? ParentActId { get; set; }

	public int CurrentActFloor { get; set; }

	public MapCoord? CurrentMapCoord { get; set; }

	public List<int>? CurrentMapPointHistoryCounts { get; set; }

	public SerializableActMap? TemporaryMap { get; set; }

	public List<MapCoord>? TemporaryUnmodifiableCoords { get; set; }

	public SerializableActMap? OriginalMap { get; set; }

	public List<MapCoord>? OriginalUnmodifiableCoords { get; set; }

	public List<MapCoord>? OriginalVisitedMapCoords { get; set; }

	public List<int>? OriginalMapPointHistoryCounts { get; set; }

	public int OriginalActFloor { get; set; }

	public SerializableActModel? OriginalActSave { get; set; }

	public uint TreeHoleMapSeed { get; set; }

	public string? StageLabel { get; set; }

	public string? DestinationActName { get; set; }

	public MapCoord TerminalCoord { get; set; }
}
