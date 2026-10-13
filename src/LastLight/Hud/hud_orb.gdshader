// Last Light — the mana orb: black glass holding molten light.
// The surface waves, sparks rise through the light, and the halo dims as it drains.
shader_type canvas_item;

uniform float ratio : hint_range(0.0, 1.0) = 1.0;

uniform vec4 deep : source_color = vec4(0.26, 0.06, 0.02, 1.0);
uniform vec4 mid : source_color = vec4(0.82, 0.42, 0.13, 1.0);
uniform vec4 hot : source_color = vec4(1.0, 0.95, 0.82, 1.0);
uniform vec4 glass : source_color = vec4(0.03, 0.025, 0.022, 1.0);
uniform vec4 iron : source_color = vec4(0.2, 0.17, 0.14, 1.0);
uniform vec4 halo : source_color = vec4(1.0, 0.72, 0.38, 1.0);

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
	for (int i = 0; i < 4; i++) {
		v += noise(p) * a;
		p *= 2.1;
		a *= 0.5;
	}
	return v;
}

void fragment() {
	// p in [-1.25, 1.25]: the glass is the unit circle, the halo spills past it.
	vec2 p = (UV - 0.5) * 2.5;
	float r = length(p);
	float lowlight = smoothstep(0.0, 0.35, ratio);

	if (r > 1.0) {
		// Outer halo + soft drop shadow.
		float glow = (1.0 - smoothstep(1.0, 1.25, r)) * 0.35 * lowlight * (0.9 + 0.1 * sin(TIME * 3.0));
		float shadow = (1.0 - smoothstep(1.0, 1.12, length(p - vec2(0.0, 0.06)))) * 0.5;
		COLOR = vec4(halo.rgb * glow, max(glow, shadow));
	} else {
		vec3 col;
		float inner = 0.9;

		if (r > inner) {
			// Iron rim, lit from the top-left.
			float lit = clamp(dot(normalize(p), vec2(-0.6, -0.8)), 0.0, 1.0);
			col = mix(iron.rgb * 0.6, iron.rgb * 2.2, lit * lit);
		} else {
			vec2 q = p / inner;
			// Surface height in q-space (-1 top, +1 bottom), with waves.
			// Never quite brim-full, so the living surface is always visible.
			float surface = 1.0 - ratio * 1.9;
			surface += 0.035 * sin(q.x * 6.0 + TIME * 2.2) + 0.018 * sin(q.x * 13.0 - TIME * 3.1);
			float depth = q.y - surface;

			if (depth > 0.0) {
				// Molten light, hottest just under the surface.
				float t = clamp(depth / max(1.0 - surface, 0.05), 0.0, 1.0);
				vec3 light = mix(hot.rgb, mid.rgb, smoothstep(0.0, 0.2, t));
				light = mix(light, deep.rgb, smoothstep(0.45, 1.0, t));
				float swirl = fbm(q * 2.5 + vec2(0.0, TIME * 0.35));
				light *= 0.85 + 0.3 * swirl;
				// A hot core, as if the light is burning from the middle.
				light += hot.rgb * 0.55 * pow(max(1.0 - length(q - vec2(0.0, 0.1)) * 1.15, 0.0), 2.5);
				// Sparks rising through it.
				vec2 g = q * 9.0 + vec2(0.0, TIME * 1.4);
				float s = step(0.965, hash2(floor(g))) * (1.0 - smoothstep(0.08, 0.2, length(fract(g) - 0.5)));
				light += vec3(1.0, 0.85, 0.6) * s * 0.7;
				// Meniscus line.
				light += vec3(1.0, 0.9, 0.7) * (1.0 - smoothstep(0.0, 0.05, depth)) * 0.6;
				col = light * mix(0.55, 1.0, lowlight);
			} else {
				// Empty black glass with faint smoke.
				float smoke = fbm(q * 2.0 + vec2(TIME * 0.05, -TIME * 0.12));
				col = glass.rgb + vec3(0.06, 0.05, 0.045) * smoke;
			}

			// Inner shading toward the rim, and a cold reflection top-left.
			col *= 1.0 - smoothstep(0.55, 1.0, length(q)) * 0.55;
			float refl = 1.0 - smoothstep(0.0, 0.14, abs(length(q - vec2(-0.05, 0.08)) - 0.78));
			refl *= smoothstep(-0.2, -0.75, q.x + q.y * 0.6) * 0.18;
			col += vec3(0.85, 0.88, 0.9) * refl;
		}

		float aa = 1.0 - smoothstep(0.985, 1.0, r);
		COLOR = vec4(col, aa);
	}
}
