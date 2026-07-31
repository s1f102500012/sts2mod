using MegaCrit.Sts2.Core.Map;

namespace HextechRunes;

internal static class HextechMapLengthReducer
{
	private static readonly System.Reflection.MethodInfo? SetSpoilsCoordMethod =
		typeof(SpoilsMap).GetProperty(nameof(SpoilsMap.SpoilsCoord))?.GetSetMethod(nonPublic: true);

	internal static ActMap ReduceNodeLength(IRunState runState, ActMap map, MapCoord? currentCoord, int rowsToRemove)
	{
		if (rowsToRemove <= 0 || IsSpecialMap(map))
		{
			return map;
		}

		ActMap modifiedMap = map;
		int appliedRows = 0;
		for (int i = 0; i < rowsToRemove; i++)
		{
			int searchStartRow = currentCoord.HasValue ? currentCoord.Value.row + 2 : 1;
			if (!TryFindSafeRowToRemove(modifiedMap, searchStartRow, out int rowToRemove))
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Hasty Scribble map shrink skipped: no safe removable row. map={modifiedMap.GetType().Name} rows={modifiedMap.GetRowCount()} current={DescribeCoord(currentCoord)} requested={rowsToRemove} applied={appliedRows}");
				break;
			}

			modifiedMap = new ShortenedActMap(modifiedMap, rowToRemove);
			AdjustSpoilsMapCoords(runState, modifiedMap, rowToRemove);
			appliedRows++;
		}

		return modifiedMap;
	}

	private static bool IsSpecialMap(ActMap map)
	{
		return map.GetType().Name == "GoldenPathActMap";
	}

	private static bool TryFindSafeRowToRemove(ActMap map, int searchStartRow, out int rowToRemove)
	{
		int rowCount = map.GetRowCount();
		for (int row = Math.Max(1, searchStartRow); row < rowCount - 1; row++)
		{
			if (IsSafeRowToRemove(map, row))
			{
				rowToRemove = row;
				return true;
			}
		}

		rowToRemove = -1;
		return false;
	}

	private static bool IsSafeRowToRemove(ActMap map, int row)
	{
		List<MapPoint> rowPoints = map.GetPointsInRow(row).ToList();
		return rowPoints.Count > 0
			&& rowPoints.All(IsSafePointToRemove);
	}

	private static bool IsSafePointToRemove(MapPoint point)
	{
		return point.CanBeModified
			&& point.Quests.Count == 0
			&& point.PointType is MapPointType.Unknown or MapPointType.Monster;
	}

	private static string DescribeCoord(MapCoord? coord)
	{
		return coord.HasValue ? $"{coord.Value.col},{coord.Value.row}" : "none";
	}

	private static void AdjustSpoilsMapCoords(IRunState runState, ActMap shortenedMap, int removedRow)
	{
		foreach (SpoilsMap spoilsMap in runState.Players
			.SelectMany(static player => player.Deck.Cards)
			.OfType<SpoilsMap>())
		{
			if (spoilsMap.SpoilsActIndex != runState.CurrentActIndex || !spoilsMap.SpoilsCoord.HasValue)
			{
				continue;
			}

			MapCoord oldCoord = spoilsMap.SpoilsCoord.Value;
			MapCoord newCoord = oldCoord.row > removedRow
				? new MapCoord(oldCoord.col, oldCoord.row - 1)
				: oldCoord;
			if (newCoord == oldCoord || !shortenedMap.HasPoint(newCoord))
			{
				continue;
			}

			SetSpoilsCoord(spoilsMap, newCoord);
		}
	}

	private static void SetSpoilsCoord(SpoilsMap spoilsMap, MapCoord coord)
	{
		if (SetSpoilsCoordMethod == null)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Hasty Scribble could not update SpoilsMap coord: setter missing.");
			return;
		}

		try
		{
			SetSpoilsCoordMethod.Invoke(spoilsMap, [coord]);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Hasty Scribble failed to update SpoilsMap coord: {ex.Message}");
		}
	}

	private sealed class ShortenedActMap : ActMap
	{
		public override MapPoint BossMapPoint { get; }

		public override MapPoint StartingMapPoint { get; }

		public override MapPoint? SecondBossMapPoint { get; }

		protected override MapPoint?[,] Grid { get; }

		private readonly int _rowToRemove;

		public ShortenedActMap(ActMap original, int rowToRemove)
		{
			_rowToRemove = rowToRemove;
			Grid = new MapPoint[original.GetColumnCount(), original.GetRowCount() - 1];
			var cloneLookup = new Dictionary<MapCoord, MapPoint>();

			foreach (MapPoint originalPoint in original.GetAllMapPoints())
			{
				if (originalPoint.coord.row == _rowToRemove)
				{
					continue;
				}

				MapPoint clone = ClonePoint(originalPoint, ShiftedRow(originalPoint.coord.row));
				Grid[clone.coord.col, clone.coord.row] = clone;
				cloneLookup[originalPoint.coord] = clone;
			}

			StartingMapPoint = ClonePoint(original.StartingMapPoint, original.StartingMapPoint.coord.row);
			BossMapPoint = ClonePoint(original.BossMapPoint, ShiftedRow(original.BossMapPoint.coord.row));
			SecondBossMapPoint = original.SecondBossMapPoint == null
				? null
				: ClonePoint(original.SecondBossMapPoint, ShiftedRow(original.SecondBossMapPoint.coord.row));

			foreach (MapPoint originalPoint in original.GetAllMapPoints())
			{
				if (!cloneLookup.TryGetValue(originalPoint.coord, out MapPoint? clonedPoint))
				{
					continue;
				}

				foreach (MapPoint child in ResolveChildTargets(original, cloneLookup, originalPoint.Children))
				{
					AddChildOnce(clonedPoint, child);
				}
			}

			foreach (MapPoint child in ResolveChildTargets(original, cloneLookup, original.StartingMapPoint.Children))
			{
				AddChildOnce(StartingMapPoint, child);
				if (!startMapPoints.Contains(child))
				{
					startMapPoints.Add(child);
				}
			}

			if (original.SecondBossMapPoint != null && SecondBossMapPoint != null)
			{
				AddChildOnce(BossMapPoint, SecondBossMapPoint);
			}
		}

		private IEnumerable<MapPoint> ResolveChildTargets(ActMap original, Dictionary<MapCoord, MapPoint> cloneLookup, IEnumerable<MapPoint> children)
		{
			foreach (MapPoint child in children)
			{
				if (child == original.BossMapPoint)
				{
					yield return BossMapPoint;
					continue;
				}

				if (child == original.SecondBossMapPoint)
				{
					if (SecondBossMapPoint != null)
					{
						yield return SecondBossMapPoint;
					}
					continue;
				}

				if (child.coord.row == _rowToRemove)
				{
					foreach (MapPoint grandChild in ResolveChildTargets(original, cloneLookup, child.Children))
					{
						yield return grandChild;
					}
					continue;
				}

				if (cloneLookup.TryGetValue(child.coord, out MapPoint? clonedChild))
				{
					yield return clonedChild;
				}
			}
		}

		private int ShiftedRow(int row)
		{
			return row > _rowToRemove ? row - 1 : row;
		}

		private static MapPoint ClonePoint(MapPoint original, int row)
		{
			var clone = new MapPoint(original.coord.col, row)
			{
				PointType = original.PointType,
				CanBeModified = original.CanBeModified
			};
			foreach (var quest in original.Quests)
			{
				clone.AddQuest(quest);
			}

			return clone;
		}

		private static void AddChildOnce(MapPoint parent, MapPoint child)
		{
			if (!parent.Children.Contains(child))
			{
				parent.AddChildPoint(child);
			}
		}
	}
}
