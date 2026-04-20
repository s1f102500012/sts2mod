using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class Act4MapHooks
{
	private static Hook? _setMapHook;

	private static Hook? _openHook;

	private static readonly FieldInfo MapPointDictionaryField = RequireField(typeof(NMapScreen), "_mapPointDictionary");
	private static readonly FieldInfo StartingPointNodeField = RequireField(typeof(NMapScreen), "_startingPointNode");
	private static readonly FieldInfo BossPointNodeField = RequireField(typeof(NMapScreen), "_bossPointNode");
	private static readonly FieldInfo MapContainerField = RequireField(typeof(NMapScreen), "_mapContainer");
	private static readonly FieldInfo PointsField = RequireField(typeof(NMapScreen), "_points");
	private static readonly FieldInfo BackstopField = RequireField(typeof(NMapScreen), "_backstop");
	private static readonly FieldInfo TargetDragPosField = RequireField(typeof(NMapScreen), "_targetDragPos");
	private static readonly FieldInfo RunStateField = RequireField(typeof(NMapScreen), "_runState");
	private static readonly FieldInfo DistYField = RequireField(typeof(NMapScreen), "_distY");
	private static readonly FieldInfo PathsField = RequireField(typeof(NMapScreen), "_paths");
	private static readonly FieldInfo PathsContainerField = RequireField(typeof(NMapScreen), "_pathsContainer");
	private static readonly FieldInfo NormalOutlineField = RequireField(typeof(NNormalMapPoint), "_outline");
	private static readonly FieldInfo BossPlaceholderOutlineField = RequireField(typeof(NBossMapPoint), "_placeholderOutline");

	private delegate void OrigSetMap(NMapScreen self, ActMap map, uint seed, bool clearDrawings);

	private delegate NMapScreen OrigOpen(NMapScreen self, bool isOpenedFromTopBar);

	public static void Install()
	{
		MethodInfo setMap = typeof(NMapScreen).GetMethod(nameof(NMapScreen.SetMap), BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Could not find NMapScreen.SetMap.");
		MethodInfo open = typeof(NMapScreen).GetMethod(nameof(NMapScreen.Open), BindingFlags.Instance | BindingFlags.Public, new[] { typeof(bool) })
			?? throw new InvalidOperationException("Could not find NMapScreen.Open(bool).");
		_setMapHook = new Hook(setMap, SetMapDetour);
		_openHook = new Hook(open, OpenDetour);
	}

	private static void SetMapDetour(OrigSetMap orig, NMapScreen self, ActMap map, uint seed, bool clearDrawings)
	{
		orig(self, map, seed, clearDrawings);

		bool isAct4 = map is FixedAct4Map;
		if (!isAct4 && RunStateField.GetValue(self) is RunState runState
			&& runState.Act.Id == ModelDb.GetId<Sts1Act4>())
		{
			isAct4 = true;
		}

		if (!isAct4)
		{
			return;
		}

		RepositionAct4Map(self, map);
	}

	private static NMapScreen OpenDetour(OrigOpen orig, NMapScreen self, bool isOpenedFromTopBar)
	{
		NMapScreen result = orig(self, isOpenedFromTopBar);
		if (RunStateField.GetValue(self) is not MegaCrit.Sts2.Core.Runs.RunState runState || runState.Act.Id != MegaCrit.Sts2.Core.Models.ModelDb.GetId<Sts1Act4>())
		{
			return result;
		}

		float distY = (float)DistYField.GetValue(self)!;
		int row = (runState.ActFloor == 0 || runState.VisitedMapCoords.Count == 0)
			? 0
			: runState.CurrentMapCoord?.row ?? 0;
		Vector2 target = new(0f, -600f + row * distY);
		((Control)MapContainerField.GetValue(self)!).Position = target;
		TargetDragPosField.SetValue(self, target);

		Control points = (Control)PointsField.GetValue(self)!;
		Color pointsColor = points.Modulate;
		pointsColor.A = 1f;
		points.Modulate = pointsColor;

		Control backstop = (Control)BackstopField.GetValue(self)!;
		Color backstopColor = backstop.Modulate;
		backstopColor.A = 0.85f;
		backstop.Modulate = backstopColor;

		((Control)MapContainerField.GetValue(self)!).Modulate = Colors.White;
		self.RefreshAllPointVisuals();
		return result;
	}

	private static void RepositionAct4Map(NMapScreen screen, ActMap map)
	{
		var pointDictionary = (Dictionary<MapCoord, NMapPoint>)MapPointDictionaryField.GetValue(screen)!;
		var startingNode = (NMapPoint)StartingPointNodeField.GetValue(screen)!;
		var bossNode = (NMapPoint)BossPointNodeField.GetValue(screen)!;
		var paths = (Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>)PathsField.GetValue(screen)!;
		var pathsContainer = (Control)PathsContainerField.GetValue(screen)!;

		// Find the elite point — it is the only interior grid point in the Act4 map
		MapPoint? elitePoint = null;
		foreach (MapPoint pt in map.GetAllMapPoints())
		{
			if (pt.PointType == MapPointType.Elite)
			{
				elitePoint = pt;
				break;
			}
		}

		if (elitePoint == null || !pointDictionary.ContainsKey(elitePoint.coord))
		{
			return;
		}

		float startX = startingNode.Position.X;
		float eliteX = pointDictionary[elitePoint.coord].Position.X;
		float bossX = bossNode.Position.X;

		startingNode.Position = new Vector2(startX, 820f);
		pointDictionary[elitePoint.coord].Position = new Vector2(eliteX, 500f);
		bossNode.Position = new Vector2(bossX, 120f);

		pathsContainer.FreeChildren();
		paths.Clear();
		RedrawPath(screen, pathsContainer, paths, startingNode, pointDictionary[elitePoint.coord]);
		RedrawPath(screen, pathsContainer, paths, pointDictionary[elitePoint.coord], bossNode);

		HideOutline(startingNode);
		HideOutline(pointDictionary[elitePoint.coord]);
		HideOutline(bossNode);
	}

	private static void RedrawPath(NMapScreen screen, Control pathsContainer, Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>> paths, NMapPoint from, NMapPoint to)
	{
		Vector2 start = GetLineEndpoint(from);
		Vector2 end = GetLineEndpoint(to);
		List<TextureRect> ticks = new();
		Vector2 direction = (end - start).Normalized();
		float angle = direction.Angle() + Mathf.Pi / 2f;
		float distance = start.DistanceTo(end);
		int count = (int)(distance / 22f) + 1;
		for (int i = 1; i < count; i++)
		{
			float offset = i * 22f;
			TextureRect tick = MegaCrit.Sts2.Core.Assets.PreloadManager.Cache.GetScene("res://scenes/ui/map_dot.tscn").Instantiate<TextureRect>(PackedScene.GenEditState.Disabled);
			tick.Position = start + direction * offset;
			tick.Position -= new Vector2(screen.Size.X * 0.5f - 20f, screen.Size.Y * 0.5f - 20f);
			pathsContainer.AddChild(tick);
			ticks.Add(tick);
		}

		paths[(from.Point.coord, to.Point.coord)] = ticks;
	}

	private static Vector2 GetLineEndpoint(NMapPoint point)
	{
		return point is NNormalMapPoint ? point.Position : point.Position + point.Size * 0.5f;
	}

	private static void HideOutline(NMapPoint point)
	{
		switch (point)
		{
			case NNormalMapPoint normal:
			{
				if (NormalOutlineField.GetValue(normal) is CanvasItem outline)
				{
					outline.Visible = false;
				}

				break;
			}
			case NBossMapPoint boss:
			{
				if (BossPlaceholderOutlineField.GetValue(boss) is CanvasItem outline)
				{
					outline.Visible = false;
				}

				break;
			}
		}
	}

	private static FieldInfo RequireField(Type type, string name)
	{
		return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"Could not find field {type.FullName}.{name}.");
	}
}
