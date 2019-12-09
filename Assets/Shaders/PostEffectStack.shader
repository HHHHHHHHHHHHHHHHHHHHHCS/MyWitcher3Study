Shader "Hidden/My Pipeline/PostEffectStack"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/PostEffectStack.hlsl"
		ENDHLSL
		
		Pass
		{
			//0.Copy Color
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex DefaultVert
			#pragma fragment CopyPassFragment
			
			ENDHLSL
			
		}
		
		Pass
		{
			//1.Blur
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex DefaultVert
			#pragma fragment BlurPassFragment
			
			ENDHLSL
			
		}
		
		Pass
		{
			//2.DepthStripes
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex DefaultVert
			#pragma fragment DepthStripesPassFragment
			
			ENDHLSL
			
		}
		
		Pass
		{
			//3.ToneMapping
			
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex DefaultVert
			#pragma fragment ToneMappingPassFragment
			
			ENDHLSL
			
		}
		
		Pass
		{
			//4.Luminance
			
			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex DefaultVert
			#pragma fragment LuminancePassFragment
			
			ENDHLSL
			
		}
	}
}
