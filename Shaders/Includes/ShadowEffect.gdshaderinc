vec4 AddShadow(vec4 over, vec2 uv, float brightness, float opacity)
{
	float gradient = distance(uv, vec2(0.5))*2.0;
	gradient*=gradient;
	gradient = 1.0-gradient;
	vec4 fill =vec4(over.rgb, 1);
	return mix(vec4(vec3(brightness),gradient*opacity),fill, over.a);
}