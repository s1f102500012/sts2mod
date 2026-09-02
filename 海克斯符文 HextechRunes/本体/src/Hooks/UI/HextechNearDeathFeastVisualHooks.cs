using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal sealed class HextechNearDeathFeastVisual
{
	private const string NodeName = "HextechRunes_NearDeathFeastAura";
	private const float BodyHeightFactor = 0.42f;
	private const float SurgeIntensityStep = 0.045f;

	// 血色调色板(纹理为白色 alpha,颜色全由 Modulate 给)。
	private static readonly Color GlowColor = new(0.86f, 0.05f, 0.09f);
	private static readonly Color RingColor = new(0.98f, 0.16f, 0.18f);
	private static readonly Color FlashColor = new(1f, 0.32f, 0.30f);

	private static readonly HashSet<ulong> ActiveCreatureNodes = [];
	private static Texture2D? _glowTexture;
	private static Texture2D? _ringTexture;

	private readonly NCreature _creature;
	private Node2D? _root;
	private Node2D? _renderLayer;
	private Sprite2D? _glow;
	private Sprite2D? _ring;
	private float _time;
	private bool _wasActive;
	private float _lastIntensity;

	private HextechNearDeathFeastVisual(NCreature creature)
	{
		_creature = creature;
	}

	internal static void TryAttach(NCreature? creature)
	{
		try
		{
			// 我方(带符文的玩家)与敌方(敌方海克斯激活时的所有敌人)都挂:轮询各自的濒死强度。
			if (!GodotObject.IsInstanceValid(creature)
				|| !creature.IsNodeReady()
				|| creature.Hitbox == null
				|| creature.Entity == null)
			{
				return;
			}

			ulong creatureInstanceId = creature.GetInstanceId();
			if (!ActiveCreatureNodes.Add(creatureInstanceId))
			{
				return;
			}

			HextechNearDeathFeastVisual visual = new(creature);
			if (!visual.Start())
			{
				ActiveCreatureNodes.Remove(creatureInstanceId);
				return;
			}

			TaskHelper.RunSafely(visual.RunAsync(creatureInstanceId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Could not attach Near-Death Feast visual: {ex.Message}");
		}
	}

	private static bool TryGetIntensity(NCreature creature, out float intensity)
	{
		intensity = 0f;
		return creature.Entity != null
			&& creature.Entity.IsAlive
			&& (NearDeathFeastRune.TryGetFeastIntensity(creature.Entity, out intensity)
				|| HextechEnemyNearDeath.TryGetFeastIntensity(creature.Entity, out intensity));
	}

	private bool Start()
	{
		Node2D? renderLayer = HextechBehindCreaturesLayer.GetOrCreate(_creature.GetParent());
		if (renderLayer == null)
		{
			return false;
		}

		_renderLayer = renderLayer;
		_root = new Node2D
		{
			Name = NodeName,
			Visible = false,
			ZAsRelative = true,
			ZIndex = 0
		};
		renderLayer.AddChildSafely(_root);
		EnsureRenderOrder();

		_glow = CreateSprite(_root, "FeastGlow", GetGlowTexture(), GlowColor with { A = 0f }, additive: false);
		_ring = CreateSprite(_root, "FeastRing", GetRingTexture(), RingColor with { A = 0f }, additive: true);
		UpdateTransform(1f);
		return true;
	}

	private async Task RunAsync(ulong creatureInstanceId)
	{
		try
		{
			while (GodotObject.IsInstanceValid(_creature) && GodotObject.IsInstanceValid(_root))
			{
				bool active = TryGetIntensity(_creature, out float intensity);
				_root!.Visible = active;

				if (active && !_wasActive)
				{
					SpawnTriggerBurst();
					_lastIntensity = intensity;
				}
				else if (active && intensity > _lastIntensity + SurgeIntensityStep)
				{
					SpawnDeeperSurge(intensity);
					_lastIntensity = intensity;
				}
				else if (!active)
				{
					_lastIntensity = 0f;
				}

				_wasActive = active;

				if (active)
				{
					EnsureRenderOrder();
					float dt = Mathf.Clamp((float)_root.GetProcessDeltaTime(), 1f / 120f, 0.05f);
					_time = Mathf.PosMod(_time + dt, 3600f);
					Animate(intensity);
				}

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
			Log.Warn($"[{ModInfo.Id}][Mayhem] Near-Death Feast visual stopped after runtime error: {ex.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(_root))
			{
				_root.QueueFree();
			}

			ActiveCreatureNodes.Remove(creatureInstanceId);
		}
	}

	private void Animate(float intensity)
	{
		// 心跳:越濒死跳得越快、越重。两段"咚-咚"(lub-dub)而非匀速正弦,更有生命体征感。
		float rate = Mathf.Lerp(1.05f, 1.95f, intensity);
		float beat = Heartbeat(_time * rate);

		float glowScale = 1f + beat * Mathf.Lerp(0.05f, 0.12f, intensity);
		UpdateTransform(glowScale);

		if (_glow != null)
		{
			float glowAlpha = Mathf.Lerp(0.16f, 0.40f, intensity) + beat * Mathf.Lerp(0.12f, 0.30f, intensity);
			_glow.Modulate = GlowColor with { A = glowAlpha };
		}

		if (_ring != null)
		{
			float ringAlpha = Mathf.Lerp(0.10f, 0.26f, intensity) + beat * 0.18f;
			_ring.Modulate = RingColor with { A = ringAlpha };
		}
	}

	private void UpdateTransform(float glowScalePulse)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_creature) || _creature.Hitbox == null)
		{
			return;
		}

		Vector2 top = _creature.GetTopOfHitbox();
		Vector2 bottom = _creature.GetBottomOfHitbox();
		_root.GlobalPosition = bottom.Lerp(top, BodyHeightFactor);

		float width = Mathf.Clamp(_creature.Hitbox.Size.X, 120f, 360f);
		ScaleSprite(_glow, width * 1.95f * glowScalePulse);
		ScaleSprite(_ring, width * 1.55f);
	}

	// ---- 触发瞬间:扩散冲击波环 + 一记血色闪光 ----
	private void SpawnTriggerBurst()
	{
		float width = Mathf.Clamp(_creature.Hitbox?.Size.X ?? 180f, 120f, 360f);
		SpawnExpandingRing(width * 0.7f, width * 2.4f, 0.45f, 0.92f);
		SpawnFlash(width * 1.7f, 0.34f, 0.32f);
	}

	// ---- 越陷越深(力量又涨一截):一圈小幅脉冲 ----
	private void SpawnDeeperSurge(float intensity)
	{
		float width = Mathf.Clamp(_creature.Hitbox?.Size.X ?? 180f, 120f, 360f);
		SpawnExpandingRing(width * 0.9f, width * 1.7f, 0.34f, 0.55f + intensity * 0.3f);
	}

	private void SpawnExpandingRing(float startDiameter, float endDiameter, float duration, float startAlpha)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		Sprite2D ring = CreateSprite(_root, "FeastBurstRing", GetRingTexture(), RingColor with { A = startAlpha }, additive: true);
		ScaleSpriteTo(ring, startDiameter);
		float endScale = endDiameter / Math.Max(GetRingTexture().GetWidth(), 1);

		Tween tween = ring.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(ring, "scale", new Vector2(endScale, endScale), duration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenProperty(ring, "modulate:a", 0f, duration)
			.SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(ring))
			{
				ring.QueueFree();
			}
		}));
	}

	private void SpawnFlash(float diameter, float peakAlpha, float duration)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		Sprite2D flash = CreateSprite(_root, "FeastFlash", GetGlowTexture(), FlashColor with { A = 0f }, additive: true);
		ScaleSpriteTo(flash, diameter);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "modulate:a", peakAlpha, duration * 0.32f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flash, "modulate:a", 0f, duration * 0.68f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(flash))
			{
				flash.QueueFree();
			}
		}));
	}

	private void EnsureRenderOrder()
	{
		HextechBehindCreaturesLayer.EnsureRenderOrder(_renderLayer);
	}

	private static Sprite2D CreateSprite(Node2D parent, string name, Texture2D texture, Color modulate, bool additive)
	{
		Sprite2D sprite = new()
		{
			Name = name,
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

		parent.AddChildSafely(sprite);
		return sprite;
	}

	private static void ScaleSprite(Sprite2D? sprite, float diameter)
	{
		if (sprite?.Texture is { } texture)
		{
			sprite.Scale = Vector2.One * (diameter / Math.Max(texture.GetWidth(), 1));
		}
	}

	private static void ScaleSpriteTo(Sprite2D sprite, float diameter)
	{
		ScaleSprite(sprite, diameter);
	}

	private static float Heartbeat(float t)
	{
		float phase = Mathf.PosMod(t, 1f);
		float lub = Bump(phase, 0f, 0.055f);
		float dub = Bump(phase, 0.17f, 0.065f) * 0.78f;
		return Mathf.Clamp(lub + dub, 0f, 1f);
	}

	private static float Bump(float x, float center, float sigma)
	{
		float d = x - center;
		if (d > 0.5f)
		{
			d -= 1f;
		}
		else if (d < -0.5f)
		{
			d += 1f;
		}

		return Mathf.Exp(-(d * d) / (2f * sigma * sigma));
	}

	private static Texture2D GetGlowTexture()
	{
		if (_glowTexture != null && GodotObject.IsInstanceValid(_glowTexture))
		{
			return _glowTexture;
		}

		Gradient gradient = new()
		{
			Offsets = [0f, 0.45f, 1f],
			Colors = [new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0f)]
		};
		_glowTexture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
		return _glowTexture;
	}

	private static Texture2D GetRingTexture()
	{
		if (_ringTexture != null && GodotObject.IsInstanceValid(_ringTexture))
		{
			return _ringTexture;
		}

		Gradient gradient = new()
		{
			Offsets = [0f, 0.60f, 0.80f, 0.93f, 1f],
			Colors =
			[
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 1f),
				new Color(1f, 1f, 1f, 0f),
				new Color(1f, 1f, 1f, 0f)
			]
		};
		_ringTexture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = 256,
			Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f)
		};
		return _ringTexture;
	}
}
