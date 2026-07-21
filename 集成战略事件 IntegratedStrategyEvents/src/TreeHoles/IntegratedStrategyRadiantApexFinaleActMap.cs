using MegaCrit.Sts2.Core.Map;

namespace IntegratedStrategyEvents.TreeHoles;

internal class IntegratedStrategyRadiantApexFinaleActMap : ActMap, IIntegratedStrategyTemporaryActMap
{
	private const int GridWidth = 7;
	private const int GridRows = 7;
	private const int CenterColumn = 3;
	private const int StartRow = 0;
	private const int ShopRow = 1;
	private const int FirstEventRow = 2;
	private const int FirstCombatRow = 3;
	private const int SecondEventRow = 4;
	private const int SecondCombatRow = 5;
	private const int RestSiteRow = 6;
	private const int BossRow = GridRows;

	protected override MapPoint?[,] Grid { get; }

	public override MapPoint BossMapPoint { get; }

	public MapPoint FirstCombatMapPoint { get; }

	public MapPoint SecondCombatMapPoint { get; }

	public override MapPoint StartingMapPoint { get; }

	public IntegratedStrategyRadiantApexFinaleActMap(
		MapPointType firstCombatPointType,
		MapPointType secondCombatPointType)
	{
		Grid = new MapPoint[GridWidth, GridRows];
		StartingMapPoint = CreateSpecialPoint(CenterColumn, StartRow, MapPointType.Ancient);
		MapPoint shopPoint = CreateGridPoint(CenterColumn, ShopRow, MapPointType.Shop);
		MapPoint firstEventPoint = CreateGridPoint(CenterColumn, FirstEventRow, MapPointType.Unknown);
		FirstCombatMapPoint = CreateGridPoint(CenterColumn, FirstCombatRow, NormalizeCombatPointType(firstCombatPointType));
		MapPoint secondEventPoint = CreateGridPoint(CenterColumn, SecondEventRow, MapPointType.Unknown);
		SecondCombatMapPoint = CreateGridPoint(CenterColumn, SecondCombatRow, NormalizeCombatPointType(secondCombatPointType));
		MapPoint restSitePoint = CreateGridPoint(CenterColumn, RestSiteRow, MapPointType.RestSite);
		BossMapPoint = CreateSpecialPoint(CenterColumn, BossRow, MapPointType.Boss);

		StartingMapPoint.AddChildPoint(shopPoint);
		shopPoint.AddChildPoint(firstEventPoint);
		firstEventPoint.AddChildPoint(FirstCombatMapPoint);
		FirstCombatMapPoint.AddChildPoint(secondEventPoint);
		secondEventPoint.AddChildPoint(SecondCombatMapPoint);
		SecondCombatMapPoint.AddChildPoint(restSitePoint);
		restSitePoint.AddChildPoint(BossMapPoint);
	}

	public static bool IsCombatCoord(MapCoord? coord)
	{
		return coord is
			{ col: CenterColumn, row: FirstCombatRow or SecondCombatRow };
	}

	private MapPoint CreateGridPoint(int col, int row, MapPointType pointType)
	{
		MapPoint point = CreateSpecialPoint(col, row, pointType);
		Grid[col, row] = point;
		return point;
	}

	private static MapPointType NormalizeCombatPointType(MapPointType pointType)
	{
		return pointType == MapPointType.Elite ? MapPointType.Elite : MapPointType.Monster;
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
