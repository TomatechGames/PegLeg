shader_type canvas_item;
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ControlSizeScale = vec2(0.66, 1.0);
uniform float CornerRadius = 10.0;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float usableRadius = min(CornerRadius, min(ControlSize.x*0.5, ControlSize.y*0.5));
	float flatAlpha = max(1.0-(cornerGradient(UV,usableRadius,ControlSize*ControlSizeScale)*2.0), 0);
	COLOR.a = flatAlpha*flatAlpha;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
