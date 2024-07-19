shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/FlipbookSlice.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 SliceAmount = vec2(4.000000, 4.000000);
uniform float FPS = 60;
uniform int CellCount;
uniform bool Fit;
uniform bool Repeat;
uniform float HorizontalMask;
uniform float VerticalMask;

void fragment() {
	float hmask = HorizontalMask>0.0 ? min(UV.x/HorizontalMask, -(UV.x/HorizontalMask)+(1.0/HorizontalMask)) : 1.0;
	float vmask = VerticalMask>0.0 ? min(UV.y/VerticalMask, -(UV.y/VerticalMask)+(1.0/VerticalMask)) : 1.0;
	float totalMask = min(hmask, vmask);
	
	vec2 adaptedUV = Fit ? adaptToAspect(ControlSize, vec2(0.5),1.0/TEXTURE_PIXEL_SIZE,UV, true) : UV;
	vec2 slicedUV = flipbookSlice(adaptedUV, SliceAmount, float(CellCount), FPS*TIME,Repeat);
	COLOR = texture(TEXTURE, slicedUV);
	//COLOR = vec4(0,0,0,1);
	//COLOR.rg = slicedUV;
	COLOR.a = COLOR.a * clamp(totalMask,0.0,1.0);
	if(slicedUV.x<-0.5)
		COLOR = vec4(0);
}
