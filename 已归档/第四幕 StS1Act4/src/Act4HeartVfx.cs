using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace StS1Act4;

internal static class Act4HeartVfx
{
	private const string BuffScenePath = "res://scenes/vfx/sts1_act4_heart_buff_vfx.tscn";

	private const string BloodShotScenePath = "res://scenes/vfx/sts1_act4_heart_blood_shot_vfx.tscn";

	private const string ScreenFlashScenePath = "res://scenes/vfx/sts1_act4_heart_screen_flash_vfx.tscn";

	private const float BloodShotFirstHitDelay = 0.245f;

	private const float BloodShotHitInterval = 0.035f;

	private const float BloodShotFlightDuration = 0.11f;

	private const float BloodShotGrowDuration = 0.055f;

	private const float BloodShotHoldDuration = 0.07f;

	private static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = new("StS1Act4.HeartVfx", MegaCrit.Sts2.Core.Logging.LogType.Generic);

	private static readonly Dictionary<string, PackedScene> SceneCache = new();

	public static void PlayBuff(Creature source)
	{
		if (!TryResolveCombatContext(source, out NCombatRoom room, out NCreature sourceNode))
		{
			return;
		}

		Vector2 center = sourceNode.VfxSpawnPosition + new Vector2(-18f, -8f);
		NPowerUpVfx.CreateNormal(source);
		SpawnScene(room.CombatVfxContainer, BuffScenePath, ("center_position", Variant.From(center)));
		SpawnScene(room, ScreenFlashScenePath, ("is_buff_flash", Variant.From(true)));
		NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
		SfxCmd.Play("event:/sfx/buff");
		Logger.Info($"Spawned heart buff VFX scene at {FormatVector(center)}.");
	}

	public static void PlayBloodShots(Creature source, IReadOnlyList<Creature> targets, int count)
	{
		Logger.Info("Skipped custom blood-shot VFX by configuration; using damage/audio only.");
	}

	private static bool TryResolveCombatContext(Creature source, out NCombatRoom room, out NCreature sourceNode)
	{
		room = NCombatRoom.Instance!;
		sourceNode = null!;
		if (room == null)
		{
			Logger.Warn("Skipped heart VFX because there was no active combat room.");
			return false;
		}

		NCreature? resolvedNode = room.GetCreatureNode(source);
		if (resolvedNode == null)
		{
			Logger.Warn("Skipped heart VFX because the source creature node was missing.");
			return false;
		}

		sourceNode = resolvedNode;
		return true;
	}

	private static void SpawnScene(Node parent, string scenePath, params (string Name, Variant Value)[] properties)
	{
		PackedScene? scene = LoadScene(scenePath);
		if (scene == null)
		{
			return;
		}

		Node node = scene.Instantiate();
		foreach ((string name, Variant value) in properties)
		{
			node.Set(name, value);
		}

		parent.AddChild(node);
		Logger.Info($"Added heart VFX scene {scenePath} to {parent.Name}; inside_tree={node.IsInsideTree()}.");
	}

	private static PackedScene? LoadScene(string scenePath)
	{
		if (SceneCache.TryGetValue(scenePath, out PackedScene? scene))
		{
			return scene;
		}

		PackedScene? loaded = ResourceLoader.Load<PackedScene>(scenePath);
		if (loaded == null)
		{
			Logger.Warn("Failed to load heart VFX scene " + scenePath);
			return null;
		}

		SceneCache[scenePath] = loaded;
		Logger.Info("Loaded heart VFX scene " + scenePath);
		return loaded;
	}

	private static string FormatVector(Vector2 vector)
	{
		return $"({vector.X:F1}, {vector.Y:F1})";
	}
}
