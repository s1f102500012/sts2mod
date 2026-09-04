using System.Reflection;
using IntegratedStrategyEvents;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;

ExternalMainMap map = new();
Assembly modAssembly = typeof(IntegratedStrategyEventsInterop).Assembly;
SecretNodePlacementProbe.Run(modAssembly);
Type controller = modAssembly.GetType(
	"IntegratedStrategyEvents.Map.IntegratedStrategySecretMapNodeController",
	throwOnError: true)!;
MethodInfo shouldSkipMutation = GetRequiredMethod(controller, "ShouldSkipMapMutation");
MethodInfo shouldSkipReplacement = GetRequiredMethod(controller, "ShouldSkipWholeMapReplacement");

Require(
	!InvokeSkip(shouldSkipMutation, map),
	"external main-map types must remain eligible for secret-node mutation");
Require(
	InvokeSkip(shouldSkipReplacement, map),
	"external map types must remain protected from whole-map replacement");

Type entryCoordinator = modAssembly.GetType(
	"IntegratedStrategyEvents.TreeHoles.TreeHoleEntryCoordinator",
	throwOnError: true)!;
MethodInfo shouldEnterDirectly = GetRequiredMethod(entryCoordinator, "ShouldEnterDirectly");
Require(
	InvokeDirectEntry(shouldEnterDirectly, NetGameType.Singleplayer),
	"singleplayer tree-hole entry must bypass the synchronized action queue");
Require(
	!InvokeDirectEntry(shouldEnterDirectly, NetGameType.Host) &&
	!InvokeDirectEntry(shouldEnterDirectly, NetGameType.Client),
	"multiplayer tree-hole entry must remain synchronized");

IntegratedStrategyEventsInterop.RegisterSecretNodeSkipPredicate(candidate => ReferenceEquals(candidate, map));
Require(
	InvokeSkip(shouldSkipMutation, map),
	"registered third-party skip predicates must exclude their temporary maps");
Require(
	IntegratedStrategyEventsInterop.GetCurrentExtraActId(null!) == null,
	"extra-act interop must ignore missing runs");

MethodInfo classifyExtraAct = GetRequiredMethod(typeof(IntegratedStrategyEventsInterop), "ClassifyExtraAct");
Type finaleKindType = classifyExtraAct.GetParameters()[0].ParameterType;
Require(
	InvokeExtraActClassification(classifyExtraAct, finaleKindType, "EndlessFinale", isCurrentFinaleMap: true) is not null,
	"actual finale maps must be exposed as extra acts");
Require(
	InvokeExtraActClassification(classifyExtraAct, finaleKindType, "ProphetHornFragment", isCurrentFinaleMap: true) == null,
	"the Prophet Horn fragment must not be exposed as an extra act");
Require(
	InvokeExtraActClassification(classifyExtraAct, finaleKindType, "EndlessFinale", isCurrentFinaleMap: false) == null,
	"ordinary tree-hole maps must not be exposed as extra acts");

Type sessionManager = modAssembly.GetType(
	"IntegratedStrategyEvents.TreeHoles.TreeHoleSessionManager",
	throwOnError: true)!;
MethodInfo matchesFinaleMapTopology = GetRequiredMethod(sessionManager, "MatchesFinaleMapTopology");
ExternalMainMap reloadedFinaleMap = new();
Require(
	InvokeMapTopologyMatch(matchesFinaleMapTopology, map, reloadedFinaleMap),
	"reloaded finale maps with matching topology must remain recognizable as extra acts");
Require(
	!InvokeMapTopologyMatch(matchesFinaleMapTopology, map, new ExternalMainMap(startX: 0)),
	"unrelated temporary maps must not match a finale solely by map type");

Console.WriteLine("Map compatibility probe passed.");

static MethodInfo GetRequiredMethod(Type type, string name)
{
	return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
		?? throw new MissingMethodException(type.FullName, name);
}

static bool InvokeSkip(MethodInfo method, ActMap map)
{
	return method.Invoke(null, [null, map]) is true;
}

static bool InvokeDirectEntry(MethodInfo method, NetGameType gameType)
{
	return method.Invoke(null, [gameType]) is true;
}

static string? InvokeExtraActClassification(MethodInfo method, Type finaleKindType, string kind, bool isCurrentFinaleMap)
{
	object finaleKind = Enum.Parse(finaleKindType, kind);
	return method.Invoke(null, [finaleKind, isCurrentFinaleMap]) as string;
}

static bool InvokeMapTopologyMatch(MethodInfo method, ActMap currentMap, ActMap finaleMap)
{
	return method.Invoke(null, [currentMap, finaleMap]) is true;
}

static void Require(bool condition, string message)
{
	if (!condition)
	{
		throw new InvalidOperationException(message);
	}
}

sealed class ExternalMainMap : ActMap
{
	private readonly MapPoint?[,] _grid = new MapPoint[3, 3];
	private readonly MapPoint _startingMapPoint;

	public override MapPoint BossMapPoint { get; } = new(1, 2);

	public override MapPoint StartingMapPoint => _startingMapPoint;

	protected override MapPoint?[,] Grid => _grid;

	public ExternalMainMap(int startX = 1)
	{
		_startingMapPoint = new MapPoint(startX, 0);
		_grid[1, 1] = new MapPoint(1, 1)
		{
			PointType = MapPointType.Unknown,
			CanBeModified = true
		};
	}
}
