shader_type canvas_item;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float circleMask = 1.0-((distance(UV, vec2(0.5))*2.0)-0.99)*100.0;
	float topMask = 1.0-((UV.y-0.5)*100.0);
	COLOR.a*=clamp(circleMask, 0, 1)+clamp(topMask, 0, 1);
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
