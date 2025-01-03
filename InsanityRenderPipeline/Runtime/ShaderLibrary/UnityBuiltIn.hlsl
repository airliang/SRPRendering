// This HLSL is basically a copy of https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl

#ifndef SCRATCH_INPUT_BUILTIN_HLSL
#define SCRATCH_INPUT_BUILTIN_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "LightInput.hlsl"
#include "ShaderVariablesGlobal.hlsl"
#include "ColorConvert.hlsl"

#if defined(STEREO_INSTANCING_ON) && (defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_PSSL) || defined(SHADER_API_VULKAN))
#define UNITY_STEREO_INSTANCING_ENABLED
#endif

#if defined(STEREO_MULTIVIEW_ON) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN)) && !(defined(SHADER_API_SWITCH))
    #define UNITY_STEREO_MULTIVIEW_ENABLED
#endif

#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#define USING_STEREO_MATRICES
#endif

#if defined(USING_STEREO_MATRICES)
// Current pass transforms.
#define glstate_matrix_projection     unity_StereoMatrixP[unity_StereoEyeIndex] // goes through GL.GetGPUProjectionMatrix()
#define unity_MatrixV                 unity_StereoMatrixV[unity_StereoEyeIndex]
#define unity_MatrixInvV              unity_StereoMatrixInvV[unity_StereoEyeIndex]
#define unity_MatrixInvP              unity_StereoMatrixInvP[unity_StereoEyeIndex]
#define unity_MatrixVP                unity_StereoMatrixVP[unity_StereoEyeIndex]
#define unity_MatrixInvVP             unity_StereoMatrixInvVP[unity_StereoEyeIndex]

// Camera transform (but the same as pass transform for XR).
#define unity_CameraProjection        unity_StereoCameraProjection[unity_StereoEyeIndex] // Does not go through GL.GetGPUProjectionMatrix()
#define unity_CameraInvProjection     unity_StereoCameraInvProjection[unity_StereoEyeIndex]
#define unity_WorldToCamera           unity_StereoMatrixV[unity_StereoEyeIndex] // Should be unity_StereoWorldToCamera but no use-case in XR pass
#define unity_CameraToWorld           unity_StereoMatrixInvV[unity_StereoEyeIndex] // Should be unity_StereoCameraToWorld but no use-case in XR pass
#define _WorldSpaceCameraPos          unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex]
#endif

#define UNITY_LIGHTMODEL_AMBIENT (glstate_lightmodel_ambient * 2)


#if !defined(USING_STEREO_MATRICES)
#define _WorldSpaceCameraPos            _WorldSpaceCameraPos_Internal.xyz
#define _WorldSpaceCameraForward        _WorldSpaceCameraForward_Internal.xyz//-_CameraViewMatrix._m20_m21_m22
#endif

// scaleBias.x = flipSign
// scaleBias.y = scale
// scaleBias.z = bias
// scaleBias.w = unused
uniform float4 _ScaleBias;
uniform float4 _ScaleBiasRt;

#if !defined(USING_STEREO_MATRICES)
// Projection matrices of the camera. Note that this might be different from projection matrix
// that is set right now, e.g. while rendering shadows the matrices below are still the projection
// of original camera.
float4x4 unity_CameraProjection;
float4x4 unity_CameraInvProjection;
float4x4 unity_WorldToCamera;
float4x4 unity_CameraToWorld;
#endif

// ----------------------------------------------------------------------------

#ifndef DOTS_INSTANCING_ON // UnityPerDraw cbuffer doesn't exist with hybrid renderer

// Block Layout should be respected due to SRP Batcher
CBUFFER_START(UnityPerDraw)
// Space block Feature
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
real4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms

// Render Layer block feature
// Only the first channel (x) contains valid data and the float must be reinterpreted using asuint() to extract the original 32 bits values.
float4 unity_RenderingLayer;

// Light Indices block feature
// These are set internally by the engine upon request by RendererConfiguration.
half4 unity_LightData;
half4 unity_LightIndices[2];

half4 unity_ProbesOcclusion;

// Reflection Probe 0 block feature
// HDR environment map decode instructions
real4 unity_SpecCube0_HDR;
real4 unity_SpecCube1_HDR;

float4 unity_SpecCube0_BoxMax;          // w contains the blend distance
float4 unity_SpecCube0_BoxMin;          // w contains the lerp value
float4 unity_SpecCube0_ProbePosition;   // w is set to 1 for box projection
float4 unity_SpecCube1_BoxMax;          // w contains the blend distance
float4 unity_SpecCube1_BoxMin;          // w contains the sign of (SpecCube0.importance - SpecCube1.importance)
float4 unity_SpecCube1_ProbePosition;   // w is set to 1 for box projection

// Lightmap block feature
float4 unity_LightmapST;
float4 unity_DynamicLightmapST;

// SH block feature
float4 unity_SHAr;
float4 unity_SHAg;
float4 unity_SHAb;
float4 unity_SHBr;
float4 unity_SHBg;
float4 unity_SHBb;
float4 unity_SHC;

// Velocity
float4x4 unity_MatrixPreviousM;
float4x4 unity_MatrixPreviousMI;
//X : Use last frame positions (right now skinned meshes are the only objects that use this
//Y : Force No Motion
//Z : Z bias value
//W : Camera only
float4 unity_MotionVectorsParams;
CBUFFER_END

#endif // UNITY_DOTS_INSTANCING_ENABLED

#if defined(USING_STEREO_MATRICES)
CBUFFER_START(UnityStereoViewBuffer)
float4x4 unity_StereoMatrixP[2];
float4x4 unity_StereoMatrixInvP[2];
float4x4 unity_StereoMatrixV[2];
float4x4 unity_StereoMatrixInvV[2];
float4x4 unity_StereoMatrixVP[2];
float4x4 unity_StereoMatrixInvVP[2];

float4x4 unity_StereoCameraProjection[2];
float4x4 unity_StereoCameraInvProjection[2];

float3   unity_StereoWorldSpaceCameraPos[2];
float4   unity_StereoScaleOffset[2];
CBUFFER_END
#endif

#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
// OVR_multiview
// In order to convey this info over the DX compiler, we wrap it into a cbuffer.
#if !defined(UNITY_DECLARE_MULTIVIEW)
#define UNITY_DECLARE_MULTIVIEW(number_of_views) CBUFFER_START(OVR_multiview) uint gl_ViewID; uint numViews_##number_of_views; CBUFFER_END
#define UNITY_VIEWID gl_ViewID
#endif
#endif

#if defined(UNITY_STEREO_MULTIVIEW_ENABLED) && defined(SHADER_STAGE_VERTEX)
#define unity_StereoEyeIndex UNITY_VIEWID
UNITY_DECLARE_MULTIVIEW(2);
#elif defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
static uint unity_StereoEyeIndex;
#elif defined(UNITY_SINGLE_PASS_STEREO)
CBUFFER_START(UnityStereoEyeIndex)
int unity_StereoEyeIndex;
CBUFFER_END
#endif

float4x4 glstate_matrix_transpose_modelview0;

// ----------------------------------------------------------------------------

//real4 glstate_lightmodel_ambient;
//real4 unity_AmbientSky;
//real4 unity_AmbientEquator;
//real4 unity_AmbientGround;
//real4 unity_IndirectSpecColor;
float4 unity_FogParams;
real4  unity_FogColor;

#if !defined(USING_STEREO_MATRICES)
float4x4 glstate_matrix_projection;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_MatrixInvP;
float4x4 unity_MatrixVP;
float4x4 unity_MatrixInvVP;
float4 unity_StereoScaleOffset;
int unity_StereoEyeIndex;
#endif

real4 unity_ShadowColor;

// ----------------------------------------------------------------------------

// Unity specific
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);
TEXTURECUBE(unity_SpecCube1);
SAMPLER(samplerunity_SpecCube1);

// Main lightmap
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
TEXTURE2D_ARRAY(unity_Lightmaps);
SAMPLER(samplerunity_Lightmaps);

// Dynamic lightmap
TEXTURE2D(unity_DynamicLightmap);
SAMPLER(samplerunity_DynamicLightmap);
// TODO ENLIGHTEN: Instanced GI

// Dual or directional lightmap (always used with unity_Lightmap, so can share sampler)
TEXTURE2D(unity_LightmapInd);
TEXTURE2D_ARRAY(unity_LightmapsInd);
TEXTURE2D(unity_DynamicDirectionality);
// TODO ENLIGHTEN: Instanced GI
// TEXTURE2D_ARRAY(unity_DynamicDirectionality);

TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);
TEXTURE2D_ARRAY(unity_ShadowMasks);
SAMPLER(samplerunity_ShadowMasks);

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

// ----------------------------------------------------------------------------

// TODO: all affine matrices should be 3x4.
// TODO: sort these vars by the frequency of use (descending), and put commonly used vars together.
// Note: please use UNITY_MATRIX_X macros instead of referencing matrix variables directly.

float4x4 OptimizeProjectionMatrix(float4x4 M)
{
    // Matrix format (x = non-constant value).
    // Orthographic Perspective  Combined(OR)
    // | x 0 0 x |  | x 0 x 0 |  | x 0 x x |
    // | 0 x 0 x |  | 0 x x 0 |  | 0 x x x |
    // | x x x x |  | x x x x |  | x x x x | <- oblique projection row
    // | 0 0 0 1 |  | 0 0 x 0 |  | 0 0 x x |
    // Notice that some values are always 0.
    // We can avoid loading and doing math with constants.
    M._21_41 = 0;
    M._12_42 = 0;
    return M;
}

float3 GetCameraPositionWS()
{
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    return 0;
#endif
    return _WorldSpaceCameraPos;
}

float4x4 ApplyCameraTranslationToMatrix(float4x4 modelMatrix)
{
    // To handle camera relative rendering we substract the camera position in the model matrix
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    modelMatrix._m03_m13_m23 -= _WorldSpaceCameraPos.xyz;
#endif
    return modelMatrix;
}

float4x4 ApplyCameraTranslationToInverseMatrix(float4x4 inverseModelMatrix)
{
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    // To handle camera relative rendering we need to apply translation before converting to object space
    float4x4 translationMatrix = { { 1.0, 0.0, 0.0, _WorldSpaceCameraPos.x },{ 0.0, 1.0, 0.0, _WorldSpaceCameraPos.y },{ 0.0, 0.0, 1.0, _WorldSpaceCameraPos.z },{ 0.0, 0.0, 0.0, 1.0 } };
    return mul(inverseModelMatrix, translationMatrix);
#else
    return inverseModelMatrix;
#endif
}

float3 ApplyCameraTranslationToPosition(float3 position)
{
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    position -= _WorldSpaceCameraPos.xyz;
#endif
    return position;
}

#ifndef DOTS_INSTANCING_ON

//void GetAbsoluteWorldRendererBounds(out float3 minBounds, out float3 maxBounds)
//{
//    minBounds = unity_RendererBounds_Min.xyz;
//    maxBounds = unity_RendererBounds_Max.xyz;
//}

// Define Model Matrix Macro
// Note: In order to be able to define our macro to forbid usage of unity_ObjectToWorld/unity_WorldToObject/unity_MatrixPreviousM/unity_MatrixPreviousMI
// We need to declare inline function. Using uniform directly mean they are expand with the macro
float4x4 GetRawUnityObjectToWorld() { return unity_ObjectToWorld; }
float4x4 GetRawUnityWorldToObject() { return unity_WorldToObject; }
float4x4 GetRawUnityPrevObjectToWorld() { return unity_MatrixPreviousM; }
float4x4 GetRawUnityPrevWorldToObject() { return unity_MatrixPreviousMI; }

#define UNITY_MATRIX_M         ApplyCameraTranslationToMatrix(GetRawUnityObjectToWorld())
#define UNITY_MATRIX_I_M       ApplyCameraTranslationToInverseMatrix(GetRawUnityWorldToObject())
#define UNITY_PREV_MATRIX_M    ApplyCameraTranslationToMatrix(GetRawUnityPrevObjectToWorld())
#define UNITY_PREV_MATRIX_I_M  ApplyCameraTranslationToInverseMatrix(GetRawUnityPrevWorldToObject())

#else

// Not yet supported by BRG
void GetAbsoluteWorldRendererBounds(out float3 minBounds, out float3 maxBounds)
{
    minBounds = 0;
    maxBounds = 0;
}

#endif

float LoadCameraDepth(uint2 pixelCoords)
{
    return LOAD_TEXTURE2D_LOD(_CameraDepthTexture, pixelCoords, 0).r;
}

float SampleCameraDepth(float2 uv)
{
    return LoadCameraDepth(uint2(uv * _ScreenSize.xy));
}

//#define UNITY_MATRIX_V     unity_MatrixV
//#define UNITY_MATRIX_I_V   unity_MatrixInvV
//#define UNITY_MATRIX_P     OptimizeProjectionMatrix(glstate_matrix_projection)
//#define UNITY_MATRIX_I_P   unity_MatrixInvP
//#define UNITY_MATRIX_VP    unity_MatrixVP
//#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
//#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
//#define UNITY_MATRIX_T_MV  transpose(UNITY_MATRIX_MV)
//#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
//#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)
//#define UNITY_PREV_MATRIX_M   unity_MatrixPreviousM
//#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

// normal should be normalized, w=1.0
half3 SHEvalLinearL0L1(half4 normal)
{
    half3 x;

    // Linear (L1) + constant (L0) polynomial terms
    x.r = dot(_SHAr, normal);
    x.g = dot(_SHAg, normal);
    x.b = dot(_SHAb, normal);

    return x;
}

// normal should be normalized, w=1.0
half3 SHEvalLinearL2(half4 normal)
{
    half3 x1, x2;
    // 4 of the quadratic (L2) polynomials
    half4 vB = normal.xyzz * normal.yzzx;
    x1.r = dot(_SHBr, vB);
    x1.g = dot(_SHBg, vB);
    x1.b = dot(_SHBb, vB);

    // Final (5th) quadratic (L2) polynomial
    half vC = normal.x * normal.x - normal.y * normal.y;
    x2 = _SHC.rgb * vC;

    return x1 + x2;
}

// normal should be normalized, w=1.0
// output in active color space
half3 ShadeSH9(half4 normal)
{
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1(normal);

    // Quadratic polynomials
    res += SHEvalLinearL2(normal);

#ifdef UNITY_COLORSPACE_GAMMA
    //res = LinearToGammaSpace(res);
#endif

    return res;
}

#endif