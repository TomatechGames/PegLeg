shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ShiftDirection = vec2(0.5,0.5);
uniform bool Fit;
uniform bool UseCorners;
uniform float CornerRadius = 10.0;

void fragment() {
	vec2 adaptedUV = adaptToAspect(ControlSize, ShiftDirection,1.0/TEXTURE_PIXEL_SIZE,UV,Fit);

	COLOR = texture(TEXTURE, adaptedUV);

	if(UseCorners)
	{
		COLOR.a *= cornerMask(UV,CornerRadius,ControlSize);
	}
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
