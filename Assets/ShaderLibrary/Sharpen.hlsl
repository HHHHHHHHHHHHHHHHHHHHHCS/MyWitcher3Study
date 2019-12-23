#ifndef MYRP_Vignette
	#define MYRP_Vignette
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	CBUFFER_START(MySharpen)
	// High settings:
	float4  g_Viewport;
	
	// 2.0, 1.80, 0.025, -0.25
	float   sharpenNear;
	float   sharpenFar;
	float   sharpenDistanceScale;
	float   sharpenDistanceBias;
	float   sharpenLumScale;
	float   sharpenLumBias;
	float2  pad001;
	CBUFFER_END
	
	static const float3 LUMINANCE_RGB = float3(0.2126, 0.7152, 0.0722);
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	TEXTURE2D(_DepthTex);
	SAMPLER(sampler_DepthTex);
	
	float4 SharpenFragment(VertexOutput input): SV_TARGET
	{
		
		float fDepth = _DepthTex.sample(sampler_DepthTex, input.uv);
		
		float fScaleDepth = LinearEyeDepth(fDepth, _ZBufferParams);
		
		float fNearFarSharpenMask = saturate(fScaleDepth * sharpenDistanceScale + sharpenDistanceBias);
		
		float fSharpenIntensity = lerp(sharpenNear, sharpenFar, fNearFarSharpenMask);
		
		fSharpenIntensity += 1.0;
	}
	
	
#endif //MYRP_Vignette
