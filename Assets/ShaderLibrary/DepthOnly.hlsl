#ifndef MYRP_DEPTH_ONLY_INCLUDED
	#define MYRP_DEPTH_ONLY_INCLUDED
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	
	CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
	float4 _DitherTexture_ST;
	CBUFFER_END
	
	CBUFFER_START(UnityPerCamera)
	float3 _WorldSpaceCameraPos;
	CBUFFER_END
	
	CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld, unity_WorldToObject;
	float4 unity_LODFade;
	float4 unity_LightIndicesOffsetAndCount;
	float4 unity_4LightIndices0, unity_4LightIndices1;
	float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax;
	float4 unity_SpecCube0_ProbePosition, unity_SpecCube0_HDR;
	float4 unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax;
	float4 unity_SpecCube1_ProbePosition, unity_SpecCube1_HDR;
	float4 unity_LightmapST, unity_DynamicLightmapST;
	float4 unity_SHAr, unity_SHAg, unity_SHAb;
	float4 unity_SHBr, unity_SHBg, unity_SHBb;
	float4 unity_SHC;
	CBUFFER_END
	
	CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float _Cutoff;
	CBUFFER_END
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	TEXTURE2D(_DitherTexture);
	SAMPLER(sampler_DitherTexture);
	
	#define UNITY_MATRIX_M unity_ObjectToWorld
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
	
	UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
	UNITY_INSTANCING_BUFFER_END(PerInstance)
	
	struct VertexInput
	{
		float4 pos: POSITION;
		float2 uv: TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};
	
	struct VertexOutput
	{
		float4 clipPos: SV_POSITION;
		float2 uv: TEXCOORD3;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};
	
	VertexOutput DepthOnlyPassVertex(VertexInput input)
	{
		VertexOutput output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_TRANSFER_INSTANCE_ID(input, output);
		float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
		output.clipPos = mul(unity_MatrixVP, worldPos);
		output.uv = TRANSFORM_TEX(input.uv, _MainTex);
		return output;
	}
	
	void LODCrossFadeClip(float4 clipPos)
	{
		float2 ditherUV = TRANSFORM_TEX(clipPos.xy, _DitherTexture);
		float lodClipBias = SAMPLE_TEXTURE2D(_DitherTexture, sampler_DitherTexture, ditherUV).a;
		if (unity_LODFade.x < 0.5)
		{
			lodClipBias = 1.0 - lodClipBias;
		}
		
		clip(unity_LODFade.x - lodClipBias);
	}
	
	float4 DepthOnlyPassFragment(VertexOutput input): SV_TARGET
	{
		UNITY_SETUP_INSTANCE_ID(input);
		
		#if defined(LOD_FADE_CROSSFADE)
			LODCrossFadeClip(input.clipPos);
		#endif
		
		float4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
		albedoAlpha *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);
		
		#if defined(_CLIPPING_ON)
			clip(albedoAlpha.a - _Cutoff);
		#endif
		
		return 0;
	}
	
#endif