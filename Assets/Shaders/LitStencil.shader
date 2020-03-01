Shader "MyPipeline/LitStencil"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
		_StencilColor ("Stencil Color", Color) = (1, 1, 1, 1)
		_MainTex ("Albedo & Alpha", 2D) = "white" { }
		[KeywordEnum(Off, On, Shadows)] _Clipping ("Alpha Clipping", Float) = 0
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		[HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
		[Enum(UnityEngine.Rendering.CullMode)]_Cull ("Cull", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
	}
	
	SubShader
	{
		Pass
		{
			Blend [_SrcBlend][_DstBlend]
			Cull [_Cull]
			ZWrite [_ZWrite]
			
			
			HLSLPROGRAM
			
			//兼容 导入 gles 2.0 SRP 库  默认GLES 2.0 是不支持的
			//#pragma prefer_hlslcc gles
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			//法向量 取消 非均匀缩放的 支持
			//#pragma instancing_options assumeuniformscaling
			
			#pragma shader_feature _CLIPPING_ON
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPLY_ALPHA
			
			#pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
			#pragma multi_compile _ _SHADOWS_HARD
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ _SHADOWMASK _DISTANCE_SHADOWMASK _SUBTRACTIVE_LIGHTING
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			
			
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment
			
			#include "../ShaderLibrary/Lit.hlsl"
			
			ENDHLSL
			
		}
		
		Pass
		{
			Tags { "LightMode" = "SRPStencil" }
			
			Blend One OneMinusSrcAlpha
			Cull Back
			ZWrite Off
			ZTest Off
			
			//原来是用模版控制的
			//然后在用轮廓算法抠出来
			/*
			Stencil
			{
				Ref 1
				Comp Greater
				Pass Keep
			}
			*/
			
			HLSLPROGRAM
			
			//兼容 导入 gles 2.0 SRP 库  默认GLES 2.0 是不支持的
			//#pragma prefer_hlslcc gles
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			//法向量 取消 非均匀缩放的 支持
			//#pragma instancing_options assumeuniformscaling
			
			#pragma vertex LitStencilPassVertex
			#pragma fragment LitStencilPassFragment
			
			
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
			
			struct LitStencilVertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float3 normal: NORMAL;
			};
			
			struct LitStencilVertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 worldToCamera: TEXCOORD1;
				float3 worldNormal: TEXCOORD2;
			};
			
			float4 _StencilColor;
			
			LitStencilVertexOutput LitStencilPassVertex(LitStencilVertexInput v)
			{
				LitStencilVertexOutput o = (LitStencilVertexOutput)0;
				
				float4 worldPos = mul(unity_ObjectToWorld, float4(v.pos.xyz, 1.0));
				o.clipPos = mul(unity_MatrixVP, worldPos);
				o.uv.xy = v.uv;
				o.worldToCamera = worldPos.xyz - _WorldSpaceCameraPos.xyz;
				o.worldNormal = mul(unity_ObjectToWorld, v.normal);
				return o;
			}
			
			
			float4 LitStencilPassFragment(LitStencilVertexOutput i): SV_TARGET
			{
				i.worldToCamera = normalize(-i.worldToCamera);
				i.worldNormal = normalize(i.worldNormal);
				float3 color = _StencilColor.rgb * pow(1 - dot(i.worldNormal, i.worldToCamera), 1);
				return float4(color, 0.5);
			}
			
			ENDHLSL
			
		}
		
		Pass
		{
			Tags { "LightMode" = "DepthOnly" }
			
			ColorMask 0
			Cull [_Cull]
			ZWrite On
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			//#pragma instancing_options assumeuniformscaling
			
			#pragma shader_feature _CLIPPING_ON
			#pragma multi_compile _ _LOD_FADE_CROSSFADE
			
			#pragma vertex DepthOnlyPassVertex
			#pragma fragment DepthOnlyPassFragment
			
			#include "../ShaderLibrary/DepthOnly.hlsl"
			
			ENDHLSL
			
		}
		
		Pass
		{
			Tags { "LightMode" = "ShadowCaster" }
			
			Cull [_Cull]
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			//#pragma instancing_options assumeuniformscaling
			
			#pragma shader_feature _CLIPPING_OFF
			
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			
			#include "../ShaderLibrary/ShadowCaster.hlsl"
			
			ENDHLSL
			
		}
		
		
		Pass
		{
			Tags { "LightMode" = "Meta" }
			
			Cull Off
			
			HLSLPROGRAM
			
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			
			#include "../ShaderLibrary/Meta.hlsl"
			
			ENDHLSL
			
		}
	}
	
	CustomEditor "LitShaderGUI"
}
