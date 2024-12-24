shader_type canvas_item;
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ControlSizeScale = vec2(0.66, 1.0);
uniform bool Expo=true;
uniform float AlphaStart = 0.0;
uniform float CornerRadius = 10.0;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float usableRadius = min(CornerRadius, min(ControlSize.x*0.5, ControlSize.y*0.5));
	float newAlpha = clamp(1.0-(cornerGradient(UV,usableRadius,ControlSize*ControlSizeScale)*2.0), 0, 1);
	newAlpha = clamp(newAlpha+AlphaStart-((1.0-newAlpha)*AlphaStart),0,1);
	if(Expo)
	{
		newAlpha*=newAlpha;
	}
	COLOR.a *= newAlpha;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
