// Last Light — overlay on top of the arena backdrop.
// Adds the healer's warm light (glow and a pool on the floor) and darkens
// everything far from it. Premultiplied blend: rgb is added,
// alpha is how much of the backdrop is swallowed.
shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec2 light_uv = vec2(0.5, 0.5);
uniform vec2 feet_uv = vec2(0.5, 0.55);
uniform float strength = 1.0;
uniform float aspect = 1.7778;
uniform float reach = 0.3;
uniform vec4 warm : source_color = vec4(1.0, 0.78, 0.45, 1.0);

// One extra light (a bonfire). extra_strength = 0 disables it.
uniform vec2 extra_uv = vec2(0.5, 0.5);
uniform float extra_strength = 0.0;

// Drifting particles: ash, embers, star motes or snow. Negative speed falls.
uniform vec4 speck_color : source_color = vec4(0.5, 0.48, 0.45, 1.0);
uniform float speck_speed = 1.6;
uniform float speck_density = 0.012;
uniform float speck_size = 1.0;
uniform float speck_strength = 0.3;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 q = UV - light_uv;
	q.x *= aspect;
	float d = length(q);

	vec2 f = UV - feet_uv;
	f.x *= aspect;
	float e = length(vec2(f.x, f.y * 3.0));

	float glow = exp(-d * d / (2.0 * 0.06 * 0.06)) * 0.32 + exp(-d * d / (2.0 * 0.28 * 0.28)) * 0.13;
	float pool = smoothstep(reach * 1.05, 0.0, e) * 0.22;
	vec3 add = warm.rgb * (glow + pool) * strength;

	vec2 x = UV - extra_uv;
	x.x *= aspect;
	float xd = length(vec2(x.x, x.y * 1.6));
	add += vec3(1.0, 0.62, 0.3) * (exp(-xd * xd / (2.0 * 0.09 * 0.09)) * 0.3 + exp(-xd * xd / (2.0 * 0.3 * 0.3)) * 0.1) * extra_strength;

	// Drifting particles (ash by default), with a little sideways sway.
	vec2 g = vec2(UV.x * aspect, UV.y) * (70.0 / speck_size) + vec2(0.0, TIME * speck_speed);
	g.x += sin(g.y * 0.35 + TIME * 0.7) * 0.25;
	vec2 cell = floor(g);
	float h = hash(cell);
	float speck = step(1.0 - speck_density, h) * (1.0 - smoothstep(0.05, 0.12, length(fract(g) - 0.5)));
	add += speck_color.rgb * speck * speck_strength;

	// Darkness: everything far from the light, plus a heavy vignette.
	float far = smoothstep(0.22, 1.05, d / max(strength, 0.3)) * 0.62;
	far = min(far, mix(1.0, smoothstep(0.18, 0.9, xd) * 0.62, step(0.001, extra_strength)));
	vec2 v = (UV - 0.5) * vec2(1.0, 1.25);
	float vig = smoothstep(0.38, 0.85, length(v)) * 0.55;
	float a = clamp(far + vig, 0.0, 0.93);

	COLOR = vec4(add, a);
}
