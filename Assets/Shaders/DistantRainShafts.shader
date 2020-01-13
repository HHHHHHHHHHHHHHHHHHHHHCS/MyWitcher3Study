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
			
			CBUFFER_START(DistantRainShafts)
			float3 _ModelScale, _MeshBias;
			CBUFFER_END
			
			#define UNITY_MATRIX_M unity_ObjectToWorld
			#define UNITY_MATRIX_I_M unity_WorldToObject
			
			struct VertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			
			struct VertexOutput
			{
				float4 clipPos: SV_POSITION;
				float3 uvZ: TEXCOORD0;
			};
			
			
			VertexOutput DistantRainShaftsPassVertex(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
				
				o.uvZ.xy = v.uv;
				
				float3 meshScale = _ModelScale.xyz;//float3(4,4,2)
				float3 meshBias = _MeshBias.xyz;//float3(-2,-2,-1)
				float3 positionL = v.pos.w * meshScale + meshBias;
				
				o.uvZ.z = mul(unity_ObjectToWorld, positionL);
				
				o.clipPos = UnityObjectToClipPos(v.pos);
				
				o.positionH = UnityObjectToClipPos(float4(positionL, 1.0));
				
				return o;
			}
			
			float GetFrustumDepth(in float depth)
			{
				float d = depth * cb12_v22.x + cb12_v22.y;
				
				d = d * cb12_v21.x + cb12_v21.y;
				
				return 1.0 / max(d, 1e-4);
			}
			
			float4 DistantRainShaftsPassFragment(VertexOutput i): SV_TARGET
			{
				float2 inputUV = i.uvZ.xy;
				float worldHeight = i.uvZ.z;
				
				float elapsedTime = cb0_v0.x;
				float2 uvAnimation = cb4_v5.xy;
				float2 uvScale = cb4_v4.xy;
				float minValue = cb4_v2.x; //0.0
				float maxValue = cb4_v3.x; //1.0
				float3 shaftsColor = cb4_v0.rgb; //float3(147/255,162/255,173/255)
				
				float3 finalColorFilter = cb2_v2.rgb; //float3(1.175,1.296,1.342)
				float finalEffectIntensity = cb2_v2.w;
				
				float2 invViewportSize = cb0_v1.zw;
				
				float depthScale = cb4_v6.x; //0.001
				
				float2 uvOffsets = elapsedTime * uvAnimation;
				float2 uv = inputUV * uvScale * uvOffsets;
				float disturb = texture0.Sample(sampler0, uv).x;
				
				float intensity = saturate(lerp(minValue, maxValue, disturb));
				intensity *= inputUV.y;
				intensity *= cb4_v1.x; //1.0
				
				float2 screenUV = i.positionH.xy * invViewportSize;
				float hardwareDepth = texture15.Sample(samapler15, screenUV).x;
				float frustumDepth = GetFrustumDepth(hardwareDepth);
				
				float depth = frustumDepth - worldHeight;
				float distantObjectsMask = saturate(depth * depthScale);
				
				float finalEffectMask = saturate(intensity * distantObjectsMask);
				
				float paramX = finalEffectMask;
				float paramY = cb0_v7.y * finalEffectMask;
				float effectAmount = lerp(paramX, paramY, cb4_v7.x);
				
				float3 effectColor = effectAmount * shaftsColor;
				
				//gamma
				effectColor = pow(effectColor, 2.2);
				
				effectColor *= finalColorFilter;
				effectColor *= finalEffectIntensity;
				
				//return zero alpha 
				//srcColor * 1.0 + (1.0 - srcAlpha) * destColor
				return float4(effectColor, 0.0);
			}
			
			ENDHLSL
			
		}
	}
}
