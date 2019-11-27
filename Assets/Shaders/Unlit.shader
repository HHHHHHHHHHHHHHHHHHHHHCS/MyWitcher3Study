Shader "MyPipeline/Unlit"
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)
	}
	
	SubShader
	{
		Pass
		{
			HLSLPROGRAM
			
			//兼容 导入 gles 2.0 SRP 库  默认GLES 2.0 是不支持的
			//#pragma prefer_hlslcc gles
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			//法向量 取消 非均匀缩放的 支持
			#pragma instancing_options assumeuniformscaling
						
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			
			#include "../ShaderLibrary/Unlit.hlsl"
			
			ENDHLSL
			
		}
	}
}
