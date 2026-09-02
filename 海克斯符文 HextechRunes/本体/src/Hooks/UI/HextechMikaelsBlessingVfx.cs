using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal static class HextechMikaelsBlessingVfx
{
	private const float Lifetime = 1.08f;
	private const float MinWidth = 175f;
	private const float MaxWidth = 350f;
	private const float WidthMultiplier = 0.98f;
	private const float HeightRatio = 0.36f;
	private static readonly Vector2 GroundOffset = new(0f, -18f);
	private static readonly HashSet<string> LoggedMissingTexturePaths = [];

	internal static void Play(Creature? creature)
	{
		try
		{
			if (creature == null || creature.IsDead)
			{
				return;
			}

			NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
			if (!GodotObject.IsInstanceValid(creatureNode)
				|| !creatureNode.IsNodeReady()
				|| creatureNode.Hitbox == null)
			{
				return;
			}

			Node2D? renderLayer = HextechBehindCreaturesLayer.GetOrCreate(creatureNode.GetParent());
			if (renderLayer == null)
			{
				return;
			}

			MikaelsBlessingBurstVisual visual = new(creatureNode, renderLayer);
			if (visual.Start())
			{
				TaskHelper.RunSafely(visual.RunAsync());
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][MikaelsBlessingVfx] Could not play cleansing burst: {ex.Message}");
		}
	}

	private sealed class MikaelsBlessingBurstVisual
	{
		private readonly NCreature _creature;
		private readonly Node2D _renderLayer;
		private Node2D? _root;
		private BurstLayer? _runeLayer;
		private BurstLayer? _glowLayer;
		private BurstLayer? _ringLayer;
		private BurstLayer? _flashLayer;
		private BurstLayer? _waveLayer;
		private float _elapsed;

		internal MikaelsBlessingBurstVisual(NCreature creature, Node2D renderLayer)
		{
			_creature = creature;
			_renderLayer = renderLayer;
		}

		internal bool Start()
		{
			Texture2D? runeTexture = LoadTextureOrWarn(HextechAssets.MikaelsBlessingAoeRunePath);
			Texture2D? discTexture = LoadTextureOrWarn(HextechAssets.HandOfBaronAuraDiscPath);
			Texture2D? ringTexture = LoadTextureOrWarn(HextechAssets.HandOfBaronAuraRingPath);
			if (runeTexture == null || discTexture == null || ringTexture == null)
			{
				return false;
			}

			_root = new Node2D
			{
				Name = "HextechRunes_MikaelsBlessingBurst",
				Visible = true,
				ShowBehindParent = false,
				TopLevel = false,
				ZAsRelative = true,
				ZIndex = 0
			};
			_renderLayer.AddChildSafely(_root);
			EnsureRenderOrder();
			UpdatePosition();

			_runeLayer = CreateLayer(_root, "MilioGroundRune", runeTexture, new Color(0.18f, 1f, 0.55f, 0.44f), additive: true);
			_glowLayer = CreateLayer(_root, "EmeraldCleanseBloom", discTexture, new Color(0.02f, 0.95f, 0.70f, 0.38f), additive: true);
			_ringLayer = CreateLayer(_root, "CleansingRing", ringTexture, new Color(0.38f, 1f, 0.72f, 0.70f), additive: true);
			_flashLayer = CreateLayer(_root, "WhiteGreenBurst", discTexture, new Color(0.86f, 1f, 0.88f, 0.78f), additive: true);
			_waveLayer = CreateLayer(_root, "OuterEmeraldWave", ringTexture, new Color(0.30f, 1f, 0.74f, 0.36f), additive: true);
			UpdateTransform();
			HextechLog.Info($"[{ModInfo.Id}][MikaelsBlessingVfx] Burst attached node={_root.GetPath()} parent={_renderLayer.GetPath()} creature={_creature.Entity?.ModelId.Entry ?? "<unknown>"}.");
			return true;
		}

		internal async Task RunAsync()
		{
			try
			{
				while (_elapsed < Lifetime
					&& GodotObject.IsInstanceValid(_creature)
					&& GodotObject.IsInstanceValid(_root))
				{
					float dt = Mathf.Min(Mathf.Max((float)_root.GetProcessDeltaTime(), 1f / 120f), 0.05f);
					_elapsed += dt;
					EnsureRenderOrder();
					UpdatePosition();
					Animate();

					SceneTree tree = _root.GetTree();
					if (!GodotObject.IsInstanceValid(tree))
					{
						return;
					}

					await _root.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][MikaelsBlessingVfx] Cleansing burst stopped after runtime error: {ex.Message}");
			}
			finally
			{
				if (GodotObject.IsInstanceValid(_root))
				{
					_root.QueueFree();
				}
			}
		}

		private void EnsureRenderOrder()
		{
			HextechBehindCreaturesLayer.EnsureRenderOrder(_renderLayer);
		}

		private void UpdatePosition()
		{
			if (_root != null)
			{
				_root.GlobalPosition = _creature.GetBottomOfHitbox() + GroundOffset;
			}
		}

		private void UpdateTransform()
		{
			float width = Mathf.Clamp(_creature.Hitbox.Size.X * WidthMultiplier, MinWidth, MaxWidth);
			float height = width * HeightRatio;
			ScaleLayer(_runeLayer, width * 1.12f, height * 1.05f);
			ScaleLayer(_glowLayer, width * 1.28f, height * 1.16f);
			ScaleLayer(_ringLayer, width * 0.92f, height * 0.90f);
			ScaleLayer(_flashLayer, width * 0.62f, height * 0.58f);
			ScaleLayer(_waveLayer, width * 1.38f, height * 1.18f);
		}

		private void Animate()
		{
			float t = Mathf.Clamp(_elapsed / Lifetime, 0f, 1f);
			float intro = SmoothStep(0f, 0.07f, t);
			float fade = 1f - SmoothStep(0.64f, 1f, t);
			float burst = EaseOutCubic(Mathf.Clamp(t / 0.42f, 0f, 1f));
			float shockwave = EaseOutCubic(t);
			float flash = intro * (1f - SmoothStep(0.08f, 0.30f, t));
			float alpha = intro * fade;
			float width = Mathf.Clamp(_creature.Hitbox.Size.X * WidthMultiplier, MinWidth, MaxWidth);
			float height = width * HeightRatio;

			ScaleLayer(_runeLayer, width * (0.34f + burst * 0.98f), height * (0.32f + burst * 0.92f));
			ScaleLayer(_glowLayer, width * (0.30f + burst * 1.18f), height * (0.28f + burst * 1.02f));
			ScaleLayer(_ringLayer, width * (0.42f + burst * 0.78f), height * (0.40f + burst * 0.70f));
			ScaleLayer(_flashLayer, width * (0.14f + burst * 0.56f), height * (0.12f + burst * 0.50f));
			ScaleLayer(_waveLayer, width * (0.48f + shockwave * 1.22f), height * (0.44f + shockwave * 1.02f));

			if (_runeLayer != null)
			{
				_runeLayer.Sprite.Rotation = 0f;
				_runeLayer.Sprite.Modulate = new Color(0.10f, 1f, 0.48f, 0.48f * alpha);
			}

			if (_glowLayer != null)
			{
				_glowLayer.Sprite.Rotation = 0f;
				_glowLayer.Sprite.Modulate = new Color(0.00f, 0.96f, 0.78f, 0.40f * alpha);
			}

			if (_ringLayer != null)
			{
				_ringLayer.Sprite.Rotation = 0f;
				_ringLayer.Sprite.Modulate = new Color(0.44f, 1f, 0.76f, 0.48f * alpha);
			}

			if (_flashLayer != null)
			{
				_flashLayer.Sprite.Rotation = 0f;
				_flashLayer.Sprite.Modulate = new Color(0.86f, 1f, 0.86f, 0.72f * flash);
			}

			if (_waveLayer != null)
			{
				_waveLayer.Sprite.Rotation = 0f;
				_waveLayer.Sprite.Modulate = new Color(0.28f, 1f, 0.72f, 0.34f * (1f - shockwave) * alpha);
			}
		}

		}

	private static BurstLayer CreateLayer(Node2D parent, string name, Texture2D texture, Color modulate, bool additive)
	{
		Node2D plane = new()
		{
			Name = name,
			ZIndex = 0,
			ZAsRelative = true
		};
		parent.AddChildSafely(plane);

		Sprite2D sprite = new()
		{
			Name = "Texture",
			Texture = texture,
			Centered = true,
			Modulate = modulate
		};
		if (additive)
		{
			sprite.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}

		plane.AddChildSafely(sprite);
		return new BurstLayer(plane, sprite);
	}

	private static Texture2D? LoadTextureOrWarn(string path)
	{
		Texture2D? texture = HextechTextures.LoadUiTexture(path);
		if (texture == null && LoggedMissingTexturePaths.Add(path))
		{
			Log.Warn($"[{ModInfo.Id}][MikaelsBlessingVfx] Texture not found: {path}");
		}

		return texture;
	}

	private static void ScaleLayer(BurstLayer? layer, float width, float height)
	{
		Texture2D? texture = layer?.Sprite.Texture;
		if (layer == null || texture == null)
		{
			return;
		}

		layer.Plane.Scale = new Vector2(width / Math.Max(texture.GetWidth(), 1), height / Math.Max(texture.GetHeight(), 1));
	}

	private static float EaseOutCubic(float value)
	{
		float inverse = 1f - value;
		return 1f - inverse * inverse * inverse;
	}

	private static float SmoothStep(float edge0, float edge1, float value)
	{
		if (edge0 >= edge1)
		{
			return value < edge0 ? 0f : 1f;
		}

		float t = Mathf.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private sealed record BurstLayer(Node2D Plane, Sprite2D Sprite);
}
