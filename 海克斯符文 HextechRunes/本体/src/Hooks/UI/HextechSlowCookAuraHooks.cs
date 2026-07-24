using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechSlowCookAuraHooks
{
	public static void Install(Harmony harmony)
	{
		harmony.Patch(
			RequireMethod(typeof(NCombatRoom), "_Ready", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechSlowCookAuraHooks), nameof(CombatRoomReadyPostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature), BindingFlags.Instance | BindingFlags.Public, typeof(Creature)),
			postfix: new HarmonyMethod(typeof(HextechSlowCookAuraHooks), nameof(AddCreaturePostfix)));
		harmony.Patch(
			RequireMethod(typeof(NCreature), "_Ready", BindingFlags.Instance | BindingFlags.Public),
			postfix: new HarmonyMethod(typeof(HextechSlowCookAuraHooks), nameof(CreatureReadyPostfix)));
	}

	private static void CombatRoomReadyPostfix(NCombatRoom __instance)
	{
		foreach (NCreature creature in __instance.CreatureNodes)
		{
			SlowCookAuraVisual.TryAttach(creature);
		}
	}

	private static void AddCreaturePostfix(NCombatRoom __instance, Creature creature)
	{
		SlowCookAuraVisual.TryAttach(HextechCreatureNodeRegistry.SafeGetCreatureNode(__instance, creature));
	}

	private static void CreatureReadyPostfix(NCreature __instance)
	{
		SlowCookAuraVisual.TryAttach(__instance);
	}
}

/// <summary>
/// 慢炖的持续脚底光环。纹理来自 Pressure Cooker 素材包，并在 Godot 中重组为
/// 热浪底盘、流动火焰、双层边缘、内部暗焰与升腾火花。
/// </summary>
internal sealed class SlowCookAuraVisual
{
	private const string NodeName = "HextechRunes_SlowCookAura";
	private const float RingRotationSpeed = -0.32f;
	private const float InnerFireRotationSpeed = 0.18f;
	private const float PulseSpeed = 2.4f;
	private const float AuraWidth = 800f;
	private const float HeightRatio = 0.36f;
	private static readonly Vector2 GroundOffset = new(0f, -18f);
	private static readonly Color HeatGlowColor = new(1f, 0.18f, 0.04f, 0.22f);
	private static readonly Color PolarFireColor = new(1f, 0.28f, 0.04f, 0.24f);
	private static readonly Color EdgeFireColor = new(1f, 0.45f, 0.08f, 0.34f);
	private static readonly Color EdgeAccentColor = new(1f, 0.72f, 0.20f, 0.24f);
	private static readonly Color InnerFireColor = new(0.82f, 0.18f, 0.03f, 0.20f);
	private static readonly Color GroundRingColor = new(1f, 0.46f, 0.08f, 0.42f);
	private static readonly Color SparkColor = new(1f, 0.72f, 0.22f, 0.55f);
	private static readonly HashSet<ulong> ActiveCreatureNodes = [];
	private static readonly HashSet<string> LoggedMissingTexturePaths = [];
	internal const string FlowShaderCode = """
		shader_type canvas_item;
		render_mode blend_add;

		uniform vec4 tint_color : source_color = vec4(1.0);
		uniform vec2 flow_offset = vec2(0.0);
		uniform sampler2D secondary_texture;
		uniform sampler2D gradient_texture;
		uniform float inner_radius = -0.08;
		uniform float outer_radius = 1.0;
		uniform float polar_amount = 1.0;
		uniform float secondary_strength = 0.45;
		uniform float gradient_strength = 0.65;

		float luminance(vec4 value) {
			return max(value.r, max(value.g, value.b)) * value.a;
		}

		vec2 mirror_repeat(vec2 value) {
			return vec2(1.0) - abs(mod(value, vec2(2.0)) - vec2(1.0));
		}

		void fragment() {
			vec2 centered = UV * 2.0 - vec2(1.0);
			float radius = length(centered);
			float angle = atan(centered.y, centered.x) / 6.2831853 + 0.5;
			vec2 cartesian_uv = mirror_repeat(UV + flow_offset + vec2(centered.y * 0.035, 0.0));
			vec2 polar_uv = fract(vec2(angle + flow_offset.x, radius * 1.35 + flow_offset.y));
			vec2 sample_uv = mix(cartesian_uv, polar_uv, polar_amount);
			float primary = luminance(texture(TEXTURE, sample_uv));
			vec2 alternate_uv = mirror_repeat(sample_uv * vec2(-1.11, 1.17) + vec2(0.37, 0.61));
			float primary_alternate = luminance(texture(TEXTURE, alternate_uv));
			float secondary = luminance(texture(secondary_texture, mirror_repeat(sample_uv * vec2(1.12, 1.07) - flow_offset * 0.55)));
			float gradient = luminance(texture(gradient_texture, mirror_repeat(UV - flow_offset * 0.35)));
			float anchored_gradient = luminance(texture(gradient_texture, mirror_repeat(UV * vec2(1.31, 1.17) + vec2(0.19, 0.53))));
			float stable_primary = mix(primary, primary_alternate, 0.42);
			float coverage = anchored_gradient * 0.52 + stable_primary * 0.30 + secondary * 0.12 + gradient * 0.06;
			float detail = mix(1.0, 0.82 + secondary * 0.26, secondary_strength);
			float falloff = mix(1.0, 0.82 + gradient * 0.24, gradient_strength);
			float inner_mask = smoothstep(inner_radius, inner_radius + 0.08, radius);
			float outer_mask = 1.0 - smoothstep(outer_radius - 0.12, outer_radius, radius);
			float coverage_floor = 0.06 * gradient_strength;
			float intensity = (coverage_floor + coverage * detail * falloff) * inner_mask * outer_mask * 1.25;
			intensity = min(intensity, 0.90);
			COLOR = vec4(tint_color.rgb * intensity, tint_color.a * intensity);
		}
		""";

	private readonly NCreature _creature;
	private Node2D? _root;
	private Node? _renderParent;
	private AuraLayer? _heatLayer;
	private AuraLayer? _polarLayer;
	private AuraLayer? _edgeLayer;
	private AuraLayer? _edgeAccentLayer;
	private AuraLayer? _innerFireLayer;
	private AuraLayer? _ringLayer;
	private readonly List<AuraLayer> _sparkLayers = [];
	private float _time;
	private float _currentWidth;
	private float _currentHeight;

	private SlowCookAuraVisual(NCreature creature)
	{
		_creature = creature;
	}

	internal static void TryAttach(NCreature? creature)
	{
		try
		{
			if (!GodotObject.IsInstanceValid(creature)
				|| !creature.IsNodeReady()
				|| creature.Hitbox == null
				|| creature.Entity?.Player == null)
			{
				return;
			}

			ulong creatureInstanceId = creature.GetInstanceId();
			if (!ActiveCreatureNodes.Add(creatureInstanceId))
			{
				return;
			}

			SlowCookAuraVisual visual = new(creature);
			if (!visual.Start())
			{
				ActiveCreatureNodes.Remove(creatureInstanceId);
				return;
			}

			TaskHelper.RunSafely(visual.RunAsync(creatureInstanceId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][SlowCookAura] Could not attach aura visual: {ex.Message}");
		}
	}

	private static bool ShouldShow(NCreature creature)
	{
		return creature.Entity?.Player?.GetRelic<SlowCookRune>() != null
			&& creature.Entity.IsAlive;
	}

	private bool Start()
	{
		Node? parent = _creature.GetParent();
		if (!GodotObject.IsInstanceValid(parent))
		{
			return false;
		}
		_renderParent = parent;

		_root = new Node2D
		{
			Name = NodeName,
			Visible = false,
			ShowBehindParent = false,
			TopLevel = false,
			ZAsRelative = true,
			ZIndex = 0
		};
		parent.AddChildSafely(_root);
		EnsureRenderOrder();

		_heatLayer = TryCreateFlowLayer(
			_root, "Groundlights", HextechAssets.SlowCookHeatGlowPath, HextechAssets.SlowCookFlameNoisePath,
			HextechAssets.SlowCookAoeGradientPath, HeatGlowColor, -0.08f, 1f, 0f, 0.32f, 0.42f);
		_polarLayer = TryCreateFlowLayer(
			_root, "AoePolar", HextechAssets.SlowCookAoePolarPath, HextechAssets.SlowCookFlameNoisePath,
			HextechAssets.SlowCookAoeGradientPath, PolarFireColor, -0.08f, 0.96f, 0f, 0.52f, 0.64f);
		_edgeLayer = TryCreateFlowLayer(
			_root, "AoeEdge", HextechAssets.SlowCookAoeEdgePath, HextechAssets.SlowCookAoePolarPath,
			HextechAssets.SlowCookAoeGradientPath, EdgeFireColor, 0.62f, 1f, 0f, 0.46f, 0.72f);
		_edgeAccentLayer = TryCreateFlowLayer(
			_root, "AoeEdgeAccentSubtle", HextechAssets.SlowCookEdgeAccentPath, HextechAssets.SlowCookAoeEdgePath,
			HextechAssets.SlowCookAoeGradientSubtlePath, EdgeAccentColor, 0.75f, 1.02f, 0f, 0.56f, 0.78f);
		_innerFireLayer = TryCreateFlowLayer(
			_root, "InnerDarkerEdges", HextechAssets.SlowCookInnerFirePath, HextechAssets.SlowCookInnerFireBPath,
			HextechAssets.SlowCookEdgeAccentPath, InnerFireColor, -0.08f, 0.74f, 0f, 0.62f, 0.45f);
		_ringLayer = TryCreateFlowLayer(
			_root, "PressureRing", HextechAssets.SlowCookGroundRingPath, HextechAssets.SlowCookGroundRingPath,
			HextechAssets.SlowCookAoeGradientPath, GroundRingColor, 0.46f, 1f, 0f, 0.18f, 0.28f);
		for (int i = 0; i < 3; i++)
		{
			AuraLayer? spark = TryCreateFlowLayer(
				_root, $"RisingSpark{i + 1}", HextechAssets.SlowCookFlarePath, HextechAssets.SlowCookFlarePath,
				HextechAssets.SlowCookAoeGradientPath, SparkColor with { A = 0f }, -0.08f, 0.94f, 0f, 0f, 0f);
			if (spark != null)
			{
				_sparkLayers.Add(spark);
			}
		}

		UpdateTransform();
		return _heatLayer != null || _polarLayer != null || _edgeLayer != null || _ringLayer != null;
	}

	private async Task RunAsync(ulong creatureInstanceId)
	{
		try
		{
			while (GodotObject.IsInstanceValid(_creature) && GodotObject.IsInstanceValid(_root))
			{
				bool visible = ShouldShow(_creature);
				_root.Visible = visible;
				if (visible)
				{
					EnsureRenderOrder();
					float dt = Mathf.Min(Mathf.Max((float)_root.GetProcessDeltaTime(), 1f / 120f), 0.05f);
					_time = Mathf.PosMod(_time + dt, 3600f);
					Animate();
					UpdateTransform();
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
			Log.Warn($"[{ModInfo.Id}][SlowCookAura] Aura visual stopped after runtime error: {ex.Message}");
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

	private void EnsureRenderOrder()
	{
		if (GodotObject.IsInstanceValid(_renderParent) && GodotObject.IsInstanceValid(_root) && _root.GetIndex() != 0)
		{
			_renderParent.MoveChildSafely(_root, 0);
		}
	}

	private void UpdateTransform()
	{
		if (_root == null)
		{
			return;
		}

		float width = ResolveWidth(_creature.Hitbox.Size.X);
		float height = width * HeightRatio;
		_currentWidth = width;
		_currentHeight = height;
		_root.GlobalPosition = _creature.GetBottomOfHitbox() + GroundOffset;

		ScaleLayer(_heatLayer, width * 1.36f, height * 1.28f);
		ScaleLayer(_polarLayer, width * 1.26f, height * 1.12f);
		ScaleLayer(_edgeLayer, width * 1.18f, height * 1.08f);
		ScaleLayer(_edgeAccentLayer, width * 1.23f, height * 1.12f);
		ScaleLayer(_innerFireLayer, width * 1.02f, height * 0.90f);
		ScaleLayer(_ringLayer, width * 1.14f, height * 1.08f);
		foreach (AuraLayer spark in _sparkLayers)
		{
			ScaleLayer(spark, width * 0.16f, width * 0.16f);
		}

	}

	internal static float ResolveWidth(float _) => AuraWidth;

	private void Animate()
	{
		float dt = Mathf.Min(Mathf.Max((float)(_root?.GetProcessDeltaTime() ?? 0.016), 1f / 120f), 0.05f);
		if (_ringLayer != null)
		{
			_ringLayer.Sprite.Rotation = Mathf.PosMod(_ringLayer.Sprite.Rotation + RingRotationSpeed * dt, Mathf.Tau);
		}

		if (_innerFireLayer != null)
		{
			_innerFireLayer.Sprite.Rotation = Mathf.PosMod(_innerFireLayer.Sprite.Rotation + InnerFireRotationSpeed * dt, Mathf.Tau);
		}

		float heatPulse = PulseAt(0.2f);
		float polarPulse = PulseAt(1.5f);
		float edgePulse = PulseAt(2.8f);
		float accentPulse = PulseAt(4.1f);
		float innerPulse = PulseAt(5.3f);
		float ringPulse = PulseAt(3.5f);
		SetFlow(_heatLayer, new Vector2(0.17f + _time * 0.018f, 0.63f - _time * 0.035f), HeatGlowColor with { A = 0.23f + heatPulse * 0.02f });
		SetFlow(_polarLayer, new Vector2(0.73f - _time * 0.032f, 0.19f - _time * 0.082f), PolarFireColor with { A = 0.24f + polarPulse * 0.03f });
		SetFlow(_edgeLayer, new Vector2(0.41f + _time * 0.055f, 0.82f - _time * 0.060f), EdgeFireColor with { A = 0.34f + edgePulse * 0.04f });
		SetFlow(_edgeAccentLayer, new Vector2(0.89f - _time * 0.072f, 0.47f - _time * 0.048f), EdgeAccentColor with { A = 0.22f + accentPulse * 0.03f });
		SetFlow(_innerFireLayer, new Vector2(0.56f + _time * 0.038f, 0.28f - _time * 0.070f), InnerFireColor with { A = 0.20f + innerPulse * 0.03f });

		if (_ringLayer != null)
		{
			SetFlow(_ringLayer, new Vector2(0.31f + _time * 0.015f, 0.74f), GroundRingColor with { A = 0.39f + ringPulse * 0.04f });
		}

		for (int i = 0; i < _sparkLayers.Count; i++)
		{
			AuraLayer spark = _sparkLayers[i];
			float phase = Mathf.PosMod(_time * 0.43f + i / (float)_sparkLayers.Count, 1f);
			float angle = i * (Mathf.Tau / _sparkLayers.Count) + _time * 0.38f;
			float rise = phase * _currentHeight * 1.75f;
			spark.Plane.Position = new Vector2(
				MathF.Cos(angle) * _currentWidth * (0.18f + phase * 0.12f),
				-_currentHeight * 0.12f - rise);
			spark.Sprite.Rotation = -angle * 0.35f;
			float alpha = MathF.Sin(phase * MathF.PI) * (0.34f + PulseAt(i * 1.9f) * 0.12f);
			SetFlow(spark, Vector2.Zero, SparkColor with { A = alpha });
		}
	}

	private float PulseAt(float phase)
	{
		return 0.5f + 0.5f * MathF.Sin(_time * PulseSpeed + phase);
	}

	private static void SetFlow(AuraLayer? layer, Vector2 offset, Color tint)
	{
		if (layer?.FlowMaterial == null)
		{
			return;
		}

		layer.FlowMaterial.SetShaderParameter("flow_offset", offset);
		layer.FlowMaterial.SetShaderParameter("tint_color", tint);
	}

	private static AuraLayer? TryCreateFlowLayer(
		Node2D parent,
		string name,
		string path,
		string secondaryPath,
		string gradientPath,
		Color tint,
		float innerRadius,
		float outerRadius,
		float polarAmount,
		float secondaryStrength,
		float gradientStrength)
	{
		Texture2D? texture = LoadTextureOrWarn(path);
		Texture2D? secondaryTexture = LoadTextureOrWarn(secondaryPath);
		Texture2D? gradientTexture = LoadTextureOrWarn(gradientPath);
		if (texture == null || secondaryTexture == null || gradientTexture == null)
		{
			return null;
		}

		Node2D plane = new()
		{
			Name = name,
			ZIndex = 0,
			ZAsRelative = true
		};
		parent.AddChildSafely(plane);

		ShaderMaterial material = new()
		{
			Shader = new Shader { Code = FlowShaderCode }
		};
		material.SetShaderParameter("tint_color", tint);
		material.SetShaderParameter("flow_offset", Vector2.Zero);
		material.SetShaderParameter("secondary_texture", secondaryTexture);
		material.SetShaderParameter("gradient_texture", gradientTexture);
		material.SetShaderParameter("inner_radius", innerRadius);
		material.SetShaderParameter("outer_radius", outerRadius);
		material.SetShaderParameter("polar_amount", polarAmount);
		material.SetShaderParameter("secondary_strength", secondaryStrength);
		material.SetShaderParameter("gradient_strength", gradientStrength);

		Sprite2D sprite = new()
		{
			Name = "Texture",
			Texture = texture,
			Centered = true,
			Modulate = Colors.White,
			Material = material
		};
		plane.AddChildSafely(sprite);
		return new AuraLayer(plane, sprite, FlowMaterial: material);
	}

	private static Texture2D? LoadTextureOrWarn(string path)
	{
		Texture2D? texture = AssetHooks.LoadUiTexture(path);
		if (texture == null && LoggedMissingTexturePaths.Add(path))
		{
			Log.Warn($"[{ModInfo.Id}][SlowCookAura] Aura texture not found: {path}");
		}

		return texture;
	}

	private static void ScaleLayer(AuraLayer? layer, float width, float height)
	{
		Texture2D? texture = layer?.ScaleBasis ?? layer?.Sprite.Texture;
		if (layer == null || texture == null)
		{
			return;
		}

		layer.Plane.Scale = new Vector2(width / Math.Max(texture.GetWidth(), 1), height / Math.Max(texture.GetHeight(), 1));
	}

	private sealed record AuraLayer(
		Node2D Plane,
		Sprite2D Sprite,
		Texture2D? ScaleBasis = null,
		ShaderMaterial? FlowMaterial = null);
}
