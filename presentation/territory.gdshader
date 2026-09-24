shader_type canvas_item;
// One draw for the whole map: terrain colour + owner tint + borders computed from neighbouring owner ids.

uniform sampler2D terrain_tex : filter_nearest;
uniform sampler2D owner_tex : filter_nearest;
uniform sampler2D palette_tex : filter_nearest;
uniform vec2 map_size = vec2(400.0, 240.0);
uniform float me = 0.0;
uniform float focus = 0.0;
uniform float pulse = 0.0;

float owner_at(vec2 uv) {
	return floor(texture(owner_tex, uv).r * 255.0 + 0.5);
}

vec4 pal(float id) {
	return texture(palette_tex, vec2((id + 0.5) / 256.0, 0.5));
}

void fragment() {
	vec2 texel = 1.0 / map_size;
	vec2 tile = floor(UV * map_size);
	vec2 cell = UV * map_size - tile;
	vec2 uv = (tile + 0.5) * texel;
	vec3 ground = texture(terrain_tex, uv).rgb;
	float o = owner_at(uv);
	vec3 col = ground;
	if (o > 0.5) {
		vec4 p = pal(o);
		col = mix(ground, p.rgb, 0.58);
		float edge = 0.0;
		// Border pixels on the inner side of each tile edge facing another owner.
		float w = 0.22;
		if (cell.x < w && owner_at(uv - vec2(texel.x, 0.0)) != o) edge = 1.0;
		if (cell.x > 1.0 - w && owner_at(uv + vec2(texel.x, 0.0)) != o) edge = 1.0;
		if (cell.y < w && owner_at(uv - vec2(0.0, texel.y)) != o) edge = 1.0;
		if (cell.y > 1.0 - w && owner_at(uv + vec2(0.0, texel.y)) != o) edge = 1.0;
		if (edge > 0.5) {
			col = p.rgb * 0.55;
			if (abs(o - me) < 0.5) col = mix(vec3(1.0), p.rgb, 0.25);
			if (abs(o - focus) < 0.5) col = mix(col, vec3(1.0, 0.95, 0.4), 0.5 + 0.5 * pulse);
		}
		// Traitors (palette alpha < 1) flicker.
		if (p.a < 0.99) col = mix(col, vec3(0.9, 0.1, 0.1), 0.25 * pulse);
	}
	COLOR = vec4(col, 1.0);
}
