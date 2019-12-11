Shader "Hidden/My Pipeline/Vignette"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Vignette.hlsl"
		ENDHLSL
		
		Pass
		{
			//0.Vignette Simple
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex DefaultVert
			#pragma fragment VignetteSimpleFragment
			
			ENDHLSL
			
		}
		
		Pass
		{
			//1.Vignette Complex
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex DefaultVert
			#pragma fragment VignetteComplexFragment
			
			ENDHLSL
			
		}
	}
}
