Shader "Hidden/My Pipeline/Drunk Effect"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/DrunkEffect.hlsl"
		ENDHLSL
		
		Pass
		{
			//0.Vignette Simple
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex DefaultVert
			#pragma fragment DrunkEffectFragment
			
			ENDHLSL
			
		}
	}
}
