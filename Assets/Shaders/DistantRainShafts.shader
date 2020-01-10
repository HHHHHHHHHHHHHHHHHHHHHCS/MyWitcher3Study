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
				o.LODParams = v.lodParams;
				
				float3 meshScale = _ModelScale.xyz;//float3(4,4,2)
				float3 meshBias = _MeshBias.xyz;//float3(-2,-2,-1)
				float3 positionL = v.pos.w * meshScale + meshBias;
				
				o.uvZ.z = mul(unity_ObjectToWorld, positionL);
				
				o.clipPos = UnityObjectToClipPos(v.pos);
				
				return o;
			}
			
			float4 DistantRainShaftsPassFragment(VertexOutput i): SV_TARGET { }
			
			ENDHLSL
			
		}
	}
}
