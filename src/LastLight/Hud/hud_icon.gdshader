// Last Light — spell icons, as if seen by candlelight: partly desaturated and warmed.
shader_type canvas_item;

uniform float desaturate : hint_range(0.0, 1.0) = 0.62;
uniform vec4 warm : source_color = vec4(1.0, 0.86, 0.68, 1.0);

void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float grey = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	c.rgb = mix(c.rgb, vec3(grey), desaturate) * warm.rgb;
	COLOR = c;
}
