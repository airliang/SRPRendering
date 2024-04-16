#ifndef UNITY_SHADER_VARIABLES_GLOBAL_INCLUDED
#define UNITY_SHADER_VARIABLES_GLOBAL_INCLUDED

// Generated from UnityEngine.Rendering.HighDefinition.ShaderVariablesGlobal
// PackingRules = Exact
GLOBAL_CBUFFER_START(ShaderVariablesGlobal, b0)
	float4x4 _ViewMatrix;
	float4x4 _CameraViewMatrix;
	float4x4 _InvViewMatrix;
	float4x4 _ProjMatrix;
	float4x4 _InvProjMatrix;
	float4x4 _ViewProjMatrix;
	float4x4 _CameraViewProjMatrix;
	float4x4 _InvViewProjMatrix;
	float4x4 _PixelCoordToViewDirWS;
	float4 _WorldSpaceCameraPos_Internal;
	float4 _ScreenSize;

	// Values used to linearize the Z buffer (http://www.humus.name/temp/Linearize%20depth.txt)
	// x = 1-far/near
	// y = far/near
	// z = x/far
	// w = y/far
	// or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
	// x = -1+far/near
	// y = 1
	// z = x/far
	// w = 1/far
	float4 _ZBufferParams;

	// x = 1 or -1 (-1 if projection is flipped)
	// y = near plane
	// z = far plane
	// w = 1/far plane
	float4 _ProjectionParams;

	// x = orthographic camera's width
	// y = orthographic camera's height
	// z = unused
	// w = 1.0 if camera is ortho, 0.0 if perspective
	float4 unity_OrthoParams;

	// x = width
	// y = height
	// z = 1 + 1.0/width
	// w = 1 + 1.0/height
	float4 _ScreenParams;
	float4 _Time;
	float4 _SinTime;
	float4 _CosTime;
	float4 unity_DeltaTime;

	int _FrameIndex;
	float3 _Pad0;
CBUFFER_END

// These are the samplers available in the HDRenderPipeline.
// Avoid declaring extra samplers as they are 4x SGPR each on GCN.
SAMPLER(s_point_clamp_sampler);
SAMPLER(s_point_repeat_sampler);
SAMPLER(s_linear_clamp_sampler);
SAMPLER(s_linear_repeat_sampler);
SAMPLER(s_trilinear_clamp_sampler);
SAMPLER(s_trilinear_repeat_sampler);
SAMPLER_CMP(s_linear_clamp_compare_sampler);

#endif // UNITY_SHADER_VARIABLES_GLOBAL_INCLUDED
