Shader "Hidden/My Pipeline/Sharpen"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Sharpen.hlsl"
		ENDHLSL
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma vertex DefaultVert
			#pragma fragment SharpenFragment
			
			ENDHLSL
			
		}
	}
}
