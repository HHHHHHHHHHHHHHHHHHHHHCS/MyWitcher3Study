﻿Shader "MyPipeline/Lightnings"
{
	Properties
	{
		_LigtningsExplosion ("Ligtnings Explosion", Float) = 0.01
		_AnimationSpeed ("Animation Speed", Float) = 1.0
		_NoiseMin ("Noise Min", Float) = 0.0
		_NoiseMax ("Noise Max", Float) = 1.0
		_NoiseAmount ("Noise Amount", Float) = 1.0
		_ColorFilter ("Color Filter", Color) = (1.0, 1.0, 1.0, 1.0)
		_LightningColorRGB ("Lightning Color RGB", Color) = (1.0, 1.0, 1.0, 1.0)
	}
	SubShader
	{
		Pass
		{
			//Tags { "LightMode" = "MoonOnly" }
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			//#pragma multi_compile_instancing

			#pragma vertex LightningsPassVertex
			#pragma fragment LightningsPassFragment
			
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "../ShaderLibrary/PPSBase.hlsl"
			
			
			CBUFFER_START(UnityPerFrame)
			float4x4 unity_MatrixV;
			float4x4 glstate_matrix_projection;
			
			float4x4 unity_MatrixVP;
			CBUFFER_END
			
			CBUFFER_START(UnityPerDraw)
			float4x4 unity_ObjectToWorld, unity_WorldToObject;
			CBUFFER_END
			
			
			#define UNITY_MATRIX_M unity_ObjectToWorld
			#define UNITY_MATRIX_I_M unity_WorldToObject
			
			struct LightningsVertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float3 normal: NORMAL;
			};
			
			
			struct LightningsVertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
			};
			
			float _LigtningsExplosion;
			float _AnimationSpeed;
			float _NoiseMin;
			float _NoiseMax;
			float _NoiseAmount;
			float4 _ColorFilter;
			float4 _LightningColorRGB;
			
			
			LightningsVertexOutput LightningsPassVertex(LightningsVertexInput v)
			{
				LightningsVertexOutput o = (LightningsVertexOutput)0;
				
				//unity_MatrixV[0].w = unity_MatrixV[1].w = unity_MatrixV[2].w = 0;
				//float4x4 mvp = mul(glstate_matrix_projection, mul(unity_MatrixV, UNITY_MATRIX_M));
				//o.clipPos = mul(mvp, v.pos);
				
				v.pos.xyz += v.normal * _LigtningsExplosion;
				float4 worldPos = mul(unity_ObjectToWorld, float4(v.pos.xyz, 1.0));
				o.clipPos = mul(unity_MatrixVP, worldPos);
				o.clipPos.z = 1;
				#if UNITY_UV_STARTS_AT_TOP
					o.clipPos.z = 0;
				#endif
				o.uv.xy = v.uv;

				return o;
			}
			
			
			float4 LightningsPassFragment(LightningsVertexOutput i): SV_TARGET
			{
				float animation = _Time.y * _AnimationSpeed + i.uv.x;
				
				int intX0 = asint(floor(animation));
				int intX1 = asint(floor(animation - 1.0));
				
				float n0 = IntegerNoise(intX0);
				float n1 = IntegerNoise(intX1);
				
				float weight = 1.0 - frac(animation);
				
				float noise = lerp(n0, n1, SCurve(weight));
				
				float lightningAmount = saturate(lerp(_NoiseMin, _NoiseMax, noise));
				lightningAmount *= _NoiseAmount;
				
				float3 lightningColor = _ColorFilter.a * _ColorFilter.rgb;
				lightningColor *= _LightningColorRGB.rgb;
				
				float3 finalLightningColor = lightningColor * lightningAmount;
				return float4(finalLightningColor, lightningAmount);
			}
			
			ENDHLSL
			
		}
	}
}
