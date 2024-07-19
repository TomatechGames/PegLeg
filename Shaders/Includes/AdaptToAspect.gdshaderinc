vec2 adaptToAspect(vec2 controlSize, vec2 shiftDirection, vec2 textureResolution, vec2 uv, bool fit)
{
	float horizontalRatio = textureResolution.x/controlSize.x;
	float verticalRatio = textureResolution.y/controlSize.y;
	float ratio = fit?max(horizontalRatio,verticalRatio):min(horizontalRatio,verticalRatio);
	vec2 scaledResolution = textureResolution/ratio;
	
	//vec2 scaledShift = clamp(shiftDirection, vec2(0),vec2(1)) * (controlSize-scaledResolution);
	vec2 temp1 = (controlSize-scaledResolution)*0.5;
	vec2 temp2 = (uv*controlSize)-temp1;
	//vec2 scaledUV = uv*controlSize;
	return temp2/scaledResolution;
	//return (scaledUV-scaledShift)/scaledResolution;
}