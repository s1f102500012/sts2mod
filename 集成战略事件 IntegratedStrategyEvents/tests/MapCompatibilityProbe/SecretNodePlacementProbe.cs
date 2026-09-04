using System.Reflection;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;

internal static class SecretNodePlacementProbe
{
	internal static void Run(Assembly assembly)
	{
		Type placement = assembly.GetType("IntegratedStrategyEvents.Map.SecretNodePlacement", throwOnError: true)!;
		MethodInfo prepare = placement.GetMethod("PrepareFirstAct", BindingFlags.Static | BindingFlags.NonPublic)!;
		MethodInfo select = placement.GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic)!;
		HashSet<MapCoord> Select(ActMap map, int actIndex, uint seed) =>
			(HashSet<MapCoord>)select.Invoke(null, [map, actIndex, seed])!;
		int Prepare(ActMap map, uint seed) => (int)prepare.Invoke(null, [map, seed])!;
		int cases = 0;
		foreach (int rows in new[] { 9, 16, 17, 24 })
		foreach (int unknowns in new[] { 0, 1, 2, 6 })
		foreach (int lateUnknowns in new[] { 0, 1, 3 })
		for (uint seed = 0; seed < 128; seed++)
		{
			PlacementMap map = new(rows, unknowns, lateUnknowns);
			Dictionary<MapCoord, MapPointType> original = Types(map);
			HashSet<MapCoord> baseline = OldSelection(map, seed);
			Require(Select(map, 1, seed).SetEquals(baseline) && Select(map, 2, seed).SetEquals(baseline),
				"later acts must retain the exact original selection");
			Select(map, 0, seed);
			Require(Types(map).SequenceEqual(original), "selection queries must not mutate the map");
			Require(Prepare(map, seed) == 0, "ordinary maps must have enough late nodes");
			HashSet<MapCoord> selected = Select(map, 0, seed);
			Require(selected.Count == baseline.Count, "first-act secret count must match the original seed");
			Require(selected.All(coord => coord.row >= (rows + 1) / 2), "no early first-act secret nodes");
			Require(selected.All(coord => map.GetPoint(coord) is { PointType: MapPointType.Unknown, CanBeModified: true }),
				"selected nodes must be mutable unknown nodes");
			Dictionary<MapCoord, MapPointType> prepared = Types(map);
			Require(original.Values.Order().SequenceEqual(prepared.Values.Order()), "room-type totals must be preserved");
			Require(map.GetAllMapPoints().Where(point => !point.CanBeModified ||
				original[point.coord] is not (MapPointType.Unknown or MapPointType.Monster))
				.All(point => point.PointType == original[point.coord]), "protected and special nodes must not move");
			Require(Prepare(map, seed) == 0 && selected.SetEquals(Select(map, 0, seed)) && Types(map).SequenceEqual(prepared),
				"repeated generation must be idempotent");
			PlacementMap restored = new(rows, unknowns, lateUnknowns);
			foreach (MapPoint point in restored.GetAllMapPoints())
				point.PointType = prepared[point.coord];
			Require(Prepare(restored, seed) == 0 && selected.SetEquals(Select(restored, 0, seed)),
				"restored point types must reconstruct identical secret coordinates");
			cases++;
		}

		PlacementMap blocked = new(17, 6, 0);
		foreach (MapPoint point in blocked.GetAllMapPoints().Where(point => point.coord.row >= 9))
			point.CanBeModified = false;
		Dictionary<MapCoord, MapPointType> blockedTypes = Types(blocked);
		Require(Prepare(blocked, 42) > 0 && Select(blocked, 0, 42).Count == 0 && Types(blocked).SequenceEqual(blockedTypes),
			"external maps with no eligible late nodes must report a shortfall without bypassing protection");
		Console.WriteLine($"Secret-node placement probe passed: {cases} seed/layout cases and protected-map fallback.");
	}

	private static HashSet<MapCoord> OldSelection(ActMap map, uint seed)
	{
		List<MapPoint> candidates = map.GetAllMapPoints()
			.Where(point => point.PointType == MapPointType.Unknown && point.CanBeModified)
			.OrderBy(point => point.coord.row).ThenBy(point => point.coord.col).ToList();
		if (candidates.Count == 0)
			return [];
		Rng rng = new(seed, "integrated_strategy_secret_map_nodes");
		rng.Shuffle(candidates);
		int count = rng.NextInt(1, Math.Min(3, candidates.Count) + 1);
		return candidates.Take(count).Select(point => point.coord).ToHashSet();
	}

	private static Dictionary<MapCoord, MapPointType> Types(ActMap map) =>
		map.GetAllMapPoints().ToDictionary(point => point.coord, point => point.PointType);

	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidOperationException(message);
	}

	private sealed class PlacementMap : ActMap
	{
		protected override MapPoint?[,] Grid { get; }
		public override MapPoint BossMapPoint { get; }
		public override MapPoint StartingMapPoint { get; } = new(1, 0);

		internal PlacementMap(int rows, int unknowns, int lateUnknowns)
		{
			Grid = new MapPoint[3, rows];
			BossMapPoint = new MapPoint(1, rows);
			for (int row = 1; row < rows; row++)
			for (int col = 0; col < 3; col++)
				Grid[col, row] = new MapPoint(col, row) { PointType = MapPointType.Monster, CanBeModified = true };
			int firstLateRow = (rows + 1) / 2;
			foreach (MapPoint point in GetAllMapPoints().Where(point => point.coord.row < firstLateRow).Take(unknowns))
				point.PointType = MapPointType.Unknown;
			foreach (MapPoint point in GetAllMapPoints().Where(point => point.coord.row >= firstLateRow).Take(lateUnknowns))
				point.PointType = MapPointType.Unknown;
			Grid[2, firstLateRow]!.PointType = MapPointType.Elite;
			Grid[2, firstLateRow + 1]!.PointType = MapPointType.Shop;
			Grid[2, firstLateRow + 2]!.PointType = MapPointType.RestSite;
			Grid[1, firstLateRow]!.PointType = MapPointType.Treasure;
			Grid[1, firstLateRow + 1]!.CanBeModified = false;
		}
	}
}
