shader_type canvas_item;

uniform float threshold = 0.45;
uniform float smoothness = 0.1;
uniform bool skipSDF = false;

varying vec4 modulate;
void vertex() {
	modulate = COLOR;
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	float main = source.r;
	
	COLOR = modulate;
	if(skipSDF){
		COLOR.a = smoothstep(threshold, threshold+smoothness, main);
	}
	else{
		COLOR.a=main;
	}
	COLOR.a*=modulate.a;
}
