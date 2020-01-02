Shader "MyPipeline/MoonLunarPhases"
{
	Properties { }
	SubShader
	{
		
		Pass
		{
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
			#pragma vertex MoonPassVertex
			#pragma fragment MoonPassFragment
			
			struct VertexInput
			{
				float4 pos: POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: NORMAL;
				float4 tangent: TANGENT;
			};
			
			struct VertexOutput
			{
				float4 clipPos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float4 normal: TEXCOORD1;
				float4 tangent: TEXCOORD2;
				float4 binormal: TEXCOORD3;
			};
			
			VertexOutput MoonPassVertex(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
				return o;
			}
			
			float4 MoonPassFragment(VertexOutput i): SV_TARGET
			{
				return 0;
			}
			
			ENDHLSL
			
		}
	}
}
