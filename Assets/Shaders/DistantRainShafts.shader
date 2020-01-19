Shader "MyPipeline/DistantRainShafts"
{
	Properties
	{
		_NoiseTex ("_NoiseTex", 2D) = "white" { }
		_RainShaftsUVAnimData ("Rain Shafts UV AnimData", Vector) = (0, 0, 1, 1)
		_RainShaftsIntensityData ("Rain Shafts Intensity Data", Vector) = (0, 1, 0, 0)
		_RainShaftsColor ("Rain Shafts Color", Color) = (1, 1, 1)
		_RainShaftsEffectAmount ("Rain Shafts Effect Amount", Vector) = (1, 0.5, 0, 0)
		[HDR]_RainShaftsFinalColor ("Rain Shafts Final Color", Color) = (1, 1, 1)
	}
	SubShader
	{
		Cull  Off
		
		Pass
		{
			//Tags { "LightMode" = "MoonOnly1" }
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex DistantRainShaftsPassVertex
			#pragma fragment DistantRainShaftsPassFragment
			
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
			
			struct RainShaftsVertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			
			struct RainShaftsVertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
			};
			
			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);
			
			float4 _RainShaftsUVAnimData;//.xy uv   .zw scale
			float2 _RainShaftsIntensityData;//.x minValue  .y maxValue
			float3 _RainShaftsColor;
			float2 _RainShaftsEffectAmount;// .x maxCoefficents   .y maskLerpValue
			float4 _RainShaftsFinalColor;// .rgb color   .a intensity
			
			RainShaftsVertexOutput DistantRainShaftsPassVertex(RainShaftsVertexInput v)
			{
				RainShaftsVertexOutput o = (RainShaftsVertexOutput)0;
				
//				unity_MatrixV[0].w = unity_MatrixV[1].w = unity_MatrixV[2].w = 0;
//				float4x4 mvp = mul(glstate_matrix_projection, mul(unity_MatrixV, UNITY_MATRIX_M));
//				o.clipPos = mul(mvp, v.pos);

				o.clipPos = mul(unity_MatrixVP , mul(unity_ObjectToWorld , float4(v.pos.xyz , 1.0)));
				o.clipPos.z = 1;
				#if UNITY_UV_STARTS_AT_TOP
					o.clipPos.z = 0;
				#endif
				o.uv.xy = v.uv;
				

				
				return o;
			}
			
			float4 DistantRainShaftsPassFragment(RainShaftsVertexOutput i): SV_TARGET
			{
				float2 inputUV = i.uv.xy;
				
				float elapsedTime = _Time.y;
				float2 uvAnimation = _RainShaftsUVAnimData.xy;
				float2 uvScale = _RainShaftsUVAnimData.zw;
				float minValue = _RainShaftsIntensityData.x; //0.0
				float maxValue = _RainShaftsIntensityData.y; //1.0
				
				float2 uvOffsets = elapsedTime * uvAnimation;
				float2 uv = inputUV * uvScale + uvOffsets;
				float disturb = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
				
				float intensity = saturate(lerp(minValue, maxValue, disturb));
				intensity *= inputUV.y;
				
				float finalEffectMask = saturate(intensity);
				
				float effectAmount = lerp(finalEffectMask, _RainShaftsEffectAmount.x * finalEffectMask, _RainShaftsEffectAmount.y);
				float3 shaftsColor = _RainShaftsColor.rgb; //float3(0.576471,0.635294,0.678431)
				float3 effectColor = effectAmount * shaftsColor;
				
				//if gamma
				//effectColor = pow(effectColor, 2.2);
				
				effectColor *= _RainShaftsFinalColor.rgb;//float3(1.175,1.296,1.342)
				effectColor *= _RainShaftsFinalColor.a;
				
				//return zero alpha  but I don't do that
				//srcColor * 1.0 + (1.0 - srcAlpha) * destColor
				return float4(effectColor, 0.0);
			}
			
			ENDHLSL
			
		}
	}
}
