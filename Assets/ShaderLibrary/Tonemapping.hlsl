#ifndef MYRP_TONEMAPPING_INCLUDED
	#define MYRP_TONEMAPPING_INCLUDED
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	
	
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
	
	TEXTURE2D(_AvgLuminanceTex);
	SAMPLER(sampler_AvgLuminanceTex);
	
	struct VertexInput
	{
		float4 pos: POSITION;
	};
	
	struct VertexOutput
	{
		float4 clipPos: SV_POSITION;
	};
	
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
	
	VertexOutput TonemappingVert(VertexInput i)
	{
		VertexOutput o;
		o.clipPos = float4(i.pos.xy, 0.0, 1.0);
		return o;
	}
	
	float4 TonemappingSimpleFrag(VertexOutput i): SV_TARGET
	{
		float avgLuminance = SAMPLE_TEXTURE2D(_AvgLuminanceTex, sampler_AvgLuminanceTex, float2(0.5, 0.5));

		avgLuminance = clamp(avgLuminance, luminClamp.x, luminClamp.y);
		avgLuminance = max(avgLuminance, 1e-4);
		
		float scaledWhitePoint = customData.x * 11.2;
		
		float luma = avgLuminance / scaledWhitePoint;
		luma = pow(luma, customData.z);
		
		luma = luma * scaledWhitePoint;
		luma = customData.x / luma;
		
		float3 HDRColor = _HDRColorTex.Load(uint3(i.clipPos.xy, 0)).rgb;
		
		float3 color = ToneMapU2Func(curveABC.x, curveABC.y, curveABC.z, curveDEF.x, curveDEF.y,
		curveDEF.z, luma * HDRColor, customData.y);
		
		return float4(color, 1);
	}
	
	float4 TonemappingLerpFrag(VertexOutput i): SV_TARGET
	{
		float avgLuminance = SAMPLE_TEXTURE2D(_AvgLuminanceTex, sampler_AvgLuminanceTex, float2(0.5, 0.5));
		
		float exposure1 = GetExposure(avgLuminance, luminClamp1.x, luminClamp1.y, customData1.x, customData1.z);
		float exposure2 = GetExposure(avgLuminance, luminClamp.x, luminClamp.y, customData.x, customData.z);
		
		float3 HDRColor = _HDRColorTex.Load(int3(i.clipPos.xy, 0)).rgb;
		
		float3 color1 = ToneMapU2Func(curveABC1.x, curveABC1.y, curveABC1.z, curveDEF1.x, curveDEF1.y,
		curveDEF1.z, exposure1 * HDRColor, customData1.y);
		
		float3 color2 = ToneMapU2Func(curveABC.x, curveABC.y, curveABC.z, curveDEF.x, curveDEF.y,
		curveDEF.z, exposure2 * HDRColor, customData.y);
		
		float3 finalColor = lerp(color2, color1, colorLerp.x);
		return float4(finalColor, 1);
	}
	
#endif //MYRP_TONEMAPPING_INCLUDED
