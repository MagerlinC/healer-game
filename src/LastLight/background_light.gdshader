// Last Light — overlay on top of the arena backdrop.
// Adds the healer's warm light (glow, a pool on the floor, the dashed edge of its
// reach) and darkens everything far from it. Premultiplied blend: rgb is added,
// alpha is how much of the backdrop is swallowed.
shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec2 light_uv = vec2(0.5, 0.5);
uniform vec2 feet_uv = vec2(0.5, 0.55);
uniform float strength = 1.0;
uniform float aspect = 1.7778;
uniform float reach = 0.3;
uniform vec4 warm : source_color = vec4(1.0, 0.78, 0.45, 1.0);

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
	float ang = atan(f.y * 3.0, f.x);
	float dash = step(0.45, fract(ang * 14.0 / 6.2831));
	float ring = (1.0 - smoothstep(0.0, 0.0035, abs(e - reach))) * dash * 0.2;
	vec3 add = warm.rgb * (glow + pool + ring) * strength;

	// Drifting ash: grey specks, rising slowly.
	vec2 g = vec2(UV.x * aspect, UV.y) * 70.0 + vec2(0.0, TIME * 1.6);
	vec2 cell = floor(g);
	float h = hash(cell);
	float speck = step(0.988, h) * (1.0 - smoothstep(0.05, 0.12, length(fract(g) - 0.5)));
	add += vec3(0.5, 0.48, 0.45) * speck * 0.3;

	// Darkness: everything far from the light, plus a heavy vignette.
	float far = smoothstep(0.22, 1.05, d / max(strength, 0.3)) * 0.62;
	vec2 v = (UV - 0.5) * vec2(1.0, 1.25);
	float vig = smoothstep(0.38, 0.85, length(v)) * 0.55;
	float a = clamp(far + vig, 0.0, 0.93);

	COLOR = vec4(add, a);
}
