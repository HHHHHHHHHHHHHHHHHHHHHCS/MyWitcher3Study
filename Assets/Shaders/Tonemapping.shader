﻿Shader "Hidden/My Pipeline/Tonemapping"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Tonemapping.hlsl"
		ENDHLSL

		Pass
		{
			//0.Eye Apaptation
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex TonemappingVert
			#pragma fragment EyeAdaptationFrag
			
			ENDHLSL
		}

		Pass
		{
			//1.TonemappingSimple
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex TonemappingVert
			#pragma fragment TonemappingSimpleFrag
			
			ENDHLSL
			
		}
		
		Pass
		{
			//2.Tonemapping Lerp Color
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex TonemappingVert
			#pragma fragment TonemappingLerpFrag
			
			ENDHLSL
			
		}
	}
}
