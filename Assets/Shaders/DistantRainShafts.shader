Shader "MyPipeline/DistantRainShafts"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" { }
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
			#pragma vertex DistantRainShaftsPassVertex
			#pragma fragment DistantRainShaftsPassFragment
			
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "../ShaderLibrary/PPSBase.hlsl"
			
			
			CBUFFER_START(UnityPerFrame)
			float4x4 unity_MatrixVP;
			CBUFFER_END
			
			CBUFFER_START(UnityPerDraw)
			float4x4 unity_ObjectToWorld, unity_WorldToObject;
			CBUFFER_END
			
			CBUFFER_START(DistantRainShafts)
			float3 _ModelScale, _MeshBias; //object xyz
			float2 _RainShaftsDepthCoefficents;
			float4 _RainShaftsUVAnimData;//.xy uv   .zw scale
			float2 _RainShaftsIntensityData;//.x minValue  .y maxValue
			float3 _RainShaftsColor;
			float2 _RainShaftsEffectAmount;// .x maxCoefficents   .y maskLerpValue
			float4 _RainShaftsFinalColor;
			
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
				float3 uvZ: TEXCOORD0;
				float4 positionH: TEXCOORD1;
			};
			
			TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);
			
			TEXTURE2D(_DepthTex);
			SAMPLER(sampler_DepthTex);
			
			RainShaftsVertexOutput DistantRainShaftsPassVertex(RainShaftsVertexInput v)
			{
				RainShaftsVertexOutput o = (RainShaftsVertexOutput)0;
				
				o.uvZ.xy = v.uv;
				
				float3 meshScale = _ModelScale.xyz;//float3(4,4,2)
				float3 meshBias = _MeshBias.xyz;//float3(-2,-2,-1)
				float3 positionL = v.pos.w * meshScale + meshBias;
				
				o.uvZ.z = mul(unity_ObjectToWorld, float4(positionL,1.0)).z;
				
				o.clipPos = mul(unity_MatrixVP, mul(UNITY_MATRIX_M, v.pos));
				
				o.positionH = mul(unity_MatrixVP, mul(UNITY_MATRIX_M, float4(positionL.xyz, 1.0)));
				
				return o;
			}
			
			float4 DistantRainShaftsPassFragment(RainShaftsVertexOutput i): SV_TARGET
			{
				float2 inputUV = i.uvZ.xy;
				float worldHeight = i.uvZ.z;
				
				float elapsedTime = _Time.y;
				float2 uvAnimation = _RainShaftsUVAnimData.xy;
				float2 uvScale = _RainShaftsUVAnimData.zw;
				float minValue = _RainShaftsIntensityData.x; //0.0
				float maxValue = _RainShaftsIntensityData.y; //1.0
				float3 shaftsColor = _RainShaftsColor.rgb; //float3(0.576471,0.635294,0.678431)
				
				float2 invViewportSize = _ScreenParams.zw - 1;
				
				
				float2 uvOffsets = elapsedTime * uvAnimation;
				float2 uv = inputUV * uvScale * uvOffsets;
				float disturb = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
				
				float intensity = saturate(lerp(minValue, maxValue, disturb));
				intensity *= inputUV.y;
				
				float2 screenUV = i.positionH.xy * invViewportSize;
				float hardwareDepth = SAMPLE_TEXTURE2D(_DepthTex, sampler_DepthTex, inputUV).r;
				hardwareDepth = LinearEyeDepth(hardwareDepth, _ZBufferParams);
				hardwareDepth = hardwareDepth * _RainShaftsDepthCoefficents.x + _RainShaftsDepthCoefficents.y;
				float frustumDepth = 1.0 / max(hardwareDepth, 1e-4);
				
				float depth = frustumDepth - worldHeight;
				//float depthScale = cb4_v6.x; //0.001
				float distantObjectsMask = saturate(depth /* *depthScale*/);
				
				float finalEffectMask = saturate(intensity * distantObjectsMask);
				
				float effectAmount = lerp(finalEffectMask, _RainShaftsEffectAmount.x * finalEffectMask, _RainShaftsEffectAmount.y);
				float3 effectColor = effectAmount * shaftsColor;
				
				//if gamma
				//effectColor = pow(effectColor, 2.2);
				
				effectColor *= _RainShaftsFinalColor.rgb;//float3(1.175,1.296,1.342)
				effectColor *= _RainShaftsFinalColor.a;
				
				//return zero alpha
				//srcColor * 1.0 + (1.0 - srcAlpha) * destColor
				return float4(effectColor, 0.0);
			}
			
			ENDHLSL
			
		}
	}
}
