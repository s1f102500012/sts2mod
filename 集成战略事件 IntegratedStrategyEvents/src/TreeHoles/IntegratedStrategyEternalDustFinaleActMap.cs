using MegaCrit.Sts2.Core.Map;

namespace IntegratedStrategyEvents.TreeHoles;

internal sealed class IntegratedStrategyEternalDustFinaleActMap : ActMap, IIntegratedStrategyTemporaryActMap
{
	private const int GridWidth = 7;
	private const int GridRows = 4;
	private const int CenterColumn = 3;
	private const int StartRow = 0;
	private const int ShopRow = 1;
	private const int FirstEventRow = 2;
	private const int SecondEventRow = 3;
	private const int BossRow = GridRows;

	protected override MapPoint?[,] Grid { get; }

	public override MapPoint BossMapPoint { get; }

	public MapPoint FirstEventMapPoint { get; }

	public MapPoint SecondEventMapPoint { get; }

	public override MapPoint StartingMapPoint { get; }

	public IntegratedStrategyEternalDustFinaleActMap()
	{
		Grid = new MapPoint[GridWidth, GridRows];
		StartingMapPoint = CreateSpecialPoint(CenterColumn, StartRow, MapPointType.Ancient);
		MapPoint shopPoint = CreateGridPoint(CenterColumn, ShopRow, MapPointType.Shop);
		FirstEventMapPoint = CreateGridPoint(CenterColumn, FirstEventRow, MapPointType.Unknown);
		SecondEventMapPoint = CreateGridPoint(CenterColumn, SecondEventRow, MapPointType.Unknown);
		BossMapPoint = CreateSpecialPoint(CenterColumn, BossRow, MapPointType.Boss);

		StartingMapPoint.AddChildPoint(shopPoint);
		shopPoint.AddChildPoint(FirstEventMapPoint);
		FirstEventMapPoint.AddChildPoint(SecondEventMapPoint);
		SecondEventMapPoint.AddChildPoint(BossMapPoint);
	}

	public static bool IsFirstEventCoord(MapCoord? coord)
	{
		return coord is { col: CenterColumn, row: FirstEventRow };
	}

	public static bool IsSecondEventCoord(MapCoord? coord)
	{
		return coord is { col: CenterColumn, row: SecondEventRow };
	}

	private MapPoint CreateGridPoint(int col, int row, MapPointType pointType)
	{
		MapPoint point = CreateSpecialPoint(col, row, pointType);
		Grid[col, row] = point;
		return point;
	}

	private static MapPoint CreateSpecialPoint(int col, int row, MapPointType pointType)
	{
		return new MapPoint(col, row)
		{
			PointType = pointType,
			CanBeModified = false
		};
	}
}
