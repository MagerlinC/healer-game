// Last Light — party frame health fill.
// Health is light. It meets the darkness at a jagged, creeping edge; recent
// damage leaves a dull ember trail that catches up a moment later.
shader_type canvas_item;

uniform float ratio : hint_range(0.0, 1.0) = 1.0;
uniform float ghost : hint_range(0.0, 1.0) = 1.0;
uniform float dead = 0.0;
uniform float seed = 0.0;
uniform vec2 size = vec2(144.0, 65.0);

uniform vec4 light_hi : source_color = vec4(0.93, 0.7, 0.42, 1.0);
uniform vec4 light_lo : source_color = vec4(0.5, 0.24, 0.08, 1.0);
uniform vec4 danger_hi : source_color = vec4(0.85, 0.3, 0.13, 1.0);
uniform vec4 danger_lo : source_color = vec4(0.32, 0.05, 0.03, 1.0);
uniform vec4 trail : source_color = vec4(0.32, 0.08, 0.05, 1.0);
uniform vec4 dark : source_color = vec4(0.025, 0.02, 0.018, 1.0);

float hash1(float n) { return fract(sin(n) * 43758.5453); }
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash1(i), hash1(i + 1.0), f);
}
float vnoise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1.0, 0.0)), f.x),
	           mix(hash2(i + vec2(0.0, 1.0)), hash2(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 px = UV * size;

	// Jagged edge: zigzag teeth along Y plus slow creep, ~5px deep.
	float teeth = abs(fract(UV.y * 3.2 + vnoise(UV.y * 2.0 + seed) * 1.4) - 0.5) * 2.0;
	float creep = vnoise(UV.y * 7.0 + seed * 3.0 + TIME * 0.6) - 0.5;
	float jag = ((teeth - 0.5) * 6.0 + creep * 4.0) / size.x;
	float edge = ratio <= 0.001 ? -1.0 : (ratio >= 0.999 ? 2.0 : ratio + jag);
	float ghost_edge = ghost <= 0.001 ? -1.0 : ghost + jag;

	// Urgency: the light reddens below ~40% health.
	float danger = 1.0 - smoothstep(0.15, 0.45, ratio);

	vec3 col;
	if (UV.x < edge) {
		// Candlelight: brighter through the middle, with a slow shimmer.
		float band = 1.0 - pow(abs(UV.y - 0.45) * 2.0, 1.6);
		float shimmer = vnoise2(vec2(px.x * 0.05 - TIME * 0.4, px.y * 0.12 + TIME * 0.2));
		vec3 hi = mix(light_hi.rgb, danger_hi.rgb, danger);
		vec3 lo = mix(light_lo.rgb, danger_lo.rgb, danger);
		col = mix(lo, hi, clamp(band * 0.85 + shimmer * 0.25, 0.0, 1.0));
		// The leading edge burns a little hotter.
		float near_edge = 1.0 - smoothstep(0.0, 10.0, (edge - UV.x) * size.x);
		col += vec3(0.3, 0.16, 0.05) * near_edge;
		// Low health flickers.
		col *= 1.0 - danger * 0.18 * (0.5 + 0.5 * sin(TIME * 9.0 + seed));
	} else if (UV.x < ghost_edge) {
		col = trail.rgb * (0.8 + 0.2 * sin(TIME * 6.0));
	} else {
		col = dark.rgb;
		// Embers drifting off the edge into the dark.
		float d = (UV.x - edge) * size.x;
		vec2 cell = floor(px / 2.0 + vec2(-TIME * 6.0, TIME * 3.0));
		float spark = step(0.93, hash2(cell + seed)) * (1.0 - smoothstep(0.0, 18.0, d));
		col = mix(col, vec3(0.9, 0.45, 0.15), spark * 0.8);
	}

	// Inner bevel: darken top and bottom rims.
	float rim = smoothstep(0.0, 3.0, px.y) * smoothstep(0.0, 3.0, size.y - px.y);
	col *= mix(0.55, 1.0, rim);

	col = mix(col, vec3(dot(col, vec3(0.3, 0.59, 0.11))) * 0.35, dead);
	COLOR = vec4(col, 1.0);
}
