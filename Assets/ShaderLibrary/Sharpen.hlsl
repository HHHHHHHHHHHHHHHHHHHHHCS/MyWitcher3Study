#ifndef MYRP_Sharpen
	#define MYRP_Sharpen
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	CBUFFER_START(MySharpen)
	//.x sharpenNear  .y sharpenFar
	float2 sharpenNearFar;
	//.x/.y sharpenDistance Scale/Bias  .z/.w sharpenLuminance Scale/Bias
	float4 sharpenDistLumScaleBias;
	CBUFFER_END
	
	static const float3 LUMINANCE_RGB = float3(0.2126, 0.7152, 0.0722);
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	TEXTURE2D(_DepthTex);
	SAMPLER(sampler_DepthTex);
	
	float4 SharpenFragment(VertexOutput input): SV_TARGET
	{
		/*Input Data*/
		float sharpenNear = sharpenNearFar.x;
		float sharpenFar = sharpenNearFar.y;
		float sharpenDistanceScale = sharpenDistLumScaleBias.x;
		float sharpenDistanceBias = sharpenDistLumScaleBias.y;
		float sharpenLumScale = sharpenDistLumScaleBias.z;
		float sharpenLumBias = sharpenDistLumScaleBias.w;
		
		/*Depth*/
		float fDepth = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, input.uv).r;
		
		float fScaleDepth = LinearEyeDepth(fDepth, _ZBufferParams);
		
		float fNearFarSharpenMask = saturate(fScaleDepth * sharpenDistanceScale + sharpenDistanceBias);
		
		//深度越大 锐化越弱
		float fSharpenIntensity = lerp(sharpenNear, sharpenFar, fNearFarSharpenMask);
		
		//用做锐化强度至少是1.0个
		fSharpenIntensity += 1.0;
		
		float fSkyboxTest = (fDepth >= 1.0)?0: 1;
		
		//fSharpenAmount基本大于1
		float fSharpenAmount = fSharpenIntensity * fSkyboxTest;
		
		/*Center UV*/
		
		float2 uvCenter = input.uv;
		
		float3 colorCenter = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvCenter).rgb;
		
		float3 finalColor = colorCenter;
		
		if (fSharpenAmount > 0.0)
		{
			/*Avg Color*/
			
			float2 uvOffset = float2(0.5, 0.5) / _ScreenParams.xy;
			
			float3 colorCorners = 0;
			
			// Top left corner
			// -0,5, -0.5
			float2 uvCorner = uvCenter - uvOffset;
			colorCorners += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvCorner).rgb;
			
			// Top right corner
			// +0.5, -0.5
			uvCorner = uvCenter + float2(uvOffset.x, -uvOffset.y);
			colorCorners += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvCorner).rgb;
			
			// Bottom left corner
			// -0.5, +0.5
			uvCorner = uvCenter + float2(uvOffset.x, -uvOffset.y);
			colorCorners += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvCorner).rgb;
			
			// Bottom right corner
			// +0.5, +0.5
			uvCorner = uvCenter + float2(-uvOffset.x, uvOffset.y);
			colorCorners += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvCorner).rgb;
			
			float3 averageColorCorners = colorCorners / 4.0;
			
			/*Diff Color*/
			
			float3 diffColor = colorCenter - averageColorCorners;
			
			float fDiffColorMaxComponent = max(abs(diffColor.x), max(abs(diffColor.y), abs(diffColor.z)));
			//luminance 系数
			float fDiffColorMaxComponentScaled = saturate(fDiffColorMaxComponent * sharpenLumScale + sharpenLumBias);
			
			/*lumiance*/
			//计算将锐化多少像素。
			//注意这里的“1.0”-这就是为什么我们在fsharpentensity之前添加了“1.0”。
			float fPixelShapenAmount = lerp(1.0, fSharpenAmount, fDiffColorMaxComponentScaled);
			
			float lumaCenter = dot(finalColor, LUMINANCE_RGB);
			float lumaCornersAverage = dot(averageColorCorners, LUMINANCE_RGB);
			
			/*Final Color*/
			
			//获得亮度平衡
			float3 fColorBalanced = colorCenter / max(lumaCenter, 1e-4);
			//修改亮度平衡
			float fPixelLuminance = lerp(lumaCornersAverage, lumaCenter, fPixelShapenAmount);
			//根据亮度平衡 得到锐化颜色
			finalColor = fColorBalanced * max(fPixelLuminance, 0.0);
		}
		
		return float4(finalColor, 1);
	}
	
	
#endif //MYRP_Sharpen
