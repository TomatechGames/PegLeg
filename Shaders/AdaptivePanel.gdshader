shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ShiftDirection = vec2(0.5,0.5);
uniform bool Fit;
uniform bool UseCorners;
uniform bool ApplyCornersToFit;
uniform float CornerRadius = 10.0;
uniform bool ClipOOB;

void fragment() {
	vec2 texRes=1.0/TEXTURE_PIXEL_SIZE;
	vec2 adaptedUV = adaptToAspect(ControlSize, ShiftDirection,texRes,UV,Fit);

	COLOR = texture(TEXTURE, adaptedUV);

	if(UseCorners)
	{
		vec2 targetUV = ApplyCornersToFit && Fit ? adaptedUV : UV;
		vec2 targetSize = ApplyCornersToFit && Fit ? GetFitSize(ControlSize, texRes) : ControlSize;
		COLOR.a *= cornerMask(targetUV,CornerRadius,targetSize);
	}
	if(Fit && ClipOOB)
	{
		COLOR.a*=1.0-ceil(max(adaptedUV.x-1.0,adaptedUV.y-1.0));
		COLOR.a*=ceil(min(adaptedUV.x,adaptedUV.y));
	}
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
