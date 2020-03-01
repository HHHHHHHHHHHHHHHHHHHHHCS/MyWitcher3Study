Shader "MyPipeline/Sky"
{
	Properties
	{
		_SkyTex ("Sky Texture", 2D) = "black" { }
	}
	SubShader
	{
		Pass
		{
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex SkyPassVertex
			#pragma fragment SkyPassFragment
			
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
			
			CBUFFER_START(MySky)
			//.x.y sunDir .z sunExponent
			float3 _SunTheta;
			float4 _SunColor;
			float4 _StarsColor;
			CBUFFER_END
			
			#define UNITY_MATRIX_M unity_ObjectToWorld
			#define UNITY_MATRIX_I_M unity_WorldToObject
			
			struct SkyVertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float3 normal: NORMAL;
			};
			
			struct SkyVertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 worldToCamera: TEXCOORD1;
				float3 sunDir: TEXCOORD2;
			};
			
			TEXTURE2D(_SkyTex);
			SAMPLER(sampler_SkyTex);
			
			
			SkyVertexOutput SkyPassVertex(SkyVertexInput v)
			{
				SkyVertexOutput o = (SkyVertexOutput)0;
				
				float4 worldPos = mul(unity_ObjectToWorld, float4(v.pos.xyz, 1.0));
				o.clipPos = mul(unity_MatrixVP, worldPos);
				o.clipPos.z = 1;
				#if UNITY_UV_STARTS_AT_TOP
					o.clipPos.z = 0;
				#endif
				
				o.uv.xy = v.uv;
				o.worldToCamera = worldPos.xyz - _WorldSpaceCameraPos.xyz;
				
				float3 sunDir;
				sunDir.x = sin(_SunTheta.x) * cos(_SunTheta.y);
				sunDir.y = sin(_SunTheta.x) * sin(_SunTheta.y);
				sunDir.z = cos(_SunTheta.x) ;
				o.sunDir = normalize(sunDir);
				
				return o;
			}
			
			
			float GetNoise(float2 uv)
			{
				
				// * Inputs - UV and elapsed time in seconds
				float2 starsUV;
				starsUV.x = 500.0 * uv.x;
				starsUV.y = 500.0 * uv.y + _Time.y * 0.2;
				
				// * Iteration 1
				int iStars1_A = GetReverseInt(starsUV.y);
				int iStars1_B = GetInt(starsUV.x);
				
				float fStarsNoise1 = IntegerNoise(iStars1_A + iStars1_B);
				
				
				// * Iteration 2
				int iStars2_A = GetReverseInt(starsUV.y);
				int iStars2_B = GetInt(starsUV.x - 1.0);
				
				float fStarsNoise2 = IntegerNoise(iStars2_A + iStars2_B);
				
				
				// * Iteration 3
				int iStars3_A = GetReverseInt(starsUV.y - 1.0);
				int iStars3_B = GetInt(starsUV.x);
				
				float fStarsNoise3 = IntegerNoise(iStars3_A + iStars3_B);
				
				
				// * Iteration 4
				int iStars4_A = GetReverseInt(starsUV.y - 1.0);
				int iStars4_B = GetInt(starsUV.x - 1.0);
				
				float fStarsNoise4 = IntegerNoise(iStars4_A + iStars4_B);
				
				float noise = fStarsNoise1 + fStarsNoise2 + fStarsNoise3 + fStarsNoise4;
				
				return 0.25 * noise;
			}
			
			
			float4 SkyPassFragment(SkyVertexOutput i): SV_TARGET
			{
				float4 skyColor = SAMPLE_TEXTURE2D(_SkyTex, sampler_SkyTex, i.uv);
				
				i.worldToCamera = normalize(i.worldToCamera);
				float cosTheta = saturate(dot(i.sunDir, i.worldToCamera));
				float sunGradient = pow(cosTheta, _SunTheta.z);
				
				float noise = GetNoise(i.uv);
				
				//float weightX = 1 - frac(i.uv.x);
				//weightX = SCurve(weightX);
				
				float weightY = 1 - frac(i.uv.y);
				weightY = SCurve(weightY);
				
				float startsNoise = 0.5 * (noise + weightY);
				float starsGradient = step(startsNoise, 0.2 + sin(_Time.y * 0.2) / 100);
				
				float4 color = skyColor;
				
				if (starsGradient > 0)
				{
					color = lerp(skyColor, _StarsColor, startsNoise);
				}
				
				color = lerp(color, _SunColor, sunGradient);
				
				
				return color;
			}
			
			
			ENDHLSL
			
		}
	}
}
