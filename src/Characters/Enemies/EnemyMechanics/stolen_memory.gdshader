// That Which Swallowed the Stars — "Stolen Memory" tiles, drawn in the boss's own look.
//   mode 0: safe (preview)    — a thin frame of the healer's warm light, the middle clear
//   mode 1: danger (preview)  — the void closing in from the edges, its maw in the middle
//   mode 2: danger (replay)   — the whole quadrant swallowed: swirling void, stars pulled
//                               into a maw ringed with stolen stars
shader_type canvas_item;

uniform int mode = 2;
uniform float seed = 0.0;
uniform vec2 size = vec2(960.0, 540.0);   // the tile in world units, for round shapes
uniform vec4 rim = vec4(1.0);             // which edges get a starlight rim: left, top, right, bottom
uniform float age = 1.0;                  // seconds since the tile appeared

const vec3 VOID = vec3(0.012, 0.01, 0.026);
const vec3 SMOKE = vec3(0.075, 0.06, 0.14);
const vec3 STAR = vec3(0.86, 0.9, 1.0);
const vec3 STAR_DIM = vec3(0.5, 0.55, 0.8);
const vec3 EMBER = vec3(1.0, 0.72, 0.38);
const vec3 EMBER_BRIGHT = vec3(1.0, 0.9, 0.66);

// Sin-free hash (see creeping_dark.gdshader).
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
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += noise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
	return v;
}
vec2 rot(vec2 p, float a) { float c = cos(a), s = sin(a); return vec2(c * p.x - s * p.y, s * p.x + c * p.y); }

// Stars streaming inward on a spiral: cells in (angle, log radius) space drift toward the centre.
float stream(float ang, float r, float t) {
	const float SPOKES = 44.0;
	vec2 g = vec2((ang / TAU + 0.5) * SPOKES + 0.9 * log(r), log(r) * 11.0 + t * 2.4);
	vec2 cell = floor(g);
	cell.x = mod(cell.x, SPOKES);
	float h = hash2(cell + seed * 7.3);
	if (h < 0.7) return 0.0;
	vec2 c = vec2(hash2(cell + 3.1), hash2(cell + 9.7)) * 0.6 + 0.2;
	vec2 d = fract(g) - c;
	d.y *= 0.45; // streaked along the fall
	float star = smoothstep(0.075, 0.0, length(d)) + smoothstep(0.16, 0.0, length(d)) * 0.25;
	float twinkle = 0.6 + 0.4 * sin(TIME * (3.0 + h * 5.0) + h * 40.0);
	return star * twinkle * smoothstep(0.07, 0.2, r); // snuffed out at the maw
}

// A fixed field of faint stars.
float field(vec2 p, float density) {
	vec2 g = p * 26.0;
	vec2 cell = floor(g);
	float h = hash2(cell + seed * 3.7);
	if (h < 1.0 - density) return 0.0;
	vec2 c = vec2(hash2(cell + 1.3), hash2(cell + 5.9)) * 0.6 + 0.2;
	float twinkle = 0.55 + 0.45 * sin(TIME * (1.5 + h * 4.0) + h * 30.0);
	return smoothstep(0.14, 0.0, length(fract(g) - c)) * twinkle;
}

// The void itself, with its maw. Returns rgb; .a is how bright the starlight in it is.
vec4 void_at(vec2 p, float maw_scale) {
	float r = length(p);
	float ang = atan(p.y, p.x);
	float t = TIME;

	// Smoke twisting into a spiral around the maw.
	vec2 q = rot(p, t * 0.12 + 1.4 * log(r + 0.04) + seed);
	float n = fbm(q * 3.2 + seed * 11.0);
	vec3 col = mix(VOID, SMOKE, smoothstep(0.45, 0.85, n) * 0.85);

	float light = stream(ang, r / maw_scale, t) * 0.9;

	// The maw: a hole ringed with stolen stars, like the boss's face.
	float mr = 0.13 * maw_scale;
	float breathe = 1.0 + 0.04 * sin(t * 1.3 + seed);
	float hole = smoothstep(mr * 0.95 * breathe, mr * 0.7 * breathe, r);
	col = mix(col, vec3(0.0), hole);
	float ring = smoothstep(0.012, 0.0, abs(r - mr * breathe)) * 0.55;
	float beads = step(0.55, hash2(vec2(floor((ang / TAU + 0.5) * 22.0 + t * 0.15), seed)))
		* smoothstep(0.02, 0.0, abs(r - mr * breathe));
	light += ring + beads * 0.9;
	return vec4(col, light);
}

// Distance (in uv) to the nearest rimmed edge.
float rim_distance(vec2 uv) {
	float d = 10.0;
	if (rim.x > 0.5) d = min(d, uv.x);
	if (rim.y > 0.5) d = min(d, uv.y);
	if (rim.z > 0.5) d = min(d, 1.0 - uv.x);
	if (rim.w > 0.5) d = min(d, 1.0 - uv.y);
	return d;
}

void fragment() {
	float aspect = size.x / max(size.y, 1.0);
	vec2 p = (UV - 0.5) * vec2(aspect, 1.0);
	float edge = min(min(UV.x, 1.0 - UV.x) * aspect, min(UV.y, 1.0 - UV.y)); // to any edge, in tile heights
	float appear = smoothstep(0.0, 0.15, age);
	float flare = exp(-age * 5.0);
	float wob = noise(UV * vec2(aspect, 1.0) * 9.0 + TIME * 0.4 + seed) - 0.5;
	vec3 col;
	float alpha;

	if (mode == 0) {
		// Safe: where the light still holds — a thin warm frame with a few embers on it, the middle clear.
		float frame = smoothstep(0.035, 0.0, edge + wob * 0.02);
		float line = smoothstep(0.006, 0.0, abs(edge - 0.018 - wob * 0.006));
		float stars = field(p, 0.08) * smoothstep(0.09, 0.02, edge);
		col = mix(EMBER, EMBER_BRIGHT, clamp(line + stars, 0.0, 1.0));
		alpha = frame * 0.4 + line * 0.9 + stars + smoothstep(0.3, 0.0, edge) * 0.08 + flare * 0.1;
	} else {
		vec4 v = void_at(p, mode == 1 ? 0.75 : 1.0);
		float d_rim = rim_distance(UV) * min(aspect, 1.0);
		float rimline = (mode == 1 ? 0.0 : 1.0) * smoothstep(0.007, 0.0, abs(d_rim - 0.004 + wob * 0.004));
		float rimglow = (mode == 1 ? 0.0 : 1.0) * exp(-d_rim * 40.0) * (0.35 + 0.4 * flare);
		float light = v.a + field(p, 0.05) * 0.5 + rimline * 0.85 + rimglow;
		col = mix(v.rgb, mix(STAR_DIM, STAR, clamp(v.a + rimline, 0.0, 1.0)), clamp(light, 0.0, 1.0));
		alpha = 0.94;

		if (mode == 1) {
			// Preview: only a ragged band of void around the edge and the maw in the middle;
			// the rest a dark veil you can still see through.
			float band = smoothstep(0.1 + wob * 0.05, 0.07 + wob * 0.05, edge);
			float maw = smoothstep(0.17, 0.11, length(p));
			float veil = 0.6;
			float solid = max(band, maw);
			alpha = mix(veil, 0.94, solid);
			col = mix(mix(VOID, v.rgb, 0.5), col, max(solid, clamp(v.a, 0.0, 1.0)));
			alpha = max(alpha, clamp(v.a, 0.0, 1.0));
		}
	}

	COLOR = vec4(col, clamp(alpha, 0.0, 1.0) * appear);
}
