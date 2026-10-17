// Last Light — arena backdrop: cold fog sky, a flagstone floor in perspective, drifting mist.
shader_type canvas_item;

uniform float horizon = 0.2;
uniform vec4 sky_top : source_color = vec4(0.10, 0.10, 0.105, 1.0);
uniform vec4 sky_low : source_color = vec4(0.28, 0.275, 0.265, 1.0);
uniform vec4 ground_far : source_color = vec4(0.19, 0.185, 0.175, 1.0);
uniform vec4 ground_near : source_color = vec4(0.065, 0.06, 0.056, 1.0);
uniform vec4 fog_color : source_color = vec4(0.3, 0.3, 0.29, 1.0);

// Stars in the sky (0 = none). Used by the Sanctum of Stars.
uniform float stars = 0.0;
uniform vec4 star_color : source_color = vec4(0.85, 0.9, 1.0, 1.0);

// 0 = flagstones; 1 = forest earth with a trampled clearing (the camp).
uniform float floor_style = 0.0;
// The clearing: centre (UV) and radii (screen heights). Outside it, roots and undergrowth.
uniform vec4 clearing = vec4(0.5, 0.69, 0.66, 0.25);

// Sin-free hash: fract(sin(...)) can round differently in neighbouring cells on
// the GPU, tearing the noise into hard seams and blocks.
float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
	           mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += noise(p) * a;
		p *= 2.03;
		a *= 0.5;
	}
	return v;
}

void fragment() {
	vec2 uv = UV;
	vec3 col;
	if (uv.y < horizon) {
		float t = uv.y / horizon;
		col = mix(sky_top.rgb, sky_low.rgb, t * t);
		if (stars > 0.0) {
			vec2 g = uv * vec2(260.0, 160.0);
			vec2 cell = floor(g);
			float h = hash(cell);
			float tw = 0.6 + 0.4 * sin(TIME * (1.0 + h * 3.0) + h * 40.0);
			float star = step(1.0 - stars, h) * (1.0 - smoothstep(0.05, 0.3, length(fract(g) - 0.5)));
			col += star_color.rgb * star * tw * (1.0 - t * 0.7);
		}
	} else {
		float g = (uv.y - horizon) / (1.0 - horizon);
		col = mix(ground_far.rgb, ground_near.rgb, pow(g, 0.7));

		float z = 1.0 / (g + 0.12);
		vec2 p = vec2((uv.x - 0.5) * z * 2.6, z * 1.5);
		if (floor_style < 0.5) {
			// Flagstones in perspective, cracked and uneven.
			p.x += floor(p.y) * 0.5;
			vec2 fp = abs(fract(p) - 0.5);
			float seam = smoothstep(0.46, 0.5, max(fp.x, fp.y)) * smoothstep(0.02, 0.35, g);
			col *= 1.0 - seam * 0.45;
			col *= 0.8 + 0.4 * fbm(p * 1.7 + 3.0);
		} else {
			// Packed earth, scattered with pebbles.
			col *= 0.72 + 0.5 * fbm(p * 2.4 + 7.0);
			vec2 pc = p * 9.0;
			float ph = hash(floor(pc));
			float pebble = step(0.93, ph) * (1.0 - smoothstep(0.12, 0.3, length(fract(pc) - 0.5)));
			col *= 1.0 - pebble * 0.35;

			// The clearing, worn smooth by feet; a ragged edge into roots and undergrowth.
			float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
			vec2 c = (uv - clearing.xy) * vec2(aspect, 1.0) / clearing.zw;
			float ang = atan(c.y, c.x);
			float d = length(c) + (noise(vec2(cos(ang), sin(ang)) * 3.0) - 0.5) * 0.22;
			float outside = smoothstep(0.82, 1.08, d);
			float worn = 1.0 - smoothstep(0.0, 0.9, d);
			col *= 1.0 + worn * 0.18;
			float tufts = fbm(vec2(p.x * 6.0, p.y * 2.0) + 11.0);
			col = mix(col, col * (0.35 + 0.35 * tufts), outside);
		}
	}

	// Mist: a band at the horizon plus slow drifting fog over everything.
	float drift = fbm(uv * vec2(3.0, 7.0) + vec2(TIME * 0.01, TIME * 0.002));
	col += fog_color.rgb * exp(-abs(uv.y - horizon) * 12.0) * 0.45;
	col += fog_color.rgb * (drift - 0.45) * 0.18;

	COLOR = vec4(col, 1.0);
}
