float cornerMask(vec2 uv, float cornerRadius, vec2 controlSize)
{
	cornerRadius = min(cornerRadius, min(controlSize.x*0.5, controlSize.y*0.5));
	vec2 controlPos = uv*controlSize;
	vec2 shrunkSize = controlSize-vec2(cornerRadius,cornerRadius);

	float maskA = distance(controlPos, vec2(cornerRadius,cornerRadius))-(cornerRadius-1.0);
	float maskB = distance(controlPos, vec2(shrunkSize[0],cornerRadius))-(cornerRadius-1.0);
	float maskC = distance(controlPos, vec2(cornerRadius,shrunkSize[1]))-(cornerRadius-1.0);
	float maskD = distance(controlPos, shrunkSize)-(cornerRadius-1.0);
	float cornerOnlyMask = min(min(maskA,maskB),min(maskC,maskD));

	float maskE = min(1.0-(controlPos[1]+1.0-controlSize[1]), controlPos[1]) * ((controlPos[0]>cornerRadius && controlPos[0]<shrunkSize[0])?1.0:0.0);
	float maskF = min(1.0-(controlPos[0]+1.0-controlSize[0]), controlPos[0]) * ((controlPos[1]>cornerRadius && controlPos[1]<shrunkSize[1])?1.0:0.0);
	float edgeOnlyMask = min(1.0-maskE, 1.0-maskF);

	return 1.0-clamp(min(cornerOnlyMask, edgeOnlyMask),0.0,1.0);
}

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