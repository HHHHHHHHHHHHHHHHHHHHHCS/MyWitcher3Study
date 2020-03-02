#ifndef MYRP_FishEye
	#define MYRP_FishEye
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	CBUFFER_START(MyFishEye)
	
	CBUFFER_END
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	float _FishEyeAmount;
	
	
	float4 FishEyeFragment(VertexOutput input): SV_TARGET
	{
		// *** Inputs
		
		// * Zoom amount, always 1
		const float zoomAmount = 1.0;
		
		// Another value which affect fisheye effect
		// but always set to float2(1.0, 1.0).
		const float2 amount = float2(1.0, 1.0);
		
		// Colors of witcher senses
		const float3 colorInteresting = float3(0, 0, 0);
		const float3 colorTraces = float3(0, 0, 0);
		
		// Was always set to float2(0.0, 0.0).
		// Setting this to higher values
		// makes "greyish corners" effect weaker.
		const float2 offset = float2(0.0, 0.0);
		
		// Elapsed time in seconds
		float time = _Time.y;
		
		// Main value which causes fisheye effect [0-1]
		const float fisheyeAmount = saturate(_FishEyeAmount);
		
		// *** Shader
		
		// Main uv
		float2 uv = input.uv;
		
		// Scale at first from [0-1] to [-1;1], then calculate abs
		float2 uv3 = abs(uv * 2.0 - 1.0);
		
		// Aspect ratio
		float aspectRatio = _ScreenParams.x / _ScreenParams.y;
		
		
		// * Mask used to make corners grey
		float mask_gray_corners;
		{
			float2 newUv = float2(uv3.x * aspectRatio, uv3.y) - offset;
			newUv = saturate(newUv / 1.8);//1.8~=1920/1080=1.777778
			newUv = pow(newUv, 2.5);//
			
			mask_gray_corners = 1 - min(1.0, length(newUv));
		}
		
		
		// circle radius used further
		float circle_radius;
		{
			float2 corners0 = saturate(float2(0.03, 0.03) - uv);
			float cor = corners0.x + corners0.y;
			
			float2 corners1 = saturate(uv - float2(0.97, 0.97));
			
			cor += corners1.x;
			cor += corners1.y;
			
			circle_radius = saturate(cor * 20.0) ; // r0.x, line 21
		}
		
		return circle_radius;
		
		/*
		// * Zooming effect
		float2 offsetUV = 0;
		float2 colorUV = 0;
		{
			float2 uv4 = 2 * PosH.xy;
			uv4 /= cb0_v2.xy;
			uv4 -= float2(1.0, 1.0);
			
			float mask3 = dot(uv4, uv4);
			uv4 *= mask3;
			
			float attenuation = fisheyeAmount * 0.1;
			uv4 *= attenuation;
			
			offsetUV = clamp(uv4, float2(-0.4, -0.4), float2(0.4, 0.4));
			offsetUV *= zoomAmount;
			
			float2 uv = PosH.xy * invTexSize; // main uv
			colorUV = uv - offsetUV * amount;
		}
		
		
		// * Sample color map
		float3 color = texture0.Sample(sampler0, colorUV).rgb; // r2.xyz
		
		
		// * Sample outline map
		
		// interesting objects (upper left square)
		float2 outlineUV = colorUV * 0.5;
		float outlineInteresting = texture2.Sample(sampler2, outlineUV).x; // r0.y
		
		// traces (upper right square)
		outlineUV = colorUV * 0.5 + float2(0.5, 0.0);
		float outlineTraces = texture2.Sample(sampler2, outlineUV).x;  // r2.w
		
		
		outlineInteresting /= 8.0; // r4.x
		outlineTraces /= 8.0; // r4.y
		
		float timeParam = time * 0.1;
		
		// adjust circle radius
		circle_radius = 1.0 - circle_radius;
		circle_radius *= 0.03;
		
		float3 color_circle_main = float3(0.0, 0.0, 0.0);
		
		[loop]
		for (int i = 0; 8 > i; i ++)
		{
			// full 2*PI = 360 angles cycle
			const float angleRadians = (float) i * PI / 4 - timeParam;
			
			// unit circle
			float2 unitCircle;
			sincos(angleRadians, unitCircle.y, unitCircle.x); // unitCircle.x = cos, unitCircle.y = sin
			
			// adjust radius
			unitCircle *= circle_radius;
			
			// * base texcoords (circle) - note we also scale radius here by 8
			// * probably because of dimensions of outline map.
			// line 55
			float2 uv_outline_base = colorUV + unitCircle / 8.0;
			
			// * interesting objects (circle)
			float2 uv_outline_interesting_circle = uv_outline_base * 0.5;
			float outline_interesting_circle = texture2.Sample(sampler2, uv_outline_interesting_circle).x;
			outlineInteresting += outline_interesting_circle / 8.0;
			
			// * traces (circle)
			float2 uv_outline_traces_circle = uv_outline_base * 0.5 + float2(0.5, 0.0);
			float outline_traces_circle = texture2.Sample(sampler2, uv_outline_traces_circle).x;
			outlineTraces += outline_traces_circle / 8.0;
			
			// * sample color texture with perturbation
			float2 uv_color_circle = colorUV + unitCircle * offsetUV;
			float3 color_circle = texture0.Sample(sampler0, uv_color_circle).rgb;
			color_circle_main += color_circle / 8.0;
		}
		
		// * Sample intensity map
		float2 intensityMap = texture3.Sample(sampler0, colorUV).xy;
		
		float intensityInteresting = intensityMap.r;
		float intensityTraces = intensityMap.g;
		
		// * Adjust outlines
		float mainOutlineInteresting = saturate(outlineInteresting - 0.8 * intensityInteresting);
		float mainOutlineTraces = saturate(outlineTraces - 0.75 * intensityTraces);
		
		// * Greyish color
		float3 color_greyish = dot(color_circle_main, float3(0.3, 0.3, 0.3)).xxx;
		
		// * Determine main color.
		// (1) At first, combine "circled" color with grey one.
		// Now we have have greyish corners here.
		float3 mainColor = lerp(color_greyish, color_circle_main, mask_gray_corners) * 0.6;
		
		// (2) Then mix "regular" color with the above.
		// Please note this operation makes corners gradually grey (because fisheyeAmount rises from 0 to 1).
		mainColor = lerp(color, mainColor, fisheyeAmount);
		
		// * Determine color of witcher senses
		float3 senses_traces = mainOutlineTraces * colorTraces;
		float3 senses_interesting = mainOutlineInteresting * colorInteresting;
		
		// * Slightly boost traces
		float3 senses_total = 1.2 * senses_traces + senses_interesting;
		
		// * Final combining
		float3 senses_total_sat = saturate(senses_total);
		float dot_senses_total = saturate(dot(senses_total, float3(1.0, 1.0, 1.0)));
		
		float3 finalColor = lerp(mainColor, senses_total_sat, dot_senses_total);
		return float4(finalColor, 1.0);
		*/
	}
#endif //My_FishEye
