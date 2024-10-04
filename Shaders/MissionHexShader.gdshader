shader_type canvas_item;
#include "Includes/AdaptToAspect.gdshaderinc"
uniform sampler2D bgImage;
uniform sampler2D iconImage;

void fragment() {
	vec2 texRes=vec2(textureSize(bgImage,0));
	vec2 adaptedUV = adaptToAspect(vec2(1.0), vec2(0.5),texRes,UV,false);

	COLOR.rgb=texture(bgImage, adaptedUV).rgb*0.8;
	vec4 iconVal=texture(iconImage, vec2(-0.25)+(UV*1.5));
	COLOR.rgb = mix(COLOR.rgb, iconVal.rgb, iconVal.a);
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
