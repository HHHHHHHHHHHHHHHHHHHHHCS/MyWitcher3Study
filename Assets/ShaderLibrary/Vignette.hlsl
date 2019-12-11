#ifndef MYRP_Vignette
	#define MYRP_Vignette
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	CBUFFER_START(MyChromaticAberration)
	float vignetteSimpleIntensity;
	float vignetteSimpleThreshold;

	float vignetteeComplexIntensity;
	float3 vignetteComplexWeights;
	float3 vignetteComplexDarkColor;
	CBUFFER_END
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	TEXTURE2D(_VignetteComplexMaskTex);
	SAMPLER(sampler_VignetteComplexMaskTex);
	
	float4 VignetteSimpleFragment(VertexOutput input): SV_TARGET
	{
		float4 color = _MainTex.SampleLevel(sampler_MainTex, input.uv, 0);
		
		float distanceFromCenter = length(input.uv - float2(0.5, 0.5));
		
		float x = distanceFromCenter * 2.0 - vignetteSimpleThreshold;
		x = saturate(x * vignetteIntensity);
		
		float x2 = x * x;
		float x3 = x2 * x;
		float x4 = x2 * x2;
		
		float outX = dot(float4(x4, x3, x2, x), float4(-0.1, -0.105, 1.12, 0.09));
		outX = min(outX, 0.94);
		
		outX = 1 - outX;
		
		return float4(color.rgb * outX, color.a);
	}
	
	float4 VignetteComplexFragment(VertexOutput input): SV_TARGET
	{
		float4 color = _MainTex.SampleLevel(sampler_MainTex, input.uv, 0);
		
		//calc weight
		float vignetteWeight = dot(color.rgb, vignetteComplexWeights);
		
		//oneMinus and clamp[0-1]
		vignetteWeight = saturate(1.0 - vignetteWeight);
		
		//mul by opacity
		vignetteWeight *= vignetteIntensity;
		
		//get Mask
		float sampledVignetteMask = _VignetteComplexMaskTex.Sample(sampler_VignetteComplexMaskTex, input.uv).x;
		
		//calcMask
		float finalInvVignetteMask = saturate(vignetteWeight * sampledVignetteMask);
		
		//calc endColor
		color.rgb = lerp(color.rgb, vignetteComplexDarkColor, finalInvVignetteMask);
		
		return color;
	}
	
#endif //MYRP_Vignette
