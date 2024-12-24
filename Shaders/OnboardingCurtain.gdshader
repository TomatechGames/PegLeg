shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

const float INV_TWO_PI = 1.0/6.28318530718;

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform float revealScale :hint_range(0.0, 1.0, 0.01) = 0;

void fragment() {
	float smallest=min(ControlSize.x, ControlSize.y);
	float largest=max(ControlSize.x, ControlSize.y);
	float upperBound = 0.5*largest/smallest;
	
	vec2 fitUV = adaptToAspect(ControlSize, vec2(0.5), vec2(1), UV, true);
	float gradient = distance(fitUV, vec2(0.5));
	gradient*=20.0;
	gradient-=(revealScale*(upperBound*20.0+5.0))-1.0;
	
	COLOR.a*=gradient;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
