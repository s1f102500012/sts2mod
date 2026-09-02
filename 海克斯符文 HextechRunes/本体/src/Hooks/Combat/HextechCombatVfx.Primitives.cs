using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechCombatVfx
{
	private static Task<bool> SpawnMissile(
		Node parent,
		Vector2 from,
		Vector2 to,
		float creatureWidth,
		int missileIndex,
		float launchDelaySeconds,
		MissileStyle style)
	{
		bool twinFlames = style == MissileStyle.TwinFlames;
		string effectName = twinFlames ? "TwinFlamesMissile" : "MagicMissile";
		TaskCompletionSource<bool> arrival = new();
		Node2D head = new() { Name = $"HextechRunes_{effectName}" };
		parent.AddChildSafely(head);
		PlaceAboveCreatures(parent, head);
		head.GlobalPosition = from;
		head.TreeExiting += () => arrival.TrySetResult(false);

		float diameter = Mathf.Clamp(creatureWidth * 0.15f, 20f, 40f);
		Sprite2D shadow = new()
		{
			Texture = GetGlowTexture(),
			Centered = true,
			Modulate = twinFlames
				? new Color(0.015f, 0.055f, 0.13f, 0.92f)
				: new Color(0.015f, 0.005f, 0.01f, 0.95f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix }
		};
		Sprite2D core = MakeSprite(
			GetGlowTexture(),
			twinFlames ? new Color(0.12f, 0.58f, 1f, 0.98f) : new Color(1f, 0.045f, 0.025f, 0.98f));
		Sprite2D innerCore = MakeSprite(
			GetGlowTexture(),
			twinFlames ? new Color(1f, 0.84f, 0.18f, 0.96f) : new Color(1f, 0.32f, 0.12f, 0.9f));
		head.AddChild(shadow);
		head.AddChild(core);
		head.AddChild(innerCore);
		SetSpriteDiameter(shadow, diameter * 1.5f);
		SetSpriteDiameter(core, diameter);
		SetSpriteDiameter(innerCore, diameter * 0.38f);

		Line2D outerTrail = new()
		{
			Name = $"HextechRunes_{effectName}OuterTrail",
			Width = diameter * 0.72f,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			JointMode = Line2D.LineJointMode.Round,
			WidthCurve = MakeTrailWidthCurve(),
			Gradient = twinFlames
				? new Gradient
				{
					Offsets = [0f, 0.42f, 1f],
					Colors =
					[
						new Color(0.08f, 0.55f, 1f, 0.94f),
						new Color(0.015f, 0.16f, 0.52f, 0.68f),
						new Color(0f, 0.02f, 0.12f, 0f)
					]
				}
				: new Gradient
				{
					Offsets = [0f, 0.45f, 1f],
					Colors =
					[
						new Color(0.08f, 0.005f, 0.008f, 0.9f),
						new Color(0.015f, 0.002f, 0.004f, 0.65f),
						new Color(0f, 0f, 0f, 0f)
					]
				},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mix }
		};
		Line2D innerTrail = new()
		{
			Name = $"HextechRunes_{effectName}InnerTrail",
			Width = diameter * 0.34f,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			JointMode = Line2D.LineJointMode.Round,
			WidthCurve = MakeTrailWidthCurve(),
			Gradient = twinFlames
				? new Gradient
				{
					Offsets = [0f, 0.32f, 0.76f, 1f],
					Colors =
					[
						new Color(1f, 0.92f, 0.28f, 0.98f),
						new Color(0.98f, 0.66f, 0.08f, 0.88f),
						new Color(0.14f, 0.5f, 1f, 0.4f),
						new Color(0f, 0.05f, 0.2f, 0f)
					]
				}
				: new Gradient
				{
					Offsets = [0f, 0.35f, 0.78f, 1f],
					Colors =
					[
						new Color(1f, 0.08f, 0.025f, 0.95f),
						new Color(0.72f, 0.015f, 0.018f, 0.75f),
						new Color(0.12f, 0.002f, 0.006f, 0.32f),
						new Color(0f, 0f, 0f, 0f)
					]
				},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(outerTrail);
		PlaceAboveCreatures(parent, outerTrail);
		parent.AddChildSafely(innerTrail);
		PlaceAboveCreatures(parent, innerTrail);

		float direction = missileIndex % 2 == 0 ? -1f : 1f;
		float arcHeight = Mathf.Clamp((to - from).Length() * (0.16f + missileIndex * 0.025f), 70f, 180f);
		Vector2 midpoint = (from + to) * 0.5f;
		Vector2 control = midpoint + new Vector2(0f, direction * arcHeight);
		float duration = MagicMissileBaseFlightSeconds + missileIndex * MagicMissileFlightStepSeconds;

		Tween tween = head.CreateTween();
		if (launchDelaySeconds > 0f)
		{
			tween.TweenInterval(launchDelaySeconds);
		}
		tween.TweenMethod(Callable.From((float t) =>
		{
			if (!GodotObject.IsInstanceValid(head))
			{
				return;
			}

			Vector2 position = Bezier(from, control, to, t);
			head.GlobalPosition = position;
			AppendTrailPoint(outerTrail, position);
			AppendTrailPoint(innerTrail, position);
		}), 0f, 1f, duration)
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(parent))
			{
				Color flashColor = twinFlames ? new Color(1f, 0.84f, 0.18f) : new Color(1f, 0.06f, 0.025f);
				Color ringColor = twinFlames ? new Color(0.06f, 0.42f, 1f) : new Color(0.16f, 0.002f, 0.008f);
				SpawnFlash(parent, to, diameter * 2.4f, flashColor, 0.22f, 0.8f, aboveCreaturesOnly: true);
				SpawnRing(parent, to, diameter * 0.45f, diameter * 2.1f, 0.26f, 0.85f, ringColor, aboveCreaturesOnly: true);
			}

			arrival.TrySetResult(true);
			FreeNode(head);
			FadeAndFreeTrail(outerTrail, 0.2f);
			FadeAndFreeTrail(innerTrail, 0.18f);
		}));

		return arrival.Task;
	}

	private static void AppendTrailPoint(Line2D trail, Vector2 globalPosition)
	{
		if (!GodotObject.IsInstanceValid(trail))
		{
			return;
		}

		trail.AddPoint(trail.ToLocal(globalPosition), 0);
		if (trail.GetPointCount() > 18)
		{
			trail.RemovePoint(trail.GetPointCount() - 1);
		}
	}

	private static void FadeAndFreeTrail(Line2D trail, float duration)
	{
		if (!GodotObject.IsInstanceValid(trail))
		{
			return;
		}

		Tween fade = trail.CreateTween();
		fade.TweenProperty(trail, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
		fade.TweenCallback(Callable.From(() => FreeNode(trail)));
	}

	/// <summary>
	/// 一缕亡魂:双层魂头(亮核+外晕)沿上拱的贝塞尔弧线飘向目标,Line2D 拖尾跟随头部渐细渐隐。
	/// 弧高由 arcLift 给定、横向偏移随机,三缕魂各走各的弧线,像魂魄而不是直线弹道。
	/// </summary>
	private static void SpawnSoulWisp(Node parent, Vector2 from, Vector2 to, float diameter, float duration, float delay, float arcLift, Color color, Action? onArrival)
	{
		// 不用 TopLevel:top-level 节点渲染时脱离父绘制树、直接按 canvas 根级项画在最上层,
		// 会盖过战斗结算 UI 且 MoveChild 调整无效;全局定位改用 GlobalPosition setter(自动换算局部)。
		Node2D head = new() { Name = "HextechRunes_SoulWisp" };
		parent.AddChildSafely(head);
		PlaceAboveCreatures(parent, head);
		head.GlobalPosition = from;
		Sprite2D halo = MakeSprite(GetGlowTexture(), color with { A = 0.75f });
		Sprite2D core = MakeSprite(GetGlowTexture(), Brighten(color) with { A = 0.95f });
		head.AddChild(halo);
		head.AddChild(core);
		SetSpriteDiameter(halo, diameter);
		SetSpriteDiameter(core, diameter * 0.45f);

		Line2D trail = new()
		{
			Name = "HextechRunes_SoulTrail",
			Width = diameter * 0.55f,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			JointMode = Line2D.LineJointMode.Round,
			WidthCurve = MakeTrailWidthCurve(),
			// 陷阱:Line2D 设置了 Gradient 后 DefaultColor 被完全忽略——魂色必须写进 Gradient
			// 本身(之前色标全白导致拖尾恒为白色)。
			Gradient = new Gradient
			{
				Offsets = [0f, 0.55f, 1f],
				Colors = [color with { A = 0.6f }, color with { A = 0.3f }, color with { A = 0f }]
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(trail);
		// 拖尾插在魂头之前(先画=垫在头下)。
		PlaceAboveCreatures(parent, trail);

		// 控制点:两点中点上方 arcLift,横向再加一点随机——先上飘、再拐向目标。
		Vector2 mid = (from + to) * 0.5f;
		float sideJitter = (VisualRng.NextSingle() - 0.5f) * (to - from).Length() * 0.3f;
		Vector2 control = mid + new Vector2(sideJitter, arcLift);

		Tween tween = head.CreateTween();
		if (delay > 0f)
		{
			tween.TweenInterval(delay);
		}

		tween.TweenMethod(Callable.From((float t) =>
		{
			if (!GodotObject.IsInstanceValid(head))
			{
				return;
			}

			Vector2 position = Bezier(from, control, to, t);
			head.GlobalPosition = position;
			if (GodotObject.IsInstanceValid(trail))
			{
				// Line2D 点集是局部坐标(非 TopLevel),全局轨迹点须换算。
				trail.AddPoint(trail.ToLocal(position), 0);
				if (trail.GetPointCount() > 16)
				{
					trail.RemovePoint(trail.GetPointCount() - 1);
				}
			}
		}), 0f, 1f, duration)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			onArrival?.Invoke();
			FreeNode(head);
			if (GodotObject.IsInstanceValid(trail))
			{
				Tween fade = trail.CreateTween();
				fade.TweenProperty(trail, "modulate:a", 0f, 0.22f).SetEase(Tween.EaseType.In);
				fade.TweenCallback(Callable.From(() => FreeNode(trail)));
			}
		}));
	}

	// 拖尾宽度:头部(第一个点)全宽,尾端收细。
	private static Curve MakeTrailWidthCurve()
	{
		Curve curve = new();
		curve.AddPoint(new Vector2(0f, 1f));
		curve.AddPoint(new Vector2(1f, 0.08f));
		return curve;
	}

	private static Vector2 CreatureCenter(NCreature node)
	{
		return node.GetTopOfHitbox().Lerp(node.GetBottomOfHitbox(), 0.5f);
	}

	private static float CreatureWidth(NCreature node)
	{
		return Mathf.Clamp(node.Hitbox?.Size.X ?? 180f, 120f, 360f);
	}

	private static void SpawnRing(Node parent, Vector2 globalPos, float startDiameter, float endDiameter, float duration, float startAlpha, Color color, bool aboveCreaturesOnly = false)
	{
		Sprite2D ring = MakeSprite(GetRingTexture(), color with { A = startAlpha });
		parent.AddChildSafely(ring);
		if (aboveCreaturesOnly)
		{
			PlaceAboveCreatures(parent, ring);
		}

		ring.GlobalPosition = globalPos;
		SetSpriteDiameter(ring, startDiameter);
		float endScale = endDiameter / Math.Max(GetRingTexture().GetWidth(), 1);

		Tween tween = ring.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(ring, "scale", new Vector2(endScale, endScale), duration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenProperty(ring, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(ring)));
	}

	private static void SpawnFlash(Node parent, Vector2 globalPos, float diameter, Color color, float duration, float peakAlpha, bool aboveCreaturesOnly = false)
	{
		Sprite2D flash = MakeSprite(GetGlowTexture(), color with { A = 0f });
		parent.AddChildSafely(flash);
		if (aboveCreaturesOnly)
		{
			PlaceAboveCreatures(parent, flash);
		}

		flash.GlobalPosition = globalPos;
		SetSpriteDiameter(flash, diameter);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "modulate:a", peakAlpha, duration * 0.3f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flash, "modulate:a", 0f, duration * 0.7f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => FreeNode(flash)));
	}

	private static void SpawnBeam(Node parent, Vector2 fromGlobal, Vector2 toGlobal, Color color, float duration)
	{
		Line2D beam = new()
		{
			Name = "HextechRunes_VfxBeam",
			TopLevel = true,
			Width = 6f,
			DefaultColor = color with { A = 0.9f },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Points = [fromGlobal, toGlobal],
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		parent.AddChildSafely(beam);

		Tween tween = beam.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(beam, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
		tween.TweenProperty(beam, "width", 1.5f, duration).SetEase(Tween.EaseType.In);
		tween.Chain().TweenCallback(Callable.From(() => FreeNode(beam)));
	}

	private static Sprite2D MakeSprite(Texture2D texture, Color modulate)
	{
		return new Sprite2D
		{
			Texture = texture,
			Centered = true,
			Modulate = modulate,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
	}

	private static void SetSpriteDiameter(Sprite2D sprite, float diameter)
	{
		if (sprite.Texture is { } texture)
		{
			sprite.Scale = Vector2.One * (diameter / Math.Max(texture.GetWidth(), 1));
		}
	}

	private static void FreeNode(Node node)
	{
		if (GodotObject.IsInstanceValid(node))
		{
			node.QueueFree();
		}
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
