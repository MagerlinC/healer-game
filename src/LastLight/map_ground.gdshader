// Last Light — the world map: an old cartographer's survey in faded ink on black.
// Contour lines trace the land; mountains crowd toward the top-right (the Frozen
// Peak); a faint graticule hints the map was once complete.
shader_type canvas_item;

uniform float aspect = 1.7778;
uniform vec4 ground : source_color = vec4(0.035, 0.03, 0.027, 1.0);
uniform vec4 ink : source_color = vec4(0.66, 0.55, 0.4, 1.0);

float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
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
		v += noise(p) * a;
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return v;
}

void fragment() {
	vec2 p = vec2(UV.x * aspect, UV.y);

	// Terrain height: rolling land, rising into a range toward the top-right.
	float h = fbm(p * 2.4);
	h += smoothstep(0.9, 0.0, length(UV - vec2(0.95, 0.4)) ) * 0.35;

	// Contours: thin lines, every fifth one heavier.
	float steps = h * 18.0;
	float c = abs(fract(steps) - 0.5);
	float px = fwidth(steps);
	float line = 1.0 - smoothstep(0.0, px * 1.2, c - 0.0);
	float major = step(4.5, mod(floor(steps + 0.5), 5.0));
	float contour = line * mix(0.32, 0.7, major);

	// Hatching on high ground.
	float hatch = step(0.62, h) * (1.0 - smoothstep(0.0, 0.25, abs(fract((p.x + p.y) * 90.0) - 0.5))) * 0.12;

	// Faded graticule.
	vec2 g = abs(fract(p * 6.0) - 0.5);
	float grid = (1.0 - smoothstep(0.0, fwidth(p.x * 6.0) * 1.2, min(g.x, g.y))) * 0.06;

	// Stains and wear.
	float stain = fbm(p * 5.0 + 11.0);

	vec3 col = ground.rgb * (0.8 + 0.5 * stain);
	col += ink.rgb * (contour + hatch + grid) * (0.6 + 0.6 * stain);

	// Vignette into nothing.
	vec2 v = (UV - 0.5) * vec2(1.0, 1.2);
	col *= 1.0 - smoothstep(0.35, 0.8, length(v)) * 0.7;

	COLOR = vec4(col, 1.0);
}
