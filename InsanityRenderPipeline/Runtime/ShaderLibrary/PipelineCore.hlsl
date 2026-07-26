#ifndef SCRATCH_INPUT_TRANSFORMATION_HLSL
#define SCRATCH_INPUT_TRANSFORMATION_HLSL

#include "Input.hlsl"

// Below functions are missing in core RP...

struct VertexPositionInputs
{
    float3 positionWS; // World space position
    float3 positionVS; // View space position
    float4 positionCS; // Homogeneous clip space position
    float4 positionNDC;// Homogeneous normalized device coordinates
};

struct VertexNormalInputs
{
    real3 tangentWS;
    real3 bitangentWS;
    float3 normalWS;
};

VertexPositionInputs GetVertexPositionInputs(float3 positionOS)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

VertexNormalInputs GetVertexNormalInputs(float3 normalOS)
{
    VertexNormalInputs tbn;
    tbn.tangentWS = real3(1.0, 0.0, 0.0);
    tbn.bitangentWS = real3(0.0, 1.0, 0.0);
    tbn.normalWS = TransformObjectToWorldNormal(normalOS);
    return tbn;
}

VertexNormalInputs GetVertexNormalInputs(float3 normalOS, float4 tangentOS)
{
    VertexNormalInputs tbn;

    // mikkts space compliant. only normalize when extracting normal at frag.
    real sign = tangentOS.w * GetOddNegativeScale();
    tbn.normalWS = TransformObjectToWorldNormal(normalOS);
    tbn.tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
    tbn.bitangentWS = cross(tbn.normalWS, tbn.tangentWS) * sign;
    return tbn;
}

#if UNITY_REVERSED_Z
#if SHADER_API_OPENGL || SHADER_API_GLES || SHADER_API_GLES3
//GL with reversed z => z clip range is [near, -far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(-(coord), 0)
#else
//D3d with reversed Z => z clip range is [near, 0] -> remapping to [0, far]
//max is required to protect ourselves from near plane not being correct/meaningfull in case of oblique matrices.
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) max(((1.0-(coord)/_ProjectionParams.y)*_ProjectionParams.z),0)
#endif
#elif UNITY_UV_STARTS_AT_TOP
//D3d without reversed z => z clip range is [0, far] -> nothing to do
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#else
//Opengl => z clip range is [-near, far] -> should remap in theory but dont do it in practice to save some perf (range is close enough)
#define UNITY_Z_0_FAR_FROM_CLIPSPACE(coord) (coord)
#endif

float3 TransformObjectToViewPos(float3 positionOS)
{
    return mul(GetWorldToViewMatrix(), mul(GetObjectToWorldMatrix(), float4(positionOS, 1.0))).xyz;
}

float4 ComputeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

// Screen UV in [0, 1] from a view-projection matrix and world position.
// Use non-jittered matrices for TAA motion vectors so jitter is handled by temporal accumulation.
float2 WorldPosToScreenUV(float4x4 viewProjMatrix, float3 positionWS)
{
    float4 csPos = mul(viewProjMatrix, float4(positionWS, 1.0));
    csPos.xy /= csPos.w;
    csPos.y *= _ProjectionParams.x;
    return csPos.xy * 0.5 + 0.5;
}

// Camera-relative: positionWS is relative to the *current* camera.
// Previous VP is relative to the *previous* camera, so offset by camera displacement.
float3 GetPreviousFramePositionWS(float3 positionWS)
{
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    return positionWS + (_WorldSpaceCameraPos_Internal.xyz - _PrevWorldSpaceCameraPos.xyz);
#else
    return positionWS;
#endif
}

// UV-space motion: current UV - previous UV. TAA uses historyUV = uv - motionVector.
// (HDRP stores NDC delta * 0.5 for the same UV-space quantity.)
float2 ComputeMotionVectorUV(float3 positionWS)
{
    float2 ssPosCurr = WorldPosToScreenUV(_NonJitteredViewProjMatrix, positionWS);
    float2 ssPosPrev = WorldPosToScreenUV(_PrevNonJitteredViewProjMatrix, GetPreviousFramePositionWS(positionWS));
    return ssPosCurr - ssPosPrev;
}

float4 ComputeGrabScreenPos (float4 pos) 
{
    #if UNITY_UV_STARTS_AT_TOP
        float scale = -1.0;
    #else
        float scale = 1.0;
    #endif

    float4 o = pos * 0.5f;
    o.xy = float2(o.x, o.y*scale) + o.w;

    #ifdef UNITY_SINGLE_PASS_STEREO
        o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
    #endif
    
    o.zw = pos.zw;
    return o;
}

#endif // SCRATCH_INPUT_TRANSFORMATION_HLSL