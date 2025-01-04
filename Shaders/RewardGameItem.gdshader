shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform float CornerRadius = 5.0;

varying vec4 modulate;
void vertex() {
	modulate = COLOR;
}

void fragment() {
	vec2 fitUV = adaptToAspect(ControlSize, vec2(0.5, 1), 1.0/TEXTURE_PIXEL_SIZE, UV, true);
	vec2 controlSpace = UV*ControlSize;
	
	float gradient = 1.0-distance(vec2(0.5, 1.25), fitUV*vec2(1,1.5));
	vec4 fitTex = texture(TEXTURE, fitUV);
	COLOR = mix(mix(vec4(0), modulate, gradient), fitTex, fitTex.a);
	if(controlSpace.y>(ControlSize.y-CornerRadius))
	{
		COLOR = mix(COLOR, modulate, modulate.a);
		COLOR.a *= cornerMask(UV, CornerRadius, ControlSize);
	}
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
