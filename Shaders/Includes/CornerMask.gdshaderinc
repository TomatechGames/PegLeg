
float cornerGradient(vec2 uv, float cornerRadius, vec2 controlSize)
{
	vec2 worldUV = (uv*controlSize);

	if(worldUV.x>cornerRadius)
	{
		worldUV = vec2(max(worldUV.x-(controlSize.x-(cornerRadius*2.0)), cornerRadius),worldUV.y);
	}
	if(worldUV.y>cornerRadius)
	{
		worldUV = vec2(worldUV.x, max(worldUV.y-(controlSize.y-(cornerRadius*2.0)), cornerRadius));
	}

	return distance(worldUV/(cornerRadius*2.0), vec2(0.5));
}

float cornerMaskFaded(vec2 uv, float cornerRadius, vec2 controlSize, float fadeDistance)
{
	cornerRadius = min(min(cornerRadius, controlSize.x*0.5), controlSize.y*0.5);
	float gradient = cornerGradient(uv, cornerRadius, controlSize);

	fadeDistance=clamp(fadeDistance, 0, cornerRadius-0.01);
	float fadeGradient=0.5*fadeDistance/cornerRadius;
	gradient-=0.5-fadeGradient;
	gradient/=fadeGradient;

	return 1.0-clamp(gradient,0,1);
}

float cornerMaskFadedN(vec2 uv, float cornerRadius, vec2 controlSize, float fadeDistanceNormalised)
{
	cornerRadius = min(min(cornerRadius, controlSize.x*0.5), controlSize.y*0.5);
	return cornerMaskFaded(uv, cornerRadius, controlSize, cornerRadius*fadeDistanceNormalised);
}

float cornerMask(vec2 uv, float cornerRadius, vec2 controlSize)
{
	return cornerMaskFaded(uv, cornerRadius, controlSize, 1.0);
}
