// Last Light — the darkness closing in on the healer's light.
// Everything outside a ragged, breathing boundary around the light is swallowed;
// thin tendrils reach inward now and then, and the boundary smoulders faintly.
shader_type canvas_item;

uniform vec2 light_uv = vec2(0.5, 0.6);
uniform float aspect = 1.7778;
uniform float radius = 0.32;      // in screen heights
uniform float squash = 1.5;       // >1 flattens the circle onto the floor
uniform float strength = 1.0;     // the healer's flicker; the dark pushes in when it dips
uniform float max_dark = 0.95;
uniform vec4 ember : source_color = vec4(0.85, 0.38, 0.14, 1.0);

float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x),
	           mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 q = UV - light_uv;
	q.x *= aspect;
	q.y *= squash;
	float d = length(q);
	float ang = atan(q.y, q.x);
	vec2 dir = vec2(cos(ang), sin(ang));
	float t = TIME * 0.12;

	// Ragged, slowly shifting boundary.
	float wob = noise(dir * 2.0 + vec2(t, -t)) * 0.6 + noise(dir * 5.0 + vec2(-t * 1.7, t)) * 0.4;
	float teeth = abs(fract(ang * 16.0 / 6.2831853 + wob * 1.5) - 0.5) * 2.0;
	float fine = abs(fract(ang * 41.0 / 6.2831853 + wob * 3.0) - 0.5) * 2.0;
	// Tendrils: narrow, sharp reaches toward the light that come and go.
	float tendril = pow(noise(dir * 7.0 + vec2(t * 1.4, 3.0)), 5.0);
	float breathe = 0.018 * sin(TIME * 0.45) + 0.008 * sin(TIME * 1.3);

	float r = radius * mix(0.9, 1.0, clamp(strength, 0.0, 1.2))
		+ (wob - 0.5) * 0.08 + (teeth - 0.5) * 0.05 + (fine - 0.5) * 0.015
		- tendril * radius * 0.75 + breathe;

	// A hard ragged front, then smoke thickening into full dark behind it.
	float dark = smoothstep(r, r + 0.01, d) * 0.72 + smoothstep(r, r + 0.14, d) * (max_dark - 0.72);
	// The light also thins toward its edge.
	float dim = smoothstep(r * 0.45, r, d) * 0.3;
	float alpha = max(dark, dim);

	// The boundary smoulders where light meets dark.
	float seam = 1.0 - smoothstep(0.0, 0.004, abs(d - r));
	float flick = 0.6 + 0.4 * noise(dir * 12.0 + vec2(TIME * 1.5, 0.0));

	vec3 col = ember.rgb * seam * flick;
	COLOR = vec4(col, max(alpha, seam * 0.45 * flick));
}
