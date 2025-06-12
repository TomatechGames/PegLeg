shader_type canvas_item;

uniform sampler2D blendFrom;
uniform float blendAmount:hint_range(0,1)=1;
uniform float threshold:hint_range(0,1) = 0.45;
uniform float smoothness:hint_range(0,1) = 0.1;
uniform bool msdf = false;
uniform bool skipSDF = false;

varying vec4 modulate;
void vertex() {
	modulate = COLOR;
}

float evalDF(float val)
{
	return smoothstep(threshold, threshold+smoothness, val);
}
vec3 evalDF(vec3 val)
{
	return vec3(evalDF(val.r),evalDF(val.g),evalDF(val.b));
}

float median(vec3 rgb)
{
	return max(min(rgb.r,rgb.g), min(max(rgb.r,rgb.g),rgb.b));
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	vec4 blend = texture(blendFrom, UV);

	COLOR = modulate;
	if(skipSDF){
		COLOR.a=source.r;
	}
	else if (msdf)
	{
		vec3 evaled=evalDF(source.rgb);
		//COLOR.a = evalDF(source.r);
		//COLOR.a = evalDF(source.g);
		COLOR.a = evalDF(mix(median(blend.rgb), median(source.rgb), blendAmount));
	}
	else
	{
		COLOR.a = evalDF(source.r);
	}
	COLOR.a*=modulate.a;
}
