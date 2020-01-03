Shader "MyPipeline/MoonLunarPhases"
{
	SubShader
	{
		Pass
		{
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
			//一个月的长度
			#define SYNODIC_MONTH_LENGTH 30
			
			#pragma vertex MoonPassVertex
			#pragma fragment MoonPassFragment
			
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			
			
			CBUFFER_START(UnityPerFrame)
			float4x4 unity_MatrixVP;
			CBUFFER_END
			
			CBUFFER_START(UnityPerDraw)
			float4x4 unity_ObjectToWorld, unity_WorldToObject;
			CBUFFER_END
			
			CBUFFER_START(MyMoonLunarPhases)
			//.x day  .y uv/dayBias
			float2 _MoonDayData;
			float4 _MoonColor;
			float3 _MoonGlowColor;
			CBUFFER_END
			
			#define UNITY_MATRIX_M unity_ObjectToWorld
			#define UNITY_MATRIX_I_M unity_WorldToObject
			
			struct VertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: NORMAL;
				float4 tangent: TANGENT;
			};
			
			
			struct VertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 normal: TEXCOORD1;
				float3 tangent: TEXCOORD2;
				float3 binormal: TEXCOORD3;
			};
			
			VertexOutput MoonPassVertex(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
				float4 worldPos = mul(UNITY_MATRIX_M, float4(v.pos.xyz, 1.0));
				o.clipPos = mul(unity_MatrixVP, worldPos);
				o.uv = v.uv;
				o.normal = v.normal;
				o.tangent = v.tangent;
				o.binormal = cross(v.normal, v.tangent) * v.tangent.w;
				return o;
			}
			
			float4 MoonPassFragment(VertexOutput i): SV_TARGET
			{
				float2 uvOffsets = float2(-_MoonDayData.y, 0.0);
				float2 uv = i.uv + uvOffsets;
				
				float4 moonNormal = _MoonColorTex.Sample(sampler0er, uv);
				float3 sampledNormal = normalize((moonNormal.xyz - 0.5) * 2);
				
				float3 Tangent = normalize(i.tangent.xyz);
				float3 Normal = normalize(i.normal.xyz);
				float3 Bitangent = normalize(i.binormal.xyz);
				
				float3x3 TBN = float3x3(Tangent, Bitangent, Normal);
				
				float2 vNormal = mul(sampledNormal, (float3x2)TBN).xy;
				
				float phase = _MoonDayData.x * (1.0 / SYNODIC_MONTH_LENGTH) + _MoonDayData.y;
				
				phase *= TWO_PI;
				
				float outSin = 0.0;
				float outCos = 0.0;
				sincos(phase, outSin, outCos);
				float lunarPhase = saturate(dot(vNormal, float2(outCos, outSin)));
				
				float3 moonColor = lunarPhase * _MoonGlowColor.xyz;
				float moonColorA = pow(moonNormal.a, 2.2);
				moonColor = moonColorA * moonColor;
				moonColor *= _MoonColor.rgb;
				
				float paramHorizon = saturate(1.0 - IN.param1.w);
				paramHorizon *= _MoonColor.a;
				moonColor *= paramHorizon;
				
				return float4(moonColor, 0.0);
				
				return 0;
			}
			
			ENDHLSL
			
		}
	}
}
