#ifndef MYRP_TONEMAPPING_INCLUDED
	#define MYRP_TONEMAPPING_INCLUDED
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"

	CBUFFER_START(MyEyeAdaptation)
	//.x/y 下降/上升的渐变速度
	float2 eyeAdaptationSpeedFactor;
	CBUFFER_END
	
	//色调映射 常用常熟 11.2
	//http://filmicworlds.com/blog/filmic-tonemapping-operators/
	
	CBUFFER_START(MyToneMapping)
	
	//mainColor .xy->允许的最小值/最大值亮度，
	float2 luminClamp;
	
	//mainColor .xyz->ToneMapU2Func曲线的ABC参数
	float3 curveABC;
	
	//mainColor .xyz->ToneMapU2Func曲线的DEF参数
	float3 curveDEF;
	
	//mainColor .x->某种“白标”或中间灰度  .y->u2分子乘数  .z->log/mul/exp指数
	float3 customData;
	
	//secondColor .xy->允许的最小值/最大值亮度，
	float2 luminClamp1;
	
	//secondColor .xyz->ToneMapU2Func曲线的ABC参数
	float3 curveABC1;
	
	//secondColor .xyz->ToneMapU2Func曲线的DEF参数
	float3 curveDEF1;
	
	//secondColor .x->某种“白标”或中间灰度  .y->u2分子乘数  .z->log/mul/exp指数
	float3 customData1;
	
	//mainColor&secondColor color lerp
	float colorLerp;
	CBUFFER_END
	
	TEXTURE2D(_HDRColorTex);
	SAMPLER(sampler_HDRColorTex);
	
	TEXTURE2D(_AvgLuminanceTex);
	SAMPLER(sampler_AvgLuminanceTex);
	
	TEXTURE2D(_PreviousAvgLuminanceTex);
	SAMPLER(sampler_PreviousAvgLuminanceTex);
	
	TEXTURE2D(_CurrentAvgLuminanceTex);
	SAMPLER(sampler_CurrentAvgLuminanceTex);
	
	float3 U2Func(float A, float B, float C, float D, float E, float F, float3 color)
	{
		//比例缩放 - 阀值
		return((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
	}
	
	//颜色映射到0-1
	float3 ToneMapU2Func(float A, float B, float C, float D, float E, float F, float3 color, float numMultiplier)
	{
		float3 numerator = U2Func(A, B, C, D, E, F, color);
		numerator = max(numerator, 0);
		numerator.rgb *= numMultiplier;
		
		float3 denominator = U2Func(A, B, C, D, E, F, 11.2);
		denominator = max(denominator, 0);
		
		return numerator / denominator;
	}
	
	//得到曝光系数
	float GetExposure(float avgLuminance, float minLuminance, float maxLuminance, float middleGray, float powParam)
	{
		avgLuminance = clamp(avgLuminance, minLuminance, maxLuminance);
		avgLuminance = max(avgLuminance, 1e-4);
		
		float scaledWhitePoint = middleGray * 11.2;
		
		float luma = avgLuminance / scaledWhitePoint;
		luma = pow(luma, powParam);
		
		float exposure = middleGray / (luma * scaledWhitePoint);
		return exposure;
	}

	float4 EyeAdaptationPassFrag(VertexOutput i): SV_TARGET
	{
		float previousAvgLuminance = _PreviousAvgLuminanceTex.SampleLevel(sampler_PreviousAvgLuminanceTex, float2(0.5, 0.5), 0).r;
		float currentAvgLuminance = _CurrentAvgLuminanceTex.SampleLevel(sampler_CurrentAvgLuminanceTex, float2(0.5, 0.5), 0).r;
		
		//根据正/负  用不同的 渐变速度
		float adaptationSpeedFactor = (currentAvgLuminance <= previousAvgLuminance) ? eyeAdaptationSpeedFactor.x: eyeAdaptationSpeedFactor.y;
		
		adaptationSpeedFactor = saturate(adaptationSpeedFactor);
		
		float adaptedLuminance = lerp(previousAvgLuminance, currentAvgLuminance, adaptationSpeedFactor);
		return adaptedLuminance;
	}
	
	float4 TonemappingSimplePassFrag(VertexOutput input): SV_TARGET
	{
		float avgLuminance = SAMPLE_TEXTURE2D(_AvgLuminanceTex, sampler_AvgLuminanceTex, float2(0.5, 0.5)).r;
		
		avgLuminance = clamp(avgLuminance, luminClamp.x, luminClamp.y);
		avgLuminance = max(avgLuminance, 1e-4);
		
		float scaledWhitePoint = customData.x * 11.2;
		
		float luma = avgLuminance / scaledWhitePoint;
		luma = pow(luma, customData.z);
		
		luma = luma * scaledWhitePoint;
		luma = customData.x / luma;
		
		
		float3 HDRColor = SAMPLE_TEXTURE2D(_HDRColorTex, sampler_HDRColorTex, input.uv).rgb;
		
		float3 color = ToneMapU2Func(curveABC.x, curveABC.y, curveABC.z, curveDEF.x, curveDEF.y,
		curveDEF.z, luma * HDRColor, customData.y);
		
		return float4(color, 1);
	}
	
	float4 TonemappingLerpPassFrag(VertexOutput input): SV_TARGET
	{
		float avgLuminance = SAMPLE_TEXTURE2D(_AvgLuminanceTex, sampler_AvgLuminanceTex, float2(0.5, 0.5)).r;
		
		float exposure1 = GetExposure(avgLuminance, luminClamp1.x, luminClamp1.y, customData1.x, customData1.z);
		float exposure2 = GetExposure(avgLuminance, luminClamp.x, luminClamp.y, customData.x, customData.z);
		
		float3 HDRColor = SAMPLE_TEXTURE2D(_HDRColorTex, sampler_HDRColorTex, input.uv).rgb;
		
		float3 color1 = ToneMapU2Func(curveABC1.x, curveABC1.y, curveABC1.z, curveDEF1.x, curveDEF1.y,
		curveDEF1.z, exposure1 * HDRColor, customData1.y);
		
		float3 color2 = ToneMapU2Func(curveABC.x, curveABC.y, curveABC.z, curveDEF.x, curveDEF.y,
		curveDEF.z, exposure2 * HDRColor, customData.y);
		
		float3 finalColor = lerp(color2, color1, colorLerp.x);
		return float4(finalColor, 1);
	}
	
#endif //MYRP_TONEMAPPING_INCLUDED
