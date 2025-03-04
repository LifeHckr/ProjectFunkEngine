//based on this star shader. Thanks gerardogc2378
//https://godotshaders.com/shader/stars-shader/

shader_type canvas_item;

uniform vec4 bg_top_color;
uniform vec4 bg_bottom_color;
uniform float gradient_ratio;
uniform float time_scale;

float rand(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
}

void fragment() {
	float color = 0.0;

	if (rand(SCREEN_UV.xy / 20.0) > 0.996)
	{
		float r = rand(SCREEN_UV.xy);
		color = r * (0.85 * sin((TIME * time_scale) * (r * 5.0) + 720.0 * r) + 0.95);
	}

    vec4 gradient_color = mix(bg_top_color, bg_bottom_color, SCREEN_UV.y / gradient_ratio);

    COLOR = vec4(vec3(color),1.0) + gradient_color;
}