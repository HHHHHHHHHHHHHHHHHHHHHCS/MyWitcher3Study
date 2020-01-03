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
				float3 normal: TEXCOORD1;
				float3 tangent: TEXCOORD2;
				float3 binormal: TEXCOORD3;
			};
			
			VertexOutput MoonPassVertex(VertexInput v)
			{
				VertexOutput o = (VertexOutput)0;
				return o;
			}
			
			float4 MoonPassFragment(VertexOutput i): SV_TARGET
			{
				float2 uvOffsets = float2(-cb0_v0.w, 0.0);
				
				float2 uv = i.uv + uvOffsets;
				
				float4 moonNormal = _MoonColorTex.Sample(sampler0er, uv);
				
				float moonColorA = pow(moonColor.a, 2.2);
				
				float3 sampledNormal = normalize((moonNormal.xyz - 0.5) * 2);
				
				float3 Tangent = i.tangent.xyz;
				float3 Normal = i.normal.xyz;
				float3 Bitangent = i.binormal.xyz;
				
				float3x3 TBN = float3x3(Tangent, Bitangent, Normal);
				
				float2 vNormal = mul(sampledNormal, (float3x2)TBN).xy;
				
				float phase = cb0_v0.y * (1.0 / SYNODIC_MONTH_LENGTH) + cb0_v0.w;
				
				phase *= TWOPI;
				
				float outSin = 0.0;
				float outCos = 0.0;
				
				sincos(phase, outSin, outCos);
				
				float lunarPhase = saturate(dot(vNormal, float2(outCos, outSin)));
				
				float3 moonSurfaceGlowColor = cb_12_v266.xyz;
				
				float3 moonColor = lunarPhase * moonSurfaceGlowColor;
				moonColor = moonColorA * moonColor;
				
				moonColor *= cb2_v2.xyz;
				
				float paramHorizon = saturate(1.0 - IN.param1.w);
				paramHorizon *= cb2_v2.w;
				
				moonColor *= paramHorizon;
				
				return float4(moonColor, 0.0);
				
				return 0;
			}
			
			ENDHLSL
			
		}
	}
}
