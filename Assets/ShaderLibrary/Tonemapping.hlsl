#ifndef MYRP_TONEMAPPING_INCLUDED
	#define MYRP_TONEMAPPING_INCLUDED
	
	CBUFFER_START(MyToneMapping)
	float4 cb3_v0;
	float4 cb3_v1;
	float4 cb3_v2;
	float4 cb3_v3;
	float4 cb3_v4;
	float4 cb3_v5;
	float4 cb3_v6;
	float4 cb3_v7;
	float4 cb3_v8;
	float4 cb3_v9;
	float4 cb3_v10;
	float4 cb3_v11;
	float4 cb3_v12;
	float4 cb3_v13;
	float4 cb3_v14;
	float4 cb3_v15;
	float4 cb3_v16, cb3_v17;
	CBUFFER_END
	
	TEXTURE2D(HDRColorTex);
	TEXTURE2D(AvgLuminanceTex);
	
	struct VertexInput
	{
		float4 pos: POSITION;
	};
	
	struct VertexOutput
	{
		float4 clipPos: SV_POSITION;
		float2 uv: TEXCOORD0;
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
	
	float4 TonemappingSimpleFrag(VertexOutput i): SV_TARGET
	{
		float avgLuminance = AvgLuminanceTex.Load(int3(0, 0, 0));
		avgLuminance = clamp(avgLuminance, cb3_v4.y, cb3_v4.z);
		avgLuminance = max(avgLuminance, 1e-4);
		
		float scaledWhitePoint = cb3_v16.x * 11.2;
		
		float luma = avgLuminance / scaledWhitePoint;
		luma = pow(luma, cb3_v16.z);
		
		luma = luma * scaledWhitePoint;
		luma = cb3_v16.x / luma;
		
		float3 HDRColor = HDRColorTex.Load(uint3(i.xy, 0)).rgb;
		
		float3 color = ToneMapU2Func(cb3_v7.x, cb3_v7.y, cb3_v7.z, cb3_v8.x, cb3_v8.y,
		cb3_v8.z, luma * HDRColor, cb3_v16.y);
		
		return float4(color, 1);
	}
	
	float4 TonemappingLerpFrag(VertexOutput i): SV_TARGET
	{
		float avgLuminance = AvgLuminanceTex.Load(int3(0, 0, 0));
		
		float exposure1 = GetExposure(avgLuminance, cb3_v9.y, cb3_v9.z, cb3_v17.x, cb3_v17.z);
		float exposure2 = GetExposure(avgLuminance, cb3_v4.y, cb3_v4.z, cb3_v16.x, cb3_v16.z);
		
		float3 HDRColor = HDRColorTex.Load(int3(i.xy, 0)).rgb;
		
		float3 color1 = ToneMapU2Func(cb3_v11.x, cb3_v11.y, cb3_v11.z, cb3_v12.x, cb3_v12.y,
		cb3_v12.z, exposure1 * HDRColor, cb3_v17.y);
		
		float3 color2 = ToneMapU2Func(cb3_v7.x, cb3_v7.y, cb3_v7.z, cb3_v8.x, cb3_v8.y,
		cb3_v8.z, exposure2 * HDRColor, cb3_v16.y);
		
		float3 finalColor = lerp(color2, color1, cb3_v13.x);
		return float4(finalColor, 1);
	}
	
#endif //MYRP_TONEMAPPING_INCLUDED
