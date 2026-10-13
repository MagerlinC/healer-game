// Last Light — arena backdrop: cold fog sky, a flagstone floor in perspective, drifting mist.
shader_type canvas_item;

uniform float horizon = 0.2;
uniform vec4 sky_top : source_color = vec4(0.10, 0.10, 0.105, 1.0);
uniform vec4 sky_low : source_color = vec4(0.28, 0.275, 0.265, 1.0);
uniform vec4 ground_far : source_color = vec4(0.19, 0.185, 0.175, 1.0);
uniform vec4 ground_near : source_color = vec4(0.065, 0.06, 0.056, 1.0);
uniform vec4 fog_color : source_color = vec4(0.3, 0.3, 0.29, 1.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
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
	} else {
		float g = (uv.y - horizon) / (1.0 - horizon);
		col = mix(ground_far.rgb, ground_near.rgb, pow(g, 0.7));

		// Flagstones in perspective, cracked and uneven.
		float z = 1.0 / (g + 0.12);
		vec2 p = vec2((uv.x - 0.5) * z * 2.6, z * 1.5);
		p.x += floor(p.y) * 0.5;
		vec2 fp = abs(fract(p) - 0.5);
		float seam = smoothstep(0.46, 0.5, max(fp.x, fp.y)) * smoothstep(0.02, 0.35, g);
		col *= 1.0 - seam * 0.45;
		col *= 0.8 + 0.4 * fbm(p * 1.7 + 3.0);
	}

	// Mist: a band at the horizon plus slow drifting fog over everything.
	float drift = fbm(uv * vec2(3.0, 7.0) + vec2(TIME * 0.01, TIME * 0.002));
	col += fog_color.rgb * exp(-abs(uv.y - horizon) * 12.0) * 0.45;
	col += fog_color.rgb * (drift - 0.45) * 0.18;

	COLOR = vec4(col, 1.0);
}
