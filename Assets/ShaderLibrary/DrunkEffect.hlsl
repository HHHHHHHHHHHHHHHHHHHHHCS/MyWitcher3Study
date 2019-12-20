#ifndef MYRP_DrunkEffect
	#define MYRP_DrunkEffect
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "PPSBase.hlsl"
	
	CBUFFER_START(MyDrunkEffect)
	float3 drunkData;
	float2 drunkCenter;
	CBUFFER_END
	
	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	
	static const float2 pointsAroundPixel[8] = {
		float2(1.0, 0.0),
		float2(-1.0, 0.0),
		float2(0.707, 0.707),
		float2(-0.707, -707),
		float2(0.0, 1.0),
		float2(0.0, -1.0),
		float2(-0.707, 0.707),
		float2(0.707, -0.707)
	};
	
	float4 DrunkEffectFragment(VertexOutput input): SV_TARGET
	{
		/* Inputs */
		float rotationAroundPixelRadius = drunkData.x;
		float drunkIntensity = drunkData.y;
		float2 texelSize = _ScreenParams.zw - 1.0;
		float2 centerPoint = drunkCenter;
		
		float2 rotationSinCos = float2(sin(_Time.x),cos(_Time.y));
		float rotationSpeed = drunkData.z;
		float2 rotationDirection = rotationSinCos.xy * rotationSpeed;
		
		float2 pixelPosition = input.uv;
		float2 centerToPixel = pixelPosition - centerPoint;
		
		/* Rotation Part */
		float intensityMask = dot(centerToPixel, centerToPixel);
		intensityMask *= 10.0;
		intensityMask = min(intensityMask, 1.0);
		intensityMask *= drunkIntensity;
		
		float distanceFromCenterToPixel = length(centerToPixel);
		
		//scale rotation direction (sin/cos pair) by distance from center
		float2 rotationOffsets = rotationDirection * distanceFromCenterToPixel;
		
		float zoomFactor = 1 - 0.1 * drunkIntensity;
		
		/* Calculating base rotation texcoords: */
		/* Approach #1 (closest approximation to original shader in terms of assembly) */
		// float2 baseTexcoords0 = zoomFactor * centerToPixel + rotationDirection * distanceFromCenterToPixel;
		// float2 baseTexcoords1 = zoomFactor * centerToPixel - rotationDirection * distanceFromCenterToPixel;
		
		// baseTexcoords0 += centerPoint;
		// baseTexcoords1 += centerPoint;
		
		/* Approach #2 (less instructions and more understandable (lerp) */
		float2 zoomedTexcoords = lerp(centerPoint, pixelPosition, zoomFactor);
		float2 baseTexcoords0 = zoomedTexcoords + rotationOffsets;
		float2 baseTexcoords1 = zoomedTexcoords - rotationOffsets;
		
		float rotationTexcoordsOffsetIntensity = intensityMask * rotationAroundPixelRadius;
		rotationTexcoordsOffsetIntensity *= 5.0;
		
		float2 rotationTexcoordsOffset = rotationTexcoordsOffsetIntensity * texelSize;
		
		// For opposite directions (difference by 180 degrees)
		float4 rotation0 = 0.0;
		float4 rotation1 = 0.0;
		
		int i = 0;
		[unroll]for (i = 0; i < 8; i ++)
		{
			rotation0 += _MainTex.Sample(sampler_MainTex, baseTexcoords0 + rotationTexcoordsOffset * pointsAroundPixel[i]);
		}
		rotation0 /= 16.0;
		
		[unroll]for (i = 0; i < 8; i ++)
		{
			rotation1 += _MainTex.Sample(sampler_MainTex, baseTexcoords1 + rotationTexcoordsOffset * pointsAroundPixel[i]);
		}
		rotation1 /= 16.0;
		
		float4 rotationPart = rotation0 + rotation1;
		
		/* Zooming in/out part */
		float zoomInOutScalePixels = drunkIntensity * 8.0;
		float2 zoomInOutScaleNormalizedScreenCoordinates = texelSize * zoomInOutScalePixels;
		float zoomInOutAmplitude = 1.0 + 0.02 * rotationSinCos.y;
		float2 zoomInOutfromCenterToTexel = zoomInOutAmplitude * centerToPixel;
		
		float2 zoomInOutBaseTextureUV = lerp(centerPoint, pixelPosition, zoomInOutAmplitude);
		float2 zoomInOutAddTextureUV0 = zoomInOutBaseTextureUV + zoomInOutfromCenterToTexel * zoomInOutScaleNormalizedScreenCoordinates;
		float2 zoomInOutAddTextureUV1 = zoomInOutBaseTextureUV + 2.0 * zoomInOutfromCenterToTexel * zoomInOutScaleNormalizedScreenCoordinates;
		
		float4 zoomColor0 = _MainTex.Sample(sampler_MainTex, zoomInOutBaseTextureUV);
		float4 zoomColor1 = _MainTex.Sample(sampler_MainTex, zoomInOutAddTextureUV0);
		float4 zoomColor2 = _MainTex.Sample(sampler_MainTex, zoomInOutAddTextureUV1);
		
		float4 zoomingPart = (zoomColor0 + zoomColor1 + zoomColor2) / 3.0;
		
		/* Combine rotation & zooming */
		/* Approach 1 (closest approximation to assembly from original shader) */
		// float4 finalColor = intensityMask * (rotationPart - zoomingPart);
		// finalColor = drunkIntensity * finalColor + zoomingPart;
		
		/* Approach 2 (you can deduce it from #1 formula, makes more sense for me */
		float4 finalColor = lerp(zoomingPart, rotationPart, intensityMask * drunkIntensity);
		
		return finalColor;
	}
	
#endif //MYRP_DrunkEffect
