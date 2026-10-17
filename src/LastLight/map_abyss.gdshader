// Last Light — the world map's secret: a hole in the top of the world. A pitch-black
// pit seen at an angle, ringed by slow ashen clouds spiralling down into it, a
// faint cold rim, and now and then something inside it opens an eye. While the
// way is shut it is barely there: a smudge on the map you only notice if you look.
// Drawn above the map's darkness so the swirl reads even where no light reaches.
shader_type canvas_item;

uniform float rect_aspect = 1.53;  // width / height of the rect this is drawn on
uniform float squash = 1.6;        // >1 flattens the pit onto the ground
uniform float awake = 0.0;         // 0 = a rumour on the map, 1 = the way is open
uniform vec4 tint : source_color = vec4(0.5, 0.55, 0.62, 1.0);

// Sin-free hash: fract(sin(...)) can round differently in neighbouring cells on
// the GPU, tearing the noise into hard seams and blocks.
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
		v += noise(p) * a;
		p = p * 2.03 + vec2(1.7, 9.2);
		a *= 0.5;
	}
	return v;
}
mat2 rot(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, -s), vec2(s, c));
}

void fragment() {
	vec2 p = (UV - 0.5) * vec2(rect_aspect, 1.0) * 2.0;
	p.y *= squash;
	float r = length(p);
	float t = TIME;

	// Spiral: the closer to the pit, the further the clouds have been wound in.
	// Rotating cartesian coords (not sampling by angle) keeps the noise seamless.
	vec2 q = rot(1.9 / (r + 0.3) + t * 0.09) * p;
	vec2 q2 = rot(1.2 / (r + 0.4) + t * 0.05) * p;
	float cloud = fbm(q * 2.3 + vec2(0.0, t * 0.04));
	float wisp = fbm(q2 * 4.6 - vec2(t * 0.03, 0.0));
	float density = cloud * 0.7 + wisp * 0.45;

	// The pit itself, with a ragged lip that the clouds tumble over.
	float lip = 0.36 - 0.06 * awake + (cloud - 0.5) * 0.14;
	float core = 1.0 - smoothstep(lip - 0.05, lip + 0.09, r);

	// A clouded ring around it, thinning out raggedly into the map.
	float outer = 1.0 - smoothstep(0.8, 1.45, r + (wisp - 0.5) * 0.45);
	float ring = smoothstep(lip - 0.04, lip + 0.22, r) * outer;
	float clouds = smoothstep(0.32, 0.85, density) * ring;

	// A faint cold glow where the light falls in over the lip.
	float rim = exp(-pow((r - lip - 0.05) / 0.07, 2.0)) * (0.5 + 0.5 * wisp);
	float breathe = 0.75 + 0.25 * sin(t * 0.6);

	// Ash-grey smoke, a touch cold against the map's warm ink.
	float presence = mix(0.55, 1.0, awake);
	vec3 smoke = mix(vec3(0.05, 0.05, 0.055), vec3(0.2, 0.2, 0.22), density);
	vec3 col = smoke * clouds * presence;
	col += tint.rgb * rim * mix(0.06, 0.45, awake) * breathe;

	// Every so often, something down there looks back.
	float blink = pow(max(0.0, sin(t * 0.42)), 60.0);
	vec2 e = (p - vec2(0.0, 0.03)) * vec2(1.0, 2.2);
	float eye = (1.0 - smoothstep(0.0, 0.07, length(e))) * blink;
	col += vec3(0.85, 0.85, 0.8) * eye * mix(0.35, 0.8, awake);

	float alpha = max(core * mix(0.85, 1.0, awake), max(clouds * 0.85 * presence, rim * 0.6));
	col = mix(col, vec3(0.0), core * (1.0 - eye));
	COLOR = vec4(col, clamp(alpha + eye, 0.0, 1.0));
}
