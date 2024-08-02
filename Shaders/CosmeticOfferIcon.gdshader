shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ShiftDirection = vec2(0.5,0.5);
uniform float CornerRadius = 10.0;
uniform vec2 CosmeticOffset;
uniform float CosmeticScale;
uniform sampler2D Cosmetic;
uniform bool Fit;

void fragment() {
	vec2 adaptedUV = adaptToAspect(ControlSize, ShiftDirection,1.0/TEXTURE_PIXEL_SIZE,UV,Fit);
	adaptedUV-=CosmeticOffset;
	adaptedUV*=1.0+CosmeticScale;
	adaptedUV-=vec2(CosmeticScale*0.5);
	COLOR = texture(Cosmetic, adaptedUV);
	COLOR.a *= cornerMask(UV,CornerRadius,ControlSize);
		if(adaptedUV.x<0.0 || adaptedUV.y<0.0 || adaptedUV.x>1.0 || adaptedUV.y>1.0)
	{
		COLOR.a=0.0;
	}
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
