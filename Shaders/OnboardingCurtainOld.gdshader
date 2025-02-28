shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

const float INV_TWO_PI = 1.0/6.28318530718;

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform float time = 0.0;

uniform float CornerRadius = 9.0;
uniform float CornerDepth :hint_range(0.0, 1.0, 0.01) = 0;
uniform float revealScale :hint_range(0.0, 1.0, 0.01) = 0;

varying vec4 modulate;
void vertex() {
	modulate=COLOR;
}

void fragment() {
	vec2 patternUV = (UV*ControlSize)*0.01;
	vec2 fitUV = adaptToAspect(ControlSize, vec2(0.5), vec2(1), UV, true);

	vec2 radialPos=fitUV-0.5;
	float height = distance(radialPos, vec2(0));
	float width = (atan(radialPos.y, radialPos.x) + PI) * INV_TWO_PI + height*0.15;

	float spiralHeight = height*8.0+mod(time, 8);
	vec2 spiralUV=vec2(spiralHeight, width*20.0+floor(spiralHeight)*0.125);

	float result = texture(TEXTURE, spiralUV).r;
	float limit = clamp((height*5.0)-0.4, 0, 1) * clamp((-height*0.5)+1.0, 0, 1);
	float resultPower = sin((result+time*0.5+spiralHeight*0.125)*PI*2.0)*limit;

	COLOR = vec4(resultPower);
	float triangleAlpha=cornerMaskFadedN(UV, CornerRadius, ControlSize, CornerDepth);
	COLOR = mix(modulate, COLOR, triangleAlpha);
	COLOR.a = 1.0;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
