#ifndef MYRP_PPS_BASE
	#define MYRP_PPS_BASE
	
	//SetupCameraProperties() 传入 _ProjectionParams 和 _ZBufferParams
	float4 _ProjectionParams;
	float4 _ZBufferParams;
	//x y  screenSize   z w 1+1/size
	float4 _ScreenParams;
	//Unity传入
	float4 _Time;
	float3 _WorldSpaceCameraPos;
	
	struct VertexInput
	{
		float4 pos: POSITION;
	};
	
	struct VertexOutput
	{
		float4 clipPos: SV_POSITION;
		float2 uv: TEXCOORD0;
	};
	
	VertexOutput DefaultVert(VertexInput input)
	{
		VertexOutput output;
		output.clipPos = float4(input.pos.x, input.pos.y, 0.0, 1.0);
		output.uv = input.pos.xy * 0.5 + 0.5;
		
		//当不使用 OpenGL 时，场景视图窗口和小型相机预览将被翻转
		//检查 ProjectionParams 向量的 x 组件来检测翻转是否发生
		//SetupCameraProperties 会设置 ProjectionParams
		if (_ProjectionParams.x < 0.0)
		{
			output.uv.y = 1.0 - output.uv.y;
		}
		
		return output;
	}
	
	float SCurve(float x)
	{
		float x2 = x * x;
		float x3 = x2 * x;
		
		// -2x^3 + 3x^2
		return - 2.0 * x3 + 3.0 * x2;
	}
	
	int GetInt(float x)
	{
		return asint(floor(x));
	}
	
	int GetReverseInt(float x)
	{
		return reversebits(GetInt(x));
	}
	
	// Shaders in TW3 use integer noise.
	// For more details see: http://libnoise.sourceforge.net/noisegen/
	float IntegerNoise(int n)
	{
		n = (n >> 13) ^ n;
		int nn = (n * (n * n * 60493 + 19990303) + 1376312589) & 0x7fffffff;
		return((float)nn / 1073741824.0);
	}
	
	
#endif // MYRP_PPS_BASE