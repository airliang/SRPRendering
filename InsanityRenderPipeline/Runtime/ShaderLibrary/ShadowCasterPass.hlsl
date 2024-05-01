#ifndef SHADOW_CASTER_ONLY_PASS_INCLUDED
#define SHADOW_CASTER_ONLY_PASS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Version.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Shadow/Shadows.hlsl"


struct Attributes
{
    float4 position     : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
        float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GetShadowPositionHClip(Attributes input)
{
    float3 positionWS = TransformObjectToWorld(input.position.xyz);

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_SHADOW_VSM)
    float4 positionCS = TransformWorldToHClip(positionWS);
#else
#if defined(_ADAPTIVE_SHADOW_BIAS)
    float4 positionCS = TransformWorldToHClip(ApplyAdaptiveShadowBias(positionWS, normalWS, _LightDirection));
#else
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
#endif
#endif

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(_ALPHATEST_ON)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif
    output.positionCS = GetShadowPositionHClip(input);//TransformObjectToHClip(input.position.xyz);
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(_ALPHATEST_ON)
    half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
#endif
#if defined(_SHADOW_VSM) || defined(_SHADOW_EVSM)
    float depth = input.positionCS.z;
#if UNITY_REVERSED_Z
    depth = 1.0 - depth;
#endif
#if defined(_SHADOW_EVSM)
    float pos = exp(_ShadowExponents.x * depth);
    float neg = -exp(-_ShadowExponents.y * depth);
    return half4(pos, pos * pos, neg, neg * neg);
#else
    float dx = ddx(depth);
    float dy = ddy(depth);

    //float firstMoment = depth; // redundant
    float secondMoment = depth * depth + 0.25 * (dx * dx + dy * dy);
    return half4(depth, secondMoment, 0, 0);
#endif
#else
    return 0;
#endif
}
#endif
