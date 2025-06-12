shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform float CornerRadius = 5.0;
uniform float solidThreshold = 10.0;
uniform float transformProgress:hint_range(0.0,1.0) = 0.0;
uniform bool extraTransform=false;
uniform bool useSolid=false;
uniform bool Fit = true;

varying vec4 modulate;
void vertex() {
	modulate = COLOR;
}

vec2 rotateUV(vec2 uv, vec2 pivot, float rotation) {
	float cosa = cos(rotation);
	float sina = sin(rotation);
	uv -= pivot;
	return vec2(
		cosa * uv.x - sina * uv.y,
		cosa * uv.y + sina * uv.x
	) + pivot;
}

vec2 scaleUV(vec2 uv, vec2 pivot, float scale)
{
	float invScale = 1.0/scale;
	return (uv*invScale)-(invScale-1.0);
}

vec4 clipUV(vec2 uv)
{
	if(uv.x<0.0 || uv.y<0.0 || uv.x>1.0 || uv.y>1.0)
	{
		return vec4(0);
	}

	return vec4(1);
}

void fragment() {
	vec2 paddedUV=UV;
	paddedUV=(UV*1.4)-vec2(0.2);
	vec2 fitUV = adaptToAspect(ControlSize, vec2(0.5, 1), 1.0/TEXTURE_PIXEL_SIZE, paddedUV, Fit);
	vec2 controlSpace = paddedUV*ControlSize;
	float limitedProgress=min(transformProgress,1);

	float gradient = 1.0-distance(vec2(1, 2), fitUV*vec2(2,2));
	gradient-=1.0-limitedProgress;

	vec2 transformedUV = fitUV+(extraTransform?vec2(0.08,-0.18):vec2(0.0));
	transformedUV = scaleUV(transformedUV,vec2(0,0),extraTransform?(transformProgress*1.05)+0.001 : transformProgress);
	transformedUV = rotateUV(transformedUV, vec2(0.8,0.8), (0.5-transformProgress*(extraTransform ? 0.6:0.5))*PI);
	//vec4 fitTex = vec4(transformedUV.r,transformedUV.g,0.0,1.0);
	vec4 fitTex = texture(TEXTURE, transformedUV)*clipUV(transformedUV);

	COLOR = mix(mix(vec4(0), modulate, clamp(gradient,0.0,1.0)), fitTex, fitTex.a);
	float diag = (ControlSize.x+ControlSize.y)-(controlSpace.x+controlSpace.y);
	if((useSolid || paddedUV.y>1.0) && (diag<solidThreshold || controlSpace.y>(ControlSize.y-CornerRadius)))
	{
		vec4 solid = modulate;
		solid.a = min(limitedProgress,1);
		COLOR = mix(COLOR, solid, modulate.a);
		COLOR.a *= cornerMask(paddedUV, CornerRadius, ControlSize);
	}
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
