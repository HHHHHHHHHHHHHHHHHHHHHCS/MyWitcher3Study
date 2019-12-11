#ifndef MYRP_CHROMATIC_ABERRATION
	#define MYRP_CHROMATIC_ABERRATION
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"

	CBUFFER_START(MyChromaticAberration)
	float2 caCenter;
	float4 caCustomData;
	CBUFFER_END
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	float4 ChromaticAberrationPassFragment(VertexOutput input): SV_TARGET
	{
		//User Data
		float2 center = caCenter;
		float centerDistanceThreshold = caCustomData.x;
		float fa = caCustomData.y;
		float intensity = caCustomData.z;
		float distortSize = caCustomData.w;
		
		//Calculate Vector
		float2 offset = input.uv - center;
		offset = offset / center;
		
		//Length
		float offsetLength = length(offset);
		float offsetLengthFixed = offsetLength - centerDistanceThreshold;
		float texel = saturate(offsetLengthFixed * fa);
		
		float4 color = _MainTex.SampleLevel(sampler_MainTex, input.uv, 0);;
		float apply = (0.0 < texel);
		if (apply)
		{
			texel *= texel;
			texel *= distortSize;
			
			offsetLength = max(offsetLength, 0.0001);
			
			float multiplier = texel / offsetLength;
			
			offset *= multiplier;
			offset *= _ScreenParams.zw - float2(1, 1);
			offset *= intensity;
			
			float2 offsetUV = -offset * 2 + input.uv;
			color.r = _MainTex.SampleLevel(sampler_MainTex, offsetUV, 0).r;
			
			offsetUV = input.uv - offset;
			color.g = _MainTex.SampleLevel(sampler_MainTex, offsetUV, 0).g;
		}
		
		return color;
	}
	
#endif //MYRP_CHROMATIC_ABERRATION
