using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace HextechRunes;

internal sealed partial class HextechGoldenRerollVisual : Control
{
	internal const string ShaderCode = """
		shader_type canvas_item;
		render_mode blend_add, unshaded;

		uniform float hover_strength = 0.0;
		uniform float layer_strength = 1.0;
		uniform float pulse_strength = 0.0;
		uniform float animation_time = 0.0;
		uniform float halo_strength = 0.0;

		float hash21(vec2 value) {
			value = fract(value * vec2(123.34, 456.21));
			value += dot(value, value + 45.32);
			return fract(value.x * value.y);
		}

		float noise(vec2 value) {
			vec2 cell = floor(value);
			vec2 local = fract(value);
			local = local * local * (3.0 - 2.0 * local);
			return mix(
				mix(hash21(cell), hash21(cell + vec2(1.0, 0.0)), local.x),
				mix(hash21(cell + vec2(0.0, 1.0)), hash21(cell + vec2(1.0, 1.0)), local.x),
				local.y);
		}

		void fragment() {
			float mask = texture(TEXTURE, UV).a;
			float flow = 0.5 + 0.5 * sin((UV.x * 9.0 - UV.y * 5.0) + animation_time * 3.4);
			float counter_flow = 0.5 + 0.5 * sin((UV.x * -15.0 - UV.y * 4.0) + animation_time * 2.1);
			float coarse_noise = noise(UV * vec2(10.0, 6.0) + vec2(animation_time * 0.65, -animation_time * 0.28));
			float fine_noise = noise(UV * vec2(31.0, 17.0) - vec2(animation_time * 0.44, animation_time * 0.57));

			float sweep_position = mod(animation_time * 0.46, 1.70) - 0.35;
			float sweep_coordinate = UV.x + UV.y * 0.34;
			float sweep = 1.0 - smoothstep(0.025, 0.15, abs(sweep_coordinate - sweep_position));
			float secondary_sweep = 1.0 - smoothstep(0.018, 0.08, abs(sweep_coordinate - sweep_position + 0.18));

			float sparkle_gate = step(0.88, fine_noise);
			float sparkle_wave = 0.5 + 0.5 * sin(animation_time * 12.0 + floor(UV.x * 18.0) * 1.7);
			float sparkles = sparkle_gate * sparkle_wave;
			float shimmer = 0.30 + flow * 0.25 + counter_flow * 0.14 + coarse_noise * 0.18;
			float pulse_boost = 0.90 + pulse_strength * 0.16;
			float hover_boost = 1.0 + hover_strength * 0.18;
			float brightness = pulse_boost * hover_boost * layer_strength;

			vec3 deep_gold = vec3(0.74, 0.30, 0.015);
			vec3 warm_gold = vec3(1.0, 0.66, 0.09);
			vec3 hot_gold = vec3(1.0, 0.82, 0.28);
			vec3 color = mix(deep_gold, warm_gold, clamp(shimmer, 0.0, 1.0));
			color = mix(color, hot_gold, clamp(sweep * 0.78 + secondary_sweep * 0.28 + sparkles * 0.22, 0.0, 1.0));

			float moving_energy = sweep * 0.62 + secondary_sweep * 0.20 + sparkles * 0.16;
			float alpha = mask * (0.58 + shimmer * 0.24 + moving_energy * 0.20);
			alpha *= brightness * (1.0 + halo_strength * 0.10);
			COLOR = vec4(color * brightness * (1.0 + moving_energy * 0.22), alpha);
		}
		""";

	private readonly List<ShaderMaterial> _materials = [];
	private bool _active;
	private bool _animationLoopStarted;
	private float _elapsed;
	private ulong _animationStartedAtMsec;
	private TextureRect? _haloLayer;
	private TextureRect? _fillLayer;
	private TextureRect? _outerGlowLayer;

	private HextechGoldenRerollVisual()
	{
		Name = "GoldenRerollVisual";
		MouseFilter = MouseFilterEnum.Ignore;
		ProcessMode = ProcessModeEnum.Always;
		ClipContents = false;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public static HextechGoldenRerollVisual? Create(
		Texture2D? outerMask,
		Texture2D? fillMask,
		Vector2 buttonSize,
		float sourceScale)
	{
		if (outerMask == null || fillMask == null)
		{
			return null;
		}

		HextechGoldenRerollVisual visual = new();
		visual._haloLayer = visual.AddMaskLayer(
			"GoldenOuterHalo",
			outerMask,
			buttonSize,
			sourceScale * 1.12f,
			layerStrength: 0.48f,
			haloStrength: 1f);
		visual._fillLayer = visual.AddMaskLayer(
			"GoldenFill",
			fillMask,
			buttonSize,
			sourceScale,
			layerStrength: 0.72f,
			haloStrength: 0f);
		visual._outerGlowLayer = visual.AddMaskLayer(
			"GoldenOuterGlow",
			outerMask,
			buttonSize,
			sourceScale,
			layerStrength: 0.84f,
			haloStrength: 0.22f);
		return visual;
	}

	public void SetVisualState(bool active, bool hovered, bool disabled)
	{
		bool shouldAnimate = active && !disabled;
		if (shouldAnimate && !_active)
		{
			_elapsed = 0f;
			_animationStartedAtMsec = Time.GetTicksMsec();
			SetPulseStrength(0f);
			ApplyLayerAnimation(0f, 0f);
		}

		_active = shouldAnimate;
		Visible = active && !disabled;
		float hover = hovered && !disabled ? 1f : 0f;
		foreach (ShaderMaterial material in _materials)
		{
			material.SetShaderParameter("hover_strength", hover);
		}
	}

	public void StartAnimationLoop()
	{
		if (_animationLoopStarted || !IsInsideTree())
		{
			return;
		}

		_animationLoopStarted = true;
		if (_active)
		{
			_animationStartedAtMsec = Time.GetTicksMsec();
		}

		Log.Info($"[{ModInfo.Id}][UI] Golden reroll animation loop started node={GetParent()?.Name}");
		TaskHelper.RunSafely(RunAnimationLoopAsync());
	}

	private async Task RunAnimationLoopAsync()
	{
		bool runningLogged = false;
		try
		{
			while (GodotObject.IsInstanceValid(this) && IsInsideTree())
			{
				SceneTree tree = GetTree();
				if (!GodotObject.IsInstanceValid(tree))
				{
					return;
				}

				await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				if (!_active)
				{
					continue;
				}

				_elapsed = (Time.GetTicksMsec() - _animationStartedAtMsec) / 1000f;
				float animationTime = _elapsed * 1.5f;
				float pulse = 0.5f - 0.5f * MathF.Cos(animationTime * MathF.Tau / 4f);
				foreach (ShaderMaterial material in _materials)
				{
					material.SetShaderParameter("animation_time", animationTime);
				}
				SetPulseStrength(pulse);
				ApplyLayerAnimation(animationTime, pulse);

				if (!runningLogged && _elapsed >= 1f)
				{
					runningLogged = true;
					Log.Info(
						$"[{ModInfo.Id}][UI] Golden reroll animation advanced " +
						$"node={GetParent()?.Name} elapsed={_elapsed:F2} pulse={pulse:F2}");
				}
			}
		}
		catch (ObjectDisposedException)
		{
		}
		catch (Exception ex)
		{
			if (GodotObject.IsInstanceValid(this))
			{
				Log.Warn($"[{ModInfo.Id}][UI] Golden reroll animation stopped: {ex.Message}");
			}
		}
		finally
		{
			_animationLoopStarted = false;
		}
	}

	private void ApplyLayerAnimation(float elapsed, float pulse)
	{
		float quickShimmer = 0.5f + 0.5f * MathF.Sin(elapsed * 3.8f);
		if (GodotObject.IsInstanceValid(_haloLayer))
		{
			float haloScale = 0.985f + pulse * 0.045f;
			_haloLayer!.Scale = Vector2.One * haloScale;
			_haloLayer.SelfModulate = new Color(1f, 0.84f, 0.42f, 0.68f + pulse * 0.14f);
		}

		if (GodotObject.IsInstanceValid(_fillLayer))
		{
			float fillScale = 0.997f + quickShimmer * 0.012f;
			_fillLayer!.Scale = Vector2.One * fillScale;
			_fillLayer.SelfModulate = new Color(1f, 0.93f, 0.72f, 0.78f + pulse * 0.08f);
		}

		if (GodotObject.IsInstanceValid(_outerGlowLayer))
		{
			float glowScale = 0.995f + pulse * 0.025f;
			_outerGlowLayer!.Scale = Vector2.One * glowScale;
			_outerGlowLayer.SelfModulate = new Color(1f, 0.88f, 0.48f, 0.66f + quickShimmer * 0.10f);
		}
	}

	private TextureRect AddMaskLayer(
		string name,
		Texture2D texture,
		Vector2 buttonSize,
		float sourceScale,
		float layerStrength,
		float haloStrength)
	{
		ShaderMaterial material = new()
		{
			Shader = new Shader { Code = ShaderCode }
		};
		material.SetShaderParameter("hover_strength", 0f);
		material.SetShaderParameter("layer_strength", layerStrength);
		material.SetShaderParameter("pulse_strength", 0f);
		material.SetShaderParameter("animation_time", 0f);
		material.SetShaderParameter("halo_strength", haloStrength);
		_materials.Add(material);

		Vector2 layerSize = new(texture.GetWidth() * sourceScale, texture.GetHeight() * sourceScale);
		TextureRect layer = new()
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Material = material,
			Position = (buttonSize - layerSize) / 2f,
			Size = layerSize,
			PivotOffset = layerSize / 2f
		};
		AddChild(layer);
		return layer;
	}

	private void SetPulseStrength(float pulse)
	{
		foreach (ShaderMaterial material in _materials)
		{
			material.SetShaderParameter("pulse_strength", pulse);
		}
	}
}
