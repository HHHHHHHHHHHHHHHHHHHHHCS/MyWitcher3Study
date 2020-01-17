Shader "MyPipeline/Lightnings"
{
	Properties { }
	SubShader
	{
		Cull  Off
		
		Pass
		{
			//Tags { "LightMode" = "MoonOnly1" }
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
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
			
			struct LightningsVertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float3 normal: TEXCOORD1;
			};
			
			
			struct LightningsVertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
			};
			
			float _LigtningsExplosion;
			
			
			LightningsVertexOutput LightningsPassVertex(LightningsVertexInput v)
			{
				LightningsVertexOutput o = (LightningsVertexOutput)0;
				
				//unity_MatrixV[0].w = unity_MatrixV[1].w = unity_MatrixV[2].w = 0;
				//float4x4 mvp = mul(glstate_matrix_projection, mul(unity_MatrixV, UNITY_MATRIX_M));
				//o.clipPos = mul(mvp, v.pos);
				
				v.pos.xyz += v.normal * _LigtningsExplosion;
				o.clipPos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.pos.xyz, 1.0)));
				o.clipPos.z = 1;
				#if UNITY_UV_STARTS_AT_TOP
					o.clipPos.z = 0;
				#endif
				o.uv.xy = v.uv;
				
				return o;
			}
			
			
			// Shaders in TW3 use integer noise.
			// For more details see: http://libnoise.sourceforge.net/noisegen/
			float IntegerNoise(int n)
			{
				n = (n >> 13) ^ n;
				int nn = (n * (n * n * 60493 + 19990303) + 1376312589) & 0x7fffffff;
				return((float)nn / 1073741824.0);
			}
			
			float SCurve(float x)
			{
				float x2 = x * x;
				float x3 = x2 * x;
				
				// -2x^3 + 3x^2
				return - 2.0 * x3 + 3.0 * x2;
			}
			
			float4 LightningsPassFragment(LightningsOutput i): SV_TARGET
			{
				// * Inputs
				float elapsedTime = cb0_v0.x;
				float animationSpeed = cb4_v4.x;
				
				float minAmount = cb4_v2.x;
				float maxAmount = cb4_v3.x;
				
				float colorMultiplier = cb4_v0.x;
				float3 colorFilter = cb4_v1.xyz;
				float3 lightningColorRGB = cb2_v2.rgb;
				
				float animation = elapsedTime * animationSpeed + INPUT.TEXCOORDS.x;
				
				int intX0 = asint(floor(animation));
				int intX1 = asint(floor(animation - 1.0));
				
				float n0 = IntegerNoise(intX0);
				float n1 = IntegerNoise(intX1);
				
				float weight = 1.0 - frac(animation);
				
				float noise = lerp(n0, n1, SCurve(weight));
				
				float lightningAmount = saturate(lerp(minAmount, maxAmount, noise));
				lightningAmount *= cb2_v2.w;
				
				float3 lightningColor = colorMultiplier * colorFilter;
				lightningColor *= lightningColorRGB;
				
				float3 finalLightningColor = lightningColor * lightningAmount;
				return float4(finalLightningColor, lightningAmount);
			}
			
			ENDHLSL
			
		}
	}
}
