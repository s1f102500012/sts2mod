using MegaCrit.Sts2.Core.Map;

namespace AITeammate.Scripts;

internal sealed class AiTeammateTestActMap : ActMap
{
    public const int EventChainColumn = 6;
    public const int FakeMerchantColumn = 9;
    public const int BranchRow = 1;
    public const int EventChainStartRow = 1;
    public const int EventChainMiddleRow = 2;
    public const int EventChainEndRow = 3;

    private const int GridWidth = 10;
    private const int GridHeight = 4;
    private const int StartColumn = 4;
    private const int MonsterColumn = 1;
    private const int EliteColumn = 2;
    private const int TreasureColumn = 3;
    private const int ShopColumn = 5;
    private const int SharedRestColumn = 4;

    protected override MapPoint?[,] Grid { get; }

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    public AiTeammateTestActMap(int actIndex)
    {
        Grid = new MapPoint[GridWidth, GridHeight];
        StartingMapPoint = CreateSpecialPoint(StartColumn, 0, MapPointType.Ancient);
        BossMapPoint = CreateSpecialPoint(SharedRestColumn, GridHeight, MapPointType.Boss);

        if (actIndex > 0)
        {
            MapPoint quickRestSite = CreatePathPoint(SharedRestColumn, 1, MapPointType.RestSite);
            StartingMapPoint.AddChildPoint(quickRestSite);
            quickRestSite.AddChildPoint(BossMapPoint);
            startMapPoints.Add(quickRestSite);
            return;
        }

        MapPoint smallMonster = CreatePathPoint(MonsterColumn, 1, MapPointType.Monster);
        MapPoint elite = CreatePathPoint(EliteColumn, 1, MapPointType.Elite);
        MapPoint treasure = CreatePathPoint(TreasureColumn, 1, MapPointType.Treasure);
        MapPoint shop = CreatePathPoint(ShopColumn, 1, MapPointType.Shop);
        MapPoint eventChainStart = CreatePathPoint(EventChainColumn, EventChainStartRow, MapPointType.Unknown);
        MapPoint eventChainMiddle = CreatePathPoint(EventChainColumn, EventChainMiddleRow, MapPointType.Unknown);
        MapPoint eventChainEnd = CreatePathPoint(EventChainColumn, EventChainEndRow, MapPointType.Unknown);
        MapPoint fakeMerchant = CreatePathPoint(FakeMerchantColumn, BranchRow, MapPointType.Unknown);
        MapPoint restSite = CreatePathPoint(SharedRestColumn, 2, MapPointType.RestSite);
        MapPoint followUpMonster = CreatePathPoint(SharedRestColumn, 3, MapPointType.Monster);

        StartingMapPoint.AddChildPoint(smallMonster);
        StartingMapPoint.AddChildPoint(elite);
        StartingMapPoint.AddChildPoint(treasure);
        StartingMapPoint.AddChildPoint(shop);
        StartingMapPoint.AddChildPoint(eventChainStart);
        StartingMapPoint.AddChildPoint(fakeMerchant);

        smallMonster.AddChildPoint(restSite);
        elite.AddChildPoint(restSite);
        treasure.AddChildPoint(restSite);
        shop.AddChildPoint(restSite);
        eventChainStart.AddChildPoint(eventChainMiddle);
        eventChainMiddle.AddChildPoint(eventChainEnd);
        eventChainEnd.AddChildPoint(BossMapPoint);
        fakeMerchant.AddChildPoint(restSite);
        restSite.AddChildPoint(followUpMonster);
        followUpMonster.AddChildPoint(BossMapPoint);

        startMapPoints.Add(smallMonster);
        startMapPoints.Add(elite);
        startMapPoints.Add(treasure);
        startMapPoints.Add(shop);
        startMapPoints.Add(eventChainStart);
        startMapPoints.Add(fakeMerchant);
    }

    public static bool IsAromaOfChaosCoord(MapCoord? coord)
    {
        return coord is { col: EventChainColumn, row: EventChainStartRow };
    }

    public static bool IsDrowningBeaconCoord(MapCoord? coord)
    {
        return coord is { col: EventChainColumn, row: EventChainMiddleRow };
    }

    public static bool IsWellspringCoord(MapCoord? coord)
    {
        return coord is { col: EventChainColumn, row: EventChainEndRow };
    }

    public static bool IsFakeMerchantCoord(MapCoord? coord)
    {
        return coord is { col: FakeMerchantColumn, row: BranchRow };
    }

    private MapPoint CreatePathPoint(int col, int row, MapPointType pointType)
    {
        MapPoint point = new(col, row)
        {
            PointType = pointType,
            CanBeModified = false
        };
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
