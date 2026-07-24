using System.Reflection;
using IntegratedStrategyEvents;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;

ExternalMainMap map = new();
Assembly modAssembly = typeof(IntegratedStrategyEventsInterop).Assembly;
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

	public override MapPoint BossMapPoint { get; } = new(1, 2);

	public override MapPoint StartingMapPoint { get; } = new(1, 0);

	protected override MapPoint?[,] Grid => _grid;

	public ExternalMainMap()
	{
		_grid[1, 1] = new MapPoint(1, 1)
		{
			PointType = MapPointType.Unknown,
			CanBeModified = true
		};
	}
}
