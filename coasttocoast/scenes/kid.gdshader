shader_type canvas_item;

uniform float sink_height : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec2 uv = UV;
    vec4 tex = texture(TEXTURE, uv);

    if (uv.y > (1.0 - sink_height)) {
        discard;
    }

    COLOR = tex;
}
