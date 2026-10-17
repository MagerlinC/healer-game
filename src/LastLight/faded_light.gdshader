// Last Light — the moment the last light goes out.
// The dark churns in from every edge and swallows the final pocket of light;
// a dying ember gutters at its heart, the rim smoulders out, and ash drifts up.
// `progress` runs 0 → 1 as the dark takes over, then the scene holds there.
shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 1.0;
uniform vec2 heart = vec2(0.5, 0.42);
uniform vec4 ember : source_color = vec4(0.88, 0.39, 0.16, 1.0);
uniform vec4 abyss : source_color = vec4(0.012, 0.009, 0.016, 1.0);
uniform float max_dark = 0.97;
// How dark the heart stays: dim once the light is gone; 0 for a light still burning there.
uniform float heart_dark = 0.6;
// Fixed size of the pocket of light (screen heights); below 0 it shrinks with progress.
uniform float pocket_radius = -1.0;

// Sin-free hash: the usual fract(sin(...)) one tears into seams and blocks here,
// since neighbouring cells can round differently at the larger fbm coordinates.
float hash2(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x),
	           mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		v += a * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return v;
}

// Ash: sparse motes drifting upward, each fading in and out on its own clock.
float ash(vec2 p, float scale, float speed, float seed) {
	p *= scale;
	p.y += TIME * speed;
	p.x += sin(p.y * 0.7 + seed * 4.0) * 0.35;
	vec2 cell = floor(p);
	vec2 f = fract(p) - 0.5;
	float h = hash2(cell + seed);
	if (h < 0.82) return 0.0;
	vec2 offset = (vec2(hash2(cell + 3.1), hash2(cell + 7.7)) - 0.5) * 0.6;
	float mote = 1.0 - smoothstep(0.0, 0.07, length(f - offset));
	return mote * (0.5 + 0.5 * sin(TIME * 1.3 + h * 40.0));
}

void fragment() {
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 p = UV - heart;
	p.x *= aspect;
	float d = length(p);
	float ang = atan(p.y, p.x);
	vec2 dir = vec2(cos(ang), sin(ang));

	float k = smoothstep(0.0, 1.0, progress);

	// Churning smoke the darkness is made of, folding over itself.
	float t = TIME * 0.06;
	float smoke = fbm(p * 2.6 + vec2(t, -t * 0.6));
	float churn = fbm(p * 5.0 + smoke * 1.8 - vec2(t * 1.4, t * 0.3));

	// The last pocket of light, shrinking as the dark wins. It breathes, unevenly.
	float breathe = 0.025 * sin(TIME * 0.55) + 0.012 * sin(TIME * 1.37 + 1.3);
	float radius = (pocket_radius >= 0.0 ? pocket_radius : mix(1.25, 0.28, k)) + breathe;
	// Tendrils reaching inward for what light is left (they thin out near the heart,
	// where their angle-based noise would otherwise pinch into a star).
	float tendril = pow(noise(dir * 6.5 + vec2(TIME * 0.12, 4.0)), 4.0);
	float edge = d - radius
		+ (churn - 0.5) * 0.22
		+ tendril * radius * 0.55 * k * smoothstep(0.0, radius, d);

	float dark = smoothstep(-0.3, 0.12, edge);
	// The light is gone: even its heart stays dim, and the corners are a pit.
	float vignette = smoothstep(0.35, 1.1, d);
	float alpha = mix(heart_dark, max_dark, max(dark, vignette * 0.85));
	// Smoke thickens and thins as it rolls past.
	alpha = clamp(alpha + (smoke - 0.5) * 0.12 * dark, 0.0, 1.0);

	// A dying ember at the heart, guttering — it dims as the dark closes.
	float flicker = 0.7 + 0.3 * noise(vec2(TIME * 3.1, 0.0)) * noise(vec2(TIME * 1.7, 5.0));
	float glow = exp(-d * d * 18.0) * mix(0.55, 0.16, k) * flicker;

	// The rim smoulders where the dark eats into the light, then burns out.
	float rim = (1.0 - smoothstep(0.0, 0.035, abs(edge))) * (1.0 - k * 0.75);
	rim *= 0.5 + 0.5 * noise(p * 40.0 + vec2(0.0, -TIME * 1.2));

	// Ash rising through the dark.
	float motes = (ash(p, 9.0, 0.35, 0.0) + ash(p, 15.0, 0.55, 2.3) * 0.6) * dark;

	vec3 col = abyss.rgb;
	col += ember.rgb * (glow * 0.6 + rim * 0.55);
	col += vec3(0.42, 0.38, 0.34) * motes * 0.35;

	// Fade the whole thing in at the very start so it doesn't snap on.
	float fade = smoothstep(0.0, 0.18, progress);
	alpha = max(alpha, max(rim * 0.6, motes * 0.5));
	COLOR = vec4(col, alpha * fade);
}
