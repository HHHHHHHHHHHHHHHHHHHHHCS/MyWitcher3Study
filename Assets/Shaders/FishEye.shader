Shader "Hidden/My Pipeline/FishEye"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/FishEye.hlsl"
		ENDHLSL
		
		Pass
		{
			HLSLPROGRAM

			#pragma target 3.5

			#pragma vertex DefaultVert
			#pragma fragment FishEyeFragment

			ENDHLSL
			
		}
	}
}
