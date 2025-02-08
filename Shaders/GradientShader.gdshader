shader_type canvas_item;

void fragment() {
	COLOR.a*=1.0-(distance(vec2(0.5),UV)*2.0);
}
