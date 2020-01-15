Shader "MyPipeline/MoonLunarPhases"
{
	Properties
	{
		_MoonAlphaTex ("Alpha Map", 2D) = "white" { }
		_MoonNormalTex ("Normal Map", 2D) = "bump" { }
		_MooNowDay ("Moon Now Day", float) = 0
		_MoonDayBias ("Moon Day Bias", float) = 0
		_MoonColor ("Moon Color", Color) = (0.75, 0.75, 0.75, 1)
		[HDR]_MoonGlowColor ("Moon Glow Color", Color) = (0.25, 0.25, 0.25)
	}
	
	SubShader
	{
		Pass
		{
			//Tags { "LightMode" = "MoonOnly" }
			
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
			//一个月的长度
			#define SYNODIC_MONTH_LENGTH 30
			
			#pragma vertex MoonPassVertex
			#pragma fragment MoonPassFragment
			
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			
			
			CBUFFER_START(UnityPerFrame)
			float4x4 unity_MatrixV;
			float4x4 glstate_matrix_projection;
			
			float4x4 unity_MatrixVP;
			CBUFFER_END
			
			CBUFFER_START(UnityPerDraw)
			float4x4 unity_ObjectToWorld, unity_WorldToObject;
			CBUFFER_END
			
			CBUFFER_START(MyMoonLunarPhases)
			float _MooNowDay, _MoonDayBias;
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
			
			TEXTURE2D(_MoonAlphaTex);
			SAMPLER(sampler_MoonAlphaTex);
			
			TEXTURE2D(_MoonNormalTex);
			SAMPLER(sampler_MoonNormalTex);
			
			VertexOutput MoonPassVertex(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
//				float4 worldPos = mul(UNITY_MATRIX_M, float4(v.pos.xyz, 1.0));
//				unity_MatrixV[0].w = unity_MatrixV[1].w = unity_MatrixV[2].w = 0;
//				o.clipPos = mul(unity_MatrixV, worldPos);
//				o.clipPos = mul(glstate_matrix_projection, o.clipPos);
				o.clipPos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.pos.xyz, 1.0)));
				o.clipPos.z = 1;
				#if UNITY_UV_STARTS_AT_TOP
					o.clipPos.z = 0;
				#endif

				o.uv = v.uv;
				o.normal = v.normal.xyz;
				o.tangent = v.tangent.xyz;
				o.binormal = cross(v.normal.xyz, v.tangent.xyz) * v.tangent.w;
				return o;
			}
			
			float4 MoonPassFragment(VertexOutput i): SV_TARGET
			{
				float2 uvOffsets = float2(-_MoonDayBias, 0.0);
				float2 uv = i.uv + uvOffsets;
				
				float4 moonNormal = SAMPLE_TEXTURE2D(_MoonNormalTex, sampler_MoonNormalTex, uv);
				float3 sampledNormal = normalize((moonNormal.xyz - 0.5) * 2);
				
				float3 Tangent = normalize(i.tangent.xyz);
				float3 Normal = normalize(i.normal.xyz);
				float3 Bitangent = normalize(i.binormal.xyz);
				
				float3x3 TBN = float3x3(Tangent, Bitangent, Normal);
				
				float2 vNormal = mul(sampledNormal, (float3x2)TBN).xy;
				
				float phase = _MooNowDay * (1.0 / SYNODIC_MONTH_LENGTH) + _MoonDayBias;
				
				phase *= TWO_PI;
				
				float outSin = 0.0;
				float outCos = 0.0;
				sincos(phase, outSin, outCos);
				float lunarPhase = saturate(dot(vNormal, float2(outCos, outSin)));
				
				float3 moonColor = lunarPhase * _MoonGlowColor.xyz;
				
				float moonColorA = SAMPLE_TEXTURE2D(_MoonAlphaTex, sampler_MoonAlphaTex, uv).r;
				moonColorA = pow(abs(moonColorA), 2.2);
				moonColor = moonColorA * moonColor;
				moonColor *= _MoonColor.rgb;
				moonColor *= _MoonColor.a;
				
				return float4(moonColor, 1.0);
			}
			
			ENDHLSL
			
		}
	}
}
