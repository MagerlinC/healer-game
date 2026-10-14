// Last Light — the darkness closing in on every light that is left.
// Each light holds back a ragged, breathing pocket; the dark fills everything
// outside them, thin tendrils reach inward now and then, and the edges smoulder.
shader_type canvas_item;

const int MAX_LIGHTS = 12;

uniform int light_count = 1;
uniform vec2 light_uvs[12];
uniform float light_radii[12];     // in screen heights
uniform float light_soft[12];      // 0 = ragged smouldering pocket, 1 = faint round glow

uniform float aspect = 1.7778;
uniform float squash = 1.5;       // >1 flattens each pocket onto the floor
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

// Distance from the ragged edge of one light's pocket (negative = inside the light).
float edge_distance(vec2 uv, vec2 light_uv, float radius, float seed, float soft) {
	vec2 q = uv - light_uv;
	q.x *= aspect;
	q.y *= squash;
	float d = length(q);
	float ang = atan(q.y, q.x);
	vec2 dir = vec2(cos(ang), sin(ang));
	float t = TIME * 0.12 + seed * 13.0;

	float wob = noise(dir * 2.0 + vec2(t, -t)) * 0.6 + noise(dir * 5.0 + vec2(-t * 1.7, t)) * 0.4;
	float teeth = abs(fract(ang * 16.0 / 6.2831853 + wob * 1.5 + seed) - 0.5) * 2.0;
	float fine = abs(fract(ang * 41.0 / 6.2831853 + wob * 3.0) - 0.5) * 2.0;
	// Tendrils: narrow, sharp reaches toward the light that come and go.
	float tendril = pow(noise(dir * 7.0 + vec2(t * 1.4, 3.0 + seed * 5.0)), 5.0);
	float breathe = 0.018 * sin(TIME * 0.45 + seed * 6.0) + 0.008 * sin(TIME * 1.3 + seed * 9.0);

	// Edge features shrink with small pockets so they stay ragged, not shredded.
	float scale = clamp(radius / 0.3, 0.35, 1.2);
	float rag = ((wob - 0.5) * 0.08 + (teeth - 0.5) * 0.05 + (fine - 0.5) * 0.015) * scale
		- tendril * radius * 0.75;
	// Soft glows barely breathe; the ragged pockets heave.
	float r = radius + breathe * scale * (1.0 - 0.85 * soft) + rag * (1.0 - soft);
	return d - r;
}

void fragment() {
	float alphas[MAX_LIGHTS];
	float edges[MAX_LIGHTS];
	float alpha = max_dark;
	int n = min(light_count, MAX_LIGHTS);

	for (int i = 0; i < n; i++) {
		float soft = light_soft[i];
		float e = edge_distance(UV, light_uvs[i], light_radii[i], float(i) * 0.37, soft);
		float r = light_radii[i];
		// A hard ragged front, then smoke thickening into full dark behind it.
		float a = smoothstep(0.0, 0.01, e) * 0.72 + smoothstep(0.0, 0.14, e) * (max_dark - 0.72);
		// The light also thins toward its edge.
		a = max(a, smoothstep(-r * 0.55, 0.0, e) * 0.3);
		// Soft lights never fully clear the dark: a faint glow fading out smoothly.
		float glow = mix(0.12, max_dark, smoothstep(-r * 0.45, r * 0.7, e));
		a = mix(a, glow, soft);
		alphas[i] = a;
		edges[i] = e;
		alpha = min(alpha, a);
	}

	// Smoulder only where a pocket's edge is not lit by another light.
	float seam = 0.0;
	for (int i = 0; i < n; i++) {
		float others = 1.0;
		for (int j = 0; j < n; j++) {
			if (j != i) {
				others = min(others, smoothstep(0.0, 0.5, alphas[j]));
			}
		}
		seam = max(seam, (1.0 - smoothstep(0.0, 0.004, abs(edges[i]))) * others * (1.0 - light_soft[i]));
	}
	float flick = 0.6 + 0.4 * noise(UV * vec2(aspect, 1.0) * 60.0 + vec2(TIME * 1.5, 0.0));

	COLOR = vec4(ember.rgb * seam * flick, max(alpha, seam * 0.45 * flick));
}
