namespace UniversalDominionSword;

internal static class AvaritiaCosmicShader
{
	// A Godot canvas-item port of Avaritia's cosmic.frag. The constants, 16-layer
	// spherical projection, pseudo-random symbol selection, rotations, color
	// accumulation, mask application and source animation timings match the
	// original Minecraft implementation. Only the vertex-to-view-ray mapping is
	// adapted from a 3D item quad to a 2D TextureRect.
	public const string Code = """
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D layer_0 : filter_nearest, repeat_disable;
uniform sampler2D layer_1 : filter_nearest, repeat_disable;
uniform sampler2D blade_mask : filter_nearest, repeat_disable;
uniform sampler2D cosmic_0 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_1 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_2 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_3 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_4 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_5 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_6 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_7 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_8 : filter_nearest, repeat_disable;
uniform sampler2D cosmic_9 : filter_nearest, repeat_disable;

uniform int pulse = 0;
uniform int is_used = 0;
uniform int is_wax = 0;

const float AVARITIA_PI = 3.1415926535897932384626433832795;
const float EXTERNAL_SCALE = 25.0;
const float LIGHT_MIX = 0.2;
const vec3 GUI_LIGHT_LEVEL = vec3(1.2);

float rand2d(vec2 x) {
	return fract(sin(mod(dot(x, vec2(12.9898, 78.233)), 3.14)) * 43758.5453);
}

mat4 rotation_matrix(vec3 axis, float angle) {
	axis = normalize(axis);
	float s = sin(angle);
	float c = cos(angle);
	float oc = 1.0 - c;
	return mat4(
		vec4(oc * axis.x * axis.x + c, oc * axis.x * axis.y - axis.z * s, oc * axis.z * axis.x + axis.y * s, 0.0),
		vec4(oc * axis.x * axis.y + axis.z * s, oc * axis.y * axis.y + c, oc * axis.y * axis.z - axis.x * s, 0.0),
		vec4(oc * axis.z * axis.x - axis.y * s, oc * axis.y * axis.z + axis.x * s, oc * axis.z * axis.z + c, 0.0),
		vec4(0.0, 0.0, 0.0, 1.0)
	);
}

float sword_frame(float tick) {
	float t = mod(tick, 30.0);
	if (t < 3.0) return 0.0;
	if (t < 6.0) return 1.0;
	if (t < 9.0) return 2.0;
	if (t < 11.0) return 3.0;
	if (t < 13.0) return 4.0;
	if (t < 14.0) return 5.0;
	if (t < 15.0) return 6.0;
	if (t < 16.0) return 7.0;
	if (t < 17.0) return 8.0;
	if (t < 18.0) return 7.0;
	if (t < 19.0) return 6.0;
	if (t < 20.0) return 5.0;
	if (t < 22.0) return 4.0;
	if (t < 24.0) return 3.0;
	if (t < 27.0) return 2.0;
	return 1.0;
}

float cosmic_frame_0(float tick) {
	float t = mod(tick, 10.0);
	if (t < 7.0) return 0.0;
	return t - 6.0;
}

float cosmic_frame_1(float tick) {
	float t = mod(tick, 23.0);
	if (t < 4.0) return 0.0;
	if (t < 5.0) return 1.0;
	if (t < 14.0) return 0.0;
	if (t < 15.0) return 2.0;
	if (t < 22.0) return 0.0;
	return 3.0;
}

float cosmic_frame_2(float tick) {
	float t = mod(tick, 31.0);
	if (t < 16.0) return 0.0;
	if (t < 19.0) return 1.0;
	if (t < 21.0) return 2.0;
	if (t < 22.0) return 3.0;
	if (t < 23.0) return 4.0;
	if (t < 24.0) return 3.0;
	if (t < 25.0) return 4.0;
	if (t < 26.0) return 3.0;
	if (t < 28.0) return 2.0;
	return 1.0;
}

float cosmic_frame_3(float tick) {
	float t = mod(tick, 47.0);
	if (t < 13.0) return 0.0;
	if (t < 14.0) return 1.0;
	if (t < 24.0) return 0.0;
	if (t < 25.0) return 3.0;
	if (t < 30.0) return 0.0;
	if (t < 31.0) return 2.0;
	if (t < 46.0) return 0.0;
	return 4.0;
}

float cosmic_frame_4(float tick) {
	float t = mod(tick, 37.0);
	if (t < 34.0) return 0.0;
	return t - 33.0;
}

float cosmic_frame_5(float tick) {
	float t = mod(tick, 39.0);
	if (t < 18.0) return 0.0;
	if (t < 19.0) return 1.0;
	if (t < 23.0) return 0.0;
	if (t < 24.0) return 3.0;
	if (t < 38.0) return 0.0;
	return 2.0;
}

float cosmic_frame_8(float tick) {
	float t = mod(tick, 84.0);
	if (t < 1.0) return 1.0;
	if (t < 2.0) return 2.0;
	if (t < 3.0) return 3.0;
	if (t < 4.0) return 2.0;
	if (t < 5.0) return 3.0;
	if (t < 6.0) return 2.0;
	if (t < 7.0) return 1.0;
	if (t < 29.0) return 0.0;
	if (t < 30.0) return 4.0;
	if (t < 31.0) return 5.0;
	if (t < 32.0) return 6.0;
	if (t < 33.0) return 5.0;
	if (t < 34.0) return 6.0;
	if (t < 35.0) return 5.0;
	if (t < 36.0) return 4.0;
	if (t < 67.0) return 0.0;
	if (t < 68.0) return 1.0;
	if (t < 69.0) return 2.0;
	if (t < 70.0) return 3.0;
	if (t < 71.0) return 2.0;
	if (t < 72.0) return 1.0;
	return 0.0;
}

vec4 sample_cosmic(int symbol, vec2 symbol_uv, float tick) {
	if (symbol == 0) {
		return texture(cosmic_0, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_0(tick)) / 4.0));
	}
	if (symbol == 1) {
		return texture(cosmic_1, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_1(tick)) / 4.0));
	}
	if (symbol == 2) {
		return texture(cosmic_2, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_2(tick)) / 5.0));
	}
	if (symbol == 3) {
		return texture(cosmic_3, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_3(tick)) / 5.0));
	}
	if (symbol == 4) {
		return texture(cosmic_4, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_4(tick)) / 4.0));
	}
	if (symbol == 5) {
		return texture(cosmic_5, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_5(tick)) / 4.0));
	}
	if (symbol == 6) {
		return texture(cosmic_6, vec2(symbol_uv.x, (symbol_uv.y + mod(tick, 6.0)) / 6.0));
	}
	if (symbol == 7) {
		return texture(cosmic_7, vec2(symbol_uv.x, (symbol_uv.y + floor(mod(tick, 8.0) / 2.0)) / 4.0));
	}
	if (symbol == 8) {
		return texture(cosmic_8, vec2(symbol_uv.x, (symbol_uv.y + cosmic_frame_8(tick)) / 7.0));
	}
	return texture(cosmic_9, vec2(symbol_uv.x, (symbol_uv.y + floor(mod(tick, 6.0) / 2.0)) / 3.0));
}

void fragment() {
	float tick = floor(TIME * 20.0);
	float frame_0 = sword_frame(tick);
	float frame_1 = floor(mod(tick, 56.0) / 2.0);
	vec2 uv_0 = vec2(UV.x, (UV.y + frame_0) / 9.0);
	vec2 uv_1 = vec2(UV.x, (UV.y + frame_1) / 28.0);

	vec4 body = texture(layer_0, uv_0);
	vec4 edge = texture(layer_1, uv_1);
	vec4 mask_sample = texture(blade_mask, uv_0);
	vec4 base = vec4(
		mix(body.rgb, edge.rgb, edge.a),
		edge.a + body.a * (1.0 - edge.a)
	);

	float one_over_external_scale = 1.0 / EXTERNAL_SCALE;
	int uv_tiles = 16;
	vec4 col = vec4(0.1, 0.0, 0.0, 1.0);
	float color_pulse = mod(tick, 400.0) / 400.0;
	col.g = sin(color_pulse * AVARITIA_PI * 2.0) * 0.075 + 0.225;
	col.b = cos(color_pulse * AVARITIA_PI * 2.0) * 0.05 + 0.3;

	vec3 item_position = vec3(
		(UV.x - 0.5) * 2.0,
		(0.5 - UV.y) * 2.0,
		-2.0
	);
	vec4 dir = normalize(vec4(-item_position, 0.0));

	for (int i = 0; i < 16; i++) {
		int mult = 16 - i;
		int j = i + 7;
		float rand_1 = float(j * j * 4321 + j * 8) * 2.0;
		int k = j + 1;
		float rand_2 = float(k * k * k * 239 + k * 37) * 3.6;
		float rand_3 = rand_1 * 347.4 + rand_2 * 63.4;
		vec3 axis = normalize(vec3(sin(rand_1), sin(rand_2), cos(rand_3)));
		vec4 ray = dir * rotation_matrix(axis, mod(rand_3, 2.0 * AVARITIA_PI));

		float raw_u = 0.5 + atan(ray.z, ray.x) / (2.0 * AVARITIA_PI);
		float raw_v = 0.5 + asin(ray.y) / AVARITIA_PI;
		float scale = float(mult) * 0.5 + 2.75;
		float u = raw_u * scale * EXTERNAL_SCALE;
		float v = (
			raw_v + tick * 0.0002 * one_over_external_scale
		) * scale * 0.6 * EXTERNAL_SCALE;

		int tile_u = int(mod(floor(u * float(uv_tiles)), float(uv_tiles)));
		int tile_v = int(mod(floor(v * float(uv_tiles)), float(uv_tiles)));
		int symbol = int(rand2d(vec2(float(tile_u), float(tile_v + i * 10))) * 101.0);
		int rotation = int(mod(
			pow(float(tile_u), float(tile_v)) + float(tile_u + 3 + tile_v * i),
			8.0
		));
		bool flip = false;
		if (rotation >= 4) {
			rotation -= 4;
			flip = true;
		}

		if (symbol >= 0 && symbol < 10) {
			float local_u = clamp(mod(u, 1.0) * float(uv_tiles) - float(tile_u), 0.0, 1.0);
			float local_v = clamp(mod(v, 1.0) * float(uv_tiles) - float(tile_v), 0.0, 1.0);
			if (flip) {
				local_u = 1.0 - local_u;
			}

			float oriented_u = local_u;
			float oriented_v = local_v;
			if (rotation == 1) {
				oriented_u = 1.0 - local_v;
				oriented_v = local_u;
			} else if (rotation == 2) {
				oriented_u = 1.0 - local_u;
				oriented_v = 1.0 - local_v;
			} else if (rotation == 3) {
				oriented_u = local_v;
				oriented_v = 1.0 - local_u;
			}

			vec4 texture_color = sample_cosmic(
				symbol,
				vec2(oriented_u, oriented_v),
				tick
			);
			float a = texture_color.r
				* (0.5 + (1.0 / float(mult)))
				* (1.0 - smoothstep(0.15, 0.48, abs(raw_v - 0.5)));
			float r = (mod(rand_1, 29.0) / 29.0) * 0.3 + 0.4;
			float g = (mod(rand_2, 35.0) / 35.0) * 0.4 + 0.6;
			float b = (mod(rand_1, 17.0) / 17.0) * 0.3 + 0.7;
			col += vec4(r, g, b, 1.0) * a;
		}
	}

	vec3 shade = GUI_LIGHT_LEVEL * LIGHT_MIX + vec3(1.0 - LIGHT_MIX);
	col.rgb *= shade;
	col.a *= mask_sample.r;
	col = clamp(col, 0.0, 1.0);

	vec4 color = vec4(
		col.rgb * col.a + base.rgb * (1.0 - col.a),
		col.a + base.a * (1.0 - col.a)
	);

	if (is_wax == 1) {
		float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
		color.rgb = mix(color.rgb, vec3(gray) * vec3(1.06, 0.84, 0.62), 0.75);
	}
	if (is_used == 1) {
		float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
		color.rgb = vec3(gray) * 0.48;
	}
	if (pulse == 1) {
		color.rgb *= 1.0 + 0.22 * (0.5 + 0.5 * sin(TIME * 6.0));
	}

	COLOR = color;
}
""";
}
