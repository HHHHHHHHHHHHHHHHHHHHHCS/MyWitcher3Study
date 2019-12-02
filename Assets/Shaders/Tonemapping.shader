Shader "Hidden/My Pipeline/Tonemapping"
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
			//0.Simple
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex TonemappingVert
			#pragma fragment TonemappingSimpleFrag
			
			ENDHLSL
			
		}
		
		Pass
		{
			//1.Lerp Color
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex TonemappingVert
			#pragma fragment TonemappingLerpFrag
			
			ENDHLSL
			
		}
	}
}
