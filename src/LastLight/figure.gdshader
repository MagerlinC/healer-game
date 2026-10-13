// Last Light — shader for a SilhouetteRig (a CanvasGroup of flat polygons).
//
// The group is first rendered as-is, then this shader re-colours the result:
//   mode 0 (party)    lit from the healer's side, warm rim on edges facing the light,
//                     missing health = darkness rising from the feet with a jagged edge.
//   mode 1 (darkness) near-black, faint warm rim; bright colours stay emissive (eyes).
//   mode 2 (light)    the healer: unshaded, warmer toward the hem, hem fades out.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;

uniform int mode = 0;
uniform float consumed : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;

// Body placement in screen UV, written every frame by SilhouetteRig.
uniform vec2 body_feet = vec2(0.5, 0.6);
uniform vec2 body_size = vec2(0.04, 0.12);

// The healer's light, in screen UV. Radius is measured in screen heights.
uniform vec2 light_uv = vec2(0.5, 0.5);
uniform float light_radius = 0.8;

uniform vec4 lit_color : source_color = vec4(0.86, 0.76, 0.58, 1.0);
uniform vec4 shade_color : source_color = vec4(0.11, 0.095, 0.08, 1.0);
uniform vec4 rim_color : source_color = vec4(1.0, 0.86, 0.6, 1.0);
uniform vec4 dark_color : source_color = vec4(0.018, 0.014, 0.014, 1.0);
uniform float rim_px = 2.0;

// Party only: a thin warm outline just outside the silhouette, so allies stay
// readable when they stand inside a boss's darkness. 0 disables.
uniform float outline_px = 2.0;
uniform vec4 outline_color : source_color = vec4(1.0, 0.84, 0.58, 0.8);

float hash1(float n) { return fract(sin(n) * 43758.5453); }
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(hash1(i), hash1(i + 1.0), f);
}

void fragment() {
	vec4 src = textureLod(screen_tex, SCREEN_UV, 0.0);
	vec2 px = SCREEN_PIXEL_SIZE;

	// Outside the silhouette: either the outline ring, or nothing.
	float outline = 0.0;
	if (src.a < 0.004) {
		if (mode != 0 || outline_px <= 0.0) {
			discard;
		}
		float near = 0.0;
		for (int i = 0; i < 8; i++) {
			float a = float(i) * 0.7853982;
			near = max(near, textureLod(screen_tex, SCREEN_UV + vec2(cos(a), sin(a)) * outline_px * px, 0.0).a);
		}
		outline = smoothstep(0.25, 0.8, near);
		if (outline <= 0.0) {
			discard;
		}
		src = vec4(0.0, 0.0, 0.0, 1.0); // keeps the shading math below well-defined
	}
	vec3 base = src.rgb / src.a;

	// Direction and distance to the light, in pixels (aspect-correct).
	vec2 to_light = (light_uv - SCREEN_UV) / px;
	float to_light_len = max(length(to_light), 0.001);
	vec2 ldir = to_light / to_light_len;
	float dist = to_light_len * px.y;
	float reach = clamp(1.0 - dist / light_radius, 0.0, 1.0);

	// Rim: this pixel is on an edge whose outside faces the light.
	float a_out = textureLod(screen_tex, SCREEN_UV + ldir * rim_px * px, 0.0).a;
	float rim = (1.0 - smoothstep(0.15, 0.6, a_out)) * smoothstep(0.0, 0.3, reach);

	// 0 at the feet, 1 at the top of the body.
	float h = (body_feet.y - SCREEN_UV.y) / max(body_size.y, 0.0001);
	// -0.5 .. 0.5 across the body.
	float xn = (SCREEN_UV.x - body_feet.x) / max(body_size.x, 0.0001);

	vec3 col;
	float alpha = src.a;

	if (mode == 2) {
		// The light itself.
		col = base * mix(vec3(0.84, 0.6, 0.32), vec3(1.08, 1.03, 0.95), smoothstep(0.0, 0.65, h));
		alpha *= smoothstep(-0.03, 0.16, h);
	} else if (mode == 1) {
		float emissive = step(0.5, dot(base, vec3(0.3333)));
		col = mix(base, rim_color.rgb, rim * 0.45 * (1.0 - emissive));
	} else {
		// Side facing the light is warm and bright, the far side falls into shade.
		vec2 center_px = (body_feet - vec2(0.0, body_size.y * 0.5)) / px;
		float side = dot(SCREEN_UV / px - center_px, ldir) / max(body_size.x / px.x * 0.5, 1.0);
		float lit = clamp(0.45 + 0.55 * side, 0.0, 1.0) * pow(reach, 0.6);
		col = mix(shade_color.rgb, lit_color.rgb, lit) * base;
		col = mix(col, rim_color.rgb, rim * 0.85);

		// The rot: missing health rises from the feet with a jagged, creeping edge.
		if (consumed > 0.002) {
			float teeth = abs(fract(xn * 4.5 + vnoise(xn * 3.0 + seed) * 1.6) - 0.5) * 2.0;
			float creep = vnoise(xn * 9.0 + seed * 7.0 + TIME * 0.35) - 0.5;
			float edge = consumed * 1.1 - 0.05 + (teeth - 0.5) * 0.16 + creep * 0.08;
			float dark = step(h, edge);

			// Ash flaking off just above the line.
			vec2 cell = floor(SCREEN_UV / px / 2.0);
			float fleck = step(hash2(cell + floor(TIME * 4.0) * 0.37), 0.16) * step(h, edge + 0.06);
			dark = max(dark, fleck);
			if (consumed > 0.995) {
				dark = 1.0;
			}
			col = mix(col, dark_color.rgb, dark);
			// Keep a thin warm outline on the consumed part so a dying ally still reads.
			col = mix(col, rim_color.rgb * 0.7, rim * 0.55 * dark);
		}
	}

	COLOR = vec4(col, alpha);
	if (outline > 0.0) {
		COLOR = vec4(outline_color.rgb, outline_color.a * outline);
	}
}
