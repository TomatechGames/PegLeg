shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
uniform sampler2D hexImage;

void fragment() {
	vec2 adaptedUV = adaptToAspect(vec2(1.0), vec2(0.5),1.0/TEXTURE_PIXEL_SIZE,UV,false);
	COLOR=texture(TEXTURE, adaptedUV);
	COLOR.rgb*0.8;
	COLOR.a= texture(hexImage, UV).r;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
