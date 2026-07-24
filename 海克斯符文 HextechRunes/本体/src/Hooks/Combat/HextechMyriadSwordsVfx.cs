using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;

namespace HextechRunes;

/// <summary>
/// 万剑归宗：复用原版刀扇的九槽扇形展开、淡入淡出和音效，只将每个槽位里的
/// 小刀视觉替换为原版君王之剑场景中的 SpineSword。
/// </summary>
internal static class HextechMyriadSwordsVfx
{
	private const string FanScenePath = "res://scenes/vfx/fan_of_knives_vfx.tscn";
	private const string SovereignBladeScenePath = "res://scenes/vfx/sovereign_blade.tscn";
	private const float BladeLift = 180f;
	private const float BladeScale = 0.55f;

	private static readonly FieldInfo? FanSpawnPositionField = typeof(NFanOfKnivesVfx)
		.GetField("_spawnPosition", BindingFlags.Instance | BindingFlags.NonPublic);

	internal static void Play(Creature owner)
	{
		if (owner.IsDead)
		{
			return;
		}

		Callable.From(() => PlayDeferred(owner)).CallDeferred();
	}

	private static void PlayDeferred(Creature owner)
	{
		try
		{
			NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
			Node? parent = owner.GetBackVfxContainer();
			PackedScene? fanScene = LoadScene(FanScenePath);
			if (owner.IsDead
				|| !GodotObject.IsInstanceValid(ownerNode)
				|| !GodotObject.IsInstanceValid(parent)
				|| fanScene == null
				|| FanSpawnPositionField == null)
			{
				return;
			}

			NFanOfKnivesVfx fan = fanScene.Instantiate<NFanOfKnivesVfx>();
			FanSpawnPositionField.SetValue(fan, ownerNode!.VfxSpawnPosition);

			List<Node2D> blades = [];
			for (int i = 1; i <= 9; i++)
			{
				Node2D? slot = fan.GetNodeOrNull<Node2D>($"ShivFanParticle{i}");
				Node2D? blade = CreateSovereignBladeVisual();
				if (slot == null || blade == null)
				{
					blade?.Free();
					continue;
				}

				HideVanillaShiv(slot);
				blade.Name = $"MyriadSovereignBlade{i}";
				blade.Position = Vector2.Zero;
				blade.Rotation = 0f;
				blade.Scale = Vector2.One * BladeScale;
				// fan 尚未入树，此处直接挂载，确保原版 _Ready 扫描九个槽位前替换已完成。
				slot.AddChild(blade);
				blades.Add(blade);
			}

			if (blades.Count == 0)
			{
				fan.Free();
				return;
			}

			parent!.AddChildSafely(fan);
			AnimateReplacementBlades(fan, blades);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MyriadSwordsVfx] Could not play Sovereign Blade fan: {ex.Message}", 2);
		}
	}

	private static Node2D? CreateSovereignBladeVisual()
	{
		PackedScene? scene = LoadScene(SovereignBladeScenePath);
		if (scene == null)
		{
			return null;
		}

		Node root = scene.Instantiate();
		Node2D? blade = root.GetNodeOrNull<Node2D>("SpineSword");
		if (blade == null)
		{
			root.Free();
			return null;
		}

		root.RemoveChild(blade);
		root.Free();
		DisableInteractiveAndTransientChildren(blade);
		return blade;
	}

	private static void HideVanillaShiv(Node2D slot)
	{
		switch (slot)
		{
			case Sprite2D sprite:
				sprite.Texture = null;
				break;
			case AnimatedSprite2D animated:
				animated.SpriteFrames = new SpriteFrames();
				break;
		}

		foreach (Node child in slot.GetChildren())
		{
			if (child is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
			}
		}
	}

	private static void DisableInteractiveAndTransientChildren(Node node)
	{
		if (node is Control control)
		{
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
		}

		if (node is GpuParticles2D particles)
		{
			particles.Emitting = false;
		}

		foreach (Node child in node.GetChildren())
		{
			DisableInteractiveAndTransientChildren(child);
		}
	}

	private static void AnimateReplacementBlades(NFanOfKnivesVfx fan, IReadOnlyList<Node2D> blades)
	{
		Tween tween = fan.CreateTween().SetParallel();
		for (int i = 0; i < blades.Count; i++)
		{
			Node2D blade = blades[i];
			double duration = 0.4 + i % 3 * 0.16;
			tween.TweenProperty(blade, "position:y", -BladeLift, duration)
				.From(0f)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back);
			tween.TweenProperty(blade, "scale", Vector2.One * BladeScale, duration)
				.From(Vector2.One * (BladeScale * 0.72f))
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Back);
		}
	}

	private static PackedScene? LoadScene(string path)
	{
		try
		{
			if (PreloadManager.Cache.ContainsKey(path))
			{
				return PreloadManager.Cache.GetScene(path);
			}
		}
		catch
		{
			// 当前角色未预载静默刀扇资源时，回退到 ResourceLoader。
		}

		return ResourceLoader.Load<PackedScene>(path, cacheMode: ResourceLoader.CacheMode.Reuse);
	}
}
