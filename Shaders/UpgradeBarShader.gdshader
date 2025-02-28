shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
#include "Includes/CornerMask.gdshaderinc"

uniform sampler2D TrianglePattern;
uniform vec2 ControlSize = vec2(100.0, 100.0);
uniform vec2 ShiftDirection = vec2(0.5,0.5);
uniform float CornerRadius = 9.0;
uniform bool ColorMode = false;
uniform vec4 CompleteColor : source_color = vec4(1,0,0,1);
uniform vec4 ProgressColor : source_color = vec4(0,1,0,1);
uniform vec4 EmptyColor : source_color = vec4(0,0,1,1);
uniform float minVal = 10;
uniform float currentVal = 20;
uniform float limitVal = 40;
uniform float totalVal = 50;
uniform float time = 0.0;

varying vec4 modulate;

void vertex()
{
	modulate=COLOR;
}

void fragment() {
	float cornerRadius = min(CornerRadius, min(ControlSize.x, ControlSize.y));
	float cornerPercent = cornerRadius/ControlSize.x;
	float valRange = ((UV.x*(1.0+(cornerPercent*2.0)))-cornerPercent)*totalVal;

	vec2 patternTexRes=1.0/vec2(textureSize(TrianglePattern, 0));
	vec2 patternUV = adaptToAspect(ControlSize, vec2(0,0), patternTexRes, UV, true);

	vec2 moddedPatternUV = patternUV*0.5;
	moddedPatternUV.y+=floor(moddedPatternUV.x)*0.5*0.5;
	moddedPatternUV = mod(moddedPatternUV, vec2(1,1));
	float patternResult = texture(TrianglePattern, moddedPatternUV).r;



	if(valRange<currentVal)
	{
		float rippleGradient = ((patternResult+0.2)*6.0)+patternUV.x-(time*6.0);
		rippleGradient = mod(rippleGradient+3.0, 6)-3.0;
		rippleGradient = 1.0-abs(rippleGradient);
		rippleGradient=clamp(rippleGradient,0,1);
		float patternScale = 0.6+((patternResult+rippleGradient*2.0)*0.2);

		COLOR=mix(CompleteColor*(0.6+(patternResult*0.25)), ProgressColor*patternScale, clamp((valRange-(minVal-0.5))*2.0,0,1));
	}
	else
	{
		float sparkleGradient = (time-(patternResult*2.0));
		sparkleGradient = mod(sparkleGradient+1.0, 2)-1.0;
		sparkleGradient = 1.0-abs(sparkleGradient);
		sparkleGradient=clamp(sparkleGradient,0,1);
		COLOR = vec4(sparkleGradient);
		COLOR=mix(EmptyColor*(0.6+(sparkleGradient*0.25)), vec4(0.1+(patternResult*0.4)), clamp((valRange-(limitVal-0.5))*2.0,0,1));
	}
	COLOR.a=1.0;
	//COLOR.rgb*=vec3(patternScale);
	//COLOR.rgb=vec3(gradient);

	COLOR.a *= cornerMask(UV,cornerRadius,ControlSize);
	COLOR *= modulate;
}
