shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform mat3 Transformation;
uniform float CornerRadius=10;
uniform vec2 ShiftDirection = vec2(0.5,0.5);
uniform bool Fit;
uniform vec2 BGUVOffset = vec2(0);
uniform float BGUVScale = 0.8;
uniform sampler2D BGTex;


void vertex() {
	vec3 rotStart=vec3((ControlSize.x*0.5)-VERTEX.x, (ControlSize.y*0.5)-VERTEX.y, 0);
	vec3 rotEnd=Transformation*rotStart;
	vec2 scaledEnd=rotEnd.xy*(1.0+(rotEnd.z*0.001));
	VERTEX=(ControlSize*0.5)-scaledEnd;
}

void fragment() {
	vec2 adaptedUV = adaptToAspect(ControlSize, ShiftDirection,1.0/TEXTURE_PIXEL_SIZE,UV,Fit);
	
	vec2 bgUV=(adaptedUV*BGUVScale)+((1.0-BGUVScale)*0.5)+BGUVOffset;
	
	COLOR = texture(BGTex, bgUV);
	vec4 mainTex = texture(TEXTURE, adaptedUV);
	COLOR=mix(COLOR,mainTex,mainTex.a);
	COLOR.a *= cornerMask(UV,CornerRadius,ControlSize);
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
