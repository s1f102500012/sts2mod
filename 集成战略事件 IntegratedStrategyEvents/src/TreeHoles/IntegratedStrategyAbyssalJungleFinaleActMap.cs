using MegaCrit.Sts2.Core.Map;

namespace IntegratedStrategyEvents.TreeHoles;

internal sealed class IntegratedStrategyAbyssalJungleFinaleActMap : ActMap, IIntegratedStrategyTemporaryActMap
{
	private const int GridWidth = 7;
	private const int GridRows = 4;
	private const int CenterColumn = 3;
	private const int StartRow = 0;
	private const int ShopRow = 1;
	private const int EventRow = 2;
	private const int RestSiteRow = 3;
	private const int BossRow = GridRows;

	protected override MapPoint?[,] Grid { get; }

	public override MapPoint BossMapPoint { get; }

	public MapPoint EventMapPoint { get; }

	public override MapPoint StartingMapPoint { get; }

	public IntegratedStrategyAbyssalJungleFinaleActMap()
	{
		Grid = new MapPoint[GridWidth, GridRows];
		StartingMapPoint = CreateSpecialPoint(CenterColumn, StartRow, MapPointType.Ancient);
		MapPoint shopPoint = CreateGridPoint(CenterColumn, ShopRow, MapPointType.Shop);
		EventMapPoint = CreateGridPoint(CenterColumn, EventRow, MapPointType.Unknown);
		MapPoint restSitePoint = CreateGridPoint(CenterColumn, RestSiteRow, MapPointType.RestSite);
		BossMapPoint = CreateSpecialPoint(CenterColumn, BossRow, MapPointType.Boss);

		StartingMapPoint.AddChildPoint(shopPoint);
		shopPoint.AddChildPoint(EventMapPoint);
		EventMapPoint.AddChildPoint(restSitePoint);
		restSitePoint.AddChildPoint(BossMapPoint);
	}

	public static bool IsEventCoord(MapCoord? coord)
	{
		return coord is { col: CenterColumn, row: EventRow };
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
