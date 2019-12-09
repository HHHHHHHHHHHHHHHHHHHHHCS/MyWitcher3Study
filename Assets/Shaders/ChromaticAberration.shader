Shader "Hidden/My Pipeline/ChromaticAberration"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off

		HLSLINCLUDE
		#include "../ShaderLibrary/ChromaticAberration.hlsl"
		ENDHLSL

		Pass
		{
			//0.ChromaticAberration
			HLSLPROGRAM

			#pragma target 3.5

			#pragma vertex DefaultVert
			#pragma fragment ChromaticAberrationPassFragment

			ENDHLSL
		}

	}
}
