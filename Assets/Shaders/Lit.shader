﻿Shader "MyPipeline/Lit"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
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
