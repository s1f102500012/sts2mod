using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;

namespace IntegratedStrategyEvents.Map;

internal static class SecretNodePlacement
{
	internal const string RngName = "integrated_strategy_secret_map_nodes";
	private const int MinimumNodes = 1;
	private const int MaximumNodes = 3;

	// 仅由对称的地图生成回调调用；图标查询和惰性坐标重建必须保持只读。
	internal static int PrepareFirstAct(ActMap map, uint seed)
	{
		List<MapPoint> candidates = ShuffleCandidates(map, seed, out int count);
		int firstRow = FirstEligibleRow(map);
		int missing = count - candidates.Count(point => point.coord.row >= firstRow);
		if (missing <= 0)
			return 0;

		List<MapPoint> donors = candidates.Where(point => point.coord.row < firstRow).Take(missing).ToList();
		List<MapPoint> destinations = map.GetAllMapPoints()
			.Where(point => point.coord.row >= firstRow && point.CanBeModified && point.PointType == MapPointType.Monster)
			.OrderBy(static point => point.coord.row)
			.ThenBy(static point => point.coord.col)
			.Take(missing).ToList();
		int moveCount = Math.Min(donors.Count, destinations.Count);
		for (int i = 0; i < moveCount; i++)
		{
			donors[i].PointType = MapPointType.Monster;
			destinations[i].PointType = MapPointType.Unknown;
		}
		return missing - moveCount;
	}

	internal static HashSet<MapCoord> Select(ActMap map, int actIndex, uint seed)
	{
		List<MapPoint> candidates = ShuffleCandidates(map, seed, out int count);
		IEnumerable<MapPoint> eligible = actIndex == 0
			? candidates.Where(point => point.coord.row >= FirstEligibleRow(map))
			: candidates;
		return eligible.Take(count).Select(static point => point.coord).ToHashSet();
	}

	// Grid 的第 0 行不属于普通路线；按实际行数划分，不硬编码原版地图长度。
	private static int FirstEligibleRow(ActMap map) => (map.GetRowCount() + 1) / 2;

	private static List<MapPoint> ShuffleCandidates(ActMap map, uint seed, out int count)
	{
		List<MapPoint> candidates = map.GetAllMapPoints()
			.Where(static point => point.PointType == MapPointType.Unknown && point.CanBeModified)
			.OrderBy(static point => point.coord.row)
			.ThenBy(static point => point.coord.col).ToList();
		count = 0;
		if (candidates.Count == 0)
			return candidates;

		// 数量抽取前仍洗牌整个问号池。对调不改变池大小，因此同种子的数量
		// 与原规则一致，重复生成和读档也不会因后半段过滤而重新抽出较小数量。
		Rng rng = new(seed, RngName);
		rng.Shuffle(candidates);
		count = rng.NextInt(MinimumNodes, Math.Min(MaximumNodes, candidates.Count) + 1);
		return candidates;
	}
}
