#ifndef MYRP_POST_EFFECT_STACK_INCLUDED
	#define MYRP_POST_EFFECT_STACK_INCLUDED
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	float _ReinhardModifier;
	
	TEXTURE2D(_CameraColorTexture);
	SAMPLER(sampler_CameraColorTexture);
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	TEXTURE2D(_DepthTex);
	SAMPLER(sampler_DepthTex);
	
	float4 BlurSample(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
	{
		//GPUs会在同一时刻并行运行很多Fragment Shader，但是并不是一个pixel一个pixel去执行的，而是将其组织在2x2的一组pixels分块中，去并行执行
		//在2x2里面 ddx 就是 右边块 - 左边块    ddy 就是 下边块 减 上面块
		uv += float2(uOffset * ddx(uv.x), vOffset * ddy(uv.y));
		return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
	}
	
	float4 CopyPassFragment(VertexOutput input): SV_TARGET
	{
		return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
	}
	
	float4 BlurPassFragment(VertexOutput input): SV_TARGET
	{
		//因为图片设置了FilterMode.Bilinear 图片会自动双线性过滤
		
		float4 color = BlurSample(input.uv, 0.5, 0.5)
		+ BlurSample(input.uv, -0.5, 0.5)
		+ BlurSample(input.uv, 0.5, -0.5)
		+ BlurSample(input.uv, -0.5, -0.5);
		
		return float4(color.rgb * 0.25, 1);
	}
	
	float4 DepthStripesPassFragment(VertexOutput input): SV_TARGET
	{
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_DepthTex, sampler_DepthTex, input.uv);
		float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
		float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
		#if UNITY_REVERSED_Z
			bool hasDepth = rawDepth > 0.00001;
		#else
			bool hasDepth = rawDepth < 0.99999;
		#endif
		if (hasDepth)
		{
			color *= pow(sin(3.14 * depth), 2.0);
		}
		return color;
	}
	
	float4 ToneMappingPassFragment(VertexOutput input): SV_TARGET
	{
		float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
		color *= (1 + color * _ReinhardModifier) / (1 + color);
		return float4(saturate(color), 1);
	}
	
	float4 LuminancePassFragment(VertexOutput input): SV_TARGET
	{
		float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
		
		color = dot(float3(0.2125, 0.7154, 0.0721), color);
		
		return float4(color, 1);
	}
	
#endif // MYRP_POST_EFFECT_STACK_INCLUDED