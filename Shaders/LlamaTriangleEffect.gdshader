shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"


uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform float CornerRadius = 10.0;
uniform bool Birthday = false;
uniform vec4 BDColMain : source_color = vec4(0.5,1,1,1);
uniform vec4 BDColA : source_color = vec4(0.25,0.25,1,1);
uniform vec4 BDColB : source_color = vec4(0.25,1,0.25,1);
uniform vec4 BDColC : source_color = vec4(1,0.25,0.25,1);

varying vec4 modulate;

void vertex() {
	modulate = COLOR;
}

vec4 triangleShift(vec2 baseUV, float speed, sampler2D tex)
{
	vec4 shifted = clamp(texture(tex, baseUV-vec2(speed*TIME,0))*8.0*(1.0-(baseUV.x*0.25)), 0.0, 1.0);
	return clamp(shifted*(1.0-distance(baseUV.x*0.25,0)), 0.0, 1.0);
}

void fragment() {
	vec2 adaptedUV = adaptToAspect(ControlSize, vec2(0,0.5), 1.0/TEXTURE_PIXEL_SIZE, UV, true)*vec2(1);

	float dist = (1.0-(distance(UV/vec2(2,1),vec2(0,0.5))));
	if(Birthday)
	{
		COLOR = BDColMain * dist;
		COLOR = mix(COLOR, BDColA, triangleShift(adaptedUV, 0.2, TEXTURE).x*0.75);
		COLOR = mix(COLOR, BDColB, triangleShift(adaptedUV, 0.4, TEXTURE).y*0.75);
		COLOR = mix(COLOR, BDColC, triangleShift(adaptedUV, 0.6, TEXTURE).z*0.75);
	}
	else
	{
		float value = dist * 0.75;
		value += triangleShift(adaptedUV, 0.2, TEXTURE).x * 0.25;
		value += triangleShift(adaptedUV, 0.4, TEXTURE).y * 0.25;
		value += triangleShift(adaptedUV, 0.6, TEXTURE).z * 0.25;
		COLOR = modulate * value;
	}
	COLOR.a = cornerMask(UV,CornerRadius,ControlSize);
}
