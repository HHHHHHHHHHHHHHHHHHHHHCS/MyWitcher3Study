#ifndef MYRP_EYEADAPTATION_INCLUDED
	#define MYRP_EYEADAPATION_INCLUDED
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	
	
	CBUFFER_START(MyEyeAdaptation)
	//.x/y 下降/上升的渐变速度
	float2 speedFactor;
	CBUFFER_END
	
	
	TEXTURE2D(texPreviousAvgLuminance);
	SAMPLER(sampler_texPreviousAvgLuminance);
	
	TEXTURE2D(texCurrentAvgLuminance);
	SAMPLER(sampler_texCurrentAvgLuminance);
	
	float4 EyeApaptationVert(float4 pos: POSITION): SV_POSITION
	{
		return UnityObjectToClipPos(pos);
	}
	
	float4 EyeAdaptationFrag(float pos): SV_POSITION
	{
		float currentAvgLuminance = texPreviousAvgLuminance.SampleLevel(sampler_texPreviousAvgLuminance, float2(0.5, 0.5), 0);
		float previousAvgLuminance = texCurrentAvgLuminance.SampleLevel(sampler_texCurrentAvgLuminance, float2(0.5, 0.5), 0);
		
		//根据正/负  用不同的 渐变速度
		float adaptationSpeedFactor = (currentAvgLuminance <= previousAvgLuminance) ? speedFactor.x: speedFactor.y;
		
		adaptationSpeedFactor = saturate(adaptationSpeedFactor);
		
		float adaptedLuminance = lerp(previousAvgLuminance, currentAvgLuminance, adaptationSpeedFactor);
		return adaptedLuminance;
	}
	
#endif // MYRP_EYEADAPTATION_INCLUDED