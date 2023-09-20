#ifndef __LUMINANCE_CGINC__
#define __LUMINANCE_CGINC__

float Luminance(float3 c) {
	return dot(c, float3(0.3126, 0.7152, 0.0722));
}
float Lightness(float3 c) {
	return dot(c, 1.0/3);
}
#endif
