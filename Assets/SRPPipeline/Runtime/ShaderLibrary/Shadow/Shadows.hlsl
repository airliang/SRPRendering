#ifndef SHADOWS_INCLUDED
#define SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "ShadowSampling.hlsl"
#include "../PipelineCore.hlsl"


#define MAX_SHADOW_CASCADES 4

#if defined(_MAIN_LIGHT_SHADOWS)
#define MAIN_LIGHT_CALCULATE_SHADOWS

#if !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
#endif
#endif

#ifndef SHADER_API_GLES3
CBUFFER_START(MainlightShadowVariablesGlobal)
#endif
float4x4    _MainLightWorldToShadow[MAX_SHADOW_CASCADES + 1];
float4      _CascadeShadowSplitSpheres[MAX_SHADOW_CASCADES];
float       _MainLightShadowDepthRange[MAX_SHADOW_CASCADES + 1];
float4      _CascadeShadowSplitSphereRadii;
half4       _MainLightShadowOffset0;  //use in moble for soft shadow
half4       _MainLightShadowOffset1;
half4       _MainLightShadowOffset2;
half4       _MainLightShadowOffset3;
float4       _MainLightShadowParams;  // (x: shadowStrength, y: 1.0 if soft shadows, 0.0 otherwise, z: csm blend distance, w: active cascade counts)
float4      _MainLightShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
half        _ShadowDistance;
float4 _ScreenSpaceShadowmapSize;
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif


float4    _ShadowBias; // x: depth bias, y: normal bias
float4 _LightSplitsNear;
float4 _LightSplitsFar;
float3 _LightDirection;
int    _ActiveCascadeIndex;

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);
//SamplerState sampler_ShadowMap_state;
#if defined(_SHADOW_EVSM) || defined(_SHADOW_VSM)
float2 _ShadowExponents;
float _LightBleedingReduction;
#endif

#ifdef _SCREENSPACE_SHADOW
Texture2D _ScreenSpaceShadowmapTexture;
#endif

#define BEYOND_SHADOW_FAR(shadowCoord) shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0

ShadowSamplingData GetMainLightShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;
    shadowSamplingData.shadowOffset0 = _MainLightShadowOffset0;
    shadowSamplingData.shadowOffset1 = _MainLightShadowOffset1;
    shadowSamplingData.shadowOffset2 = _MainLightShadowOffset2;
    shadowSamplingData.shadowOffset3 = _MainLightShadowOffset3;
    shadowSamplingData.shadowmapSize = _MainLightShadowmapSize;
    shadowSamplingData.softShadowQuality = _MainLightShadowParams.y;

    return shadowSamplingData;
}

// ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetMainLightShadowParams()
{
    return _MainLightShadowParams;
}

#ifdef _SCREENSPACE_SHADOW
half SampleScreenSpaceShadowmap(float2 shadowCoord)
{
    //shadowCoord.xy /= shadowCoord.w;

    half attenuation = SAMPLE_TEXTURE2D(_ScreenSpaceShadowmapTexture, sampler_LinearClamp, shadowCoord.xy).x;

    return attenuation;
}
#endif

#ifdef _SHADOW_PCSS
inline float GetPCSSScale(float4 cascadeWeights)
{
    float scale = 1.0;
    scale = (cascadeWeights.y > 0.0) ? 2.0 : scale;
    scale = (cascadeWeights.z > 0.0) ? 4.0 : scale;
    scale = (cascadeWeights.w > 0.0) ? 8.0 : scale;
    return 1.0 / scale;
}
#endif

real SampleShadowmap(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float2 positionSS, 
    int cascadeIndex, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true)
{
    // Compiler will optimize this branch away as long as isPerspectiveProjection is known at compile time
    if (isPerspectiveProjection)
        shadowCoord.xyz /= shadowCoord.w;

    real attenuation;
    real shadowStrength = shadowParams.x;

#ifdef _SHADOW_PCSS
    int cascadeScale = 1 << cascadeIndex;
    attenuation = SampleShadowmapPCSS(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, 
        positionSS, (float)cascadeScale, _FrameIndex, s_point_clamp_sampler, _MainLightShadowmapSize.xy);
#elif _SHADOW_VSM
    attenuation = SampleVarianceShadowmap(_ShadowMap, s_linear_clamp_sampler, shadowCoord);
#elif _SHADOW_EVSM
    attenuation = SampleExponentialVarianceShadowmap(_ShadowMap, s_linear_clamp_sampler, shadowCoord, _ShadowExponents, _LightBleedingReduction);
#else
    // TODO: We could branch on if this light has soft shadows (shadowParams.y) to save perf on some platforms.
//#ifdef _SHADOWS_SOFT
    attenuation = SampleShadowmapFiltered(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
//#else
    // 1-tap hardware comparison
    //attenuation = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz);
//#endif
#endif

    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
}


float remapping(float shadow_factor, float minVal)
{
    return saturate((shadow_factor - minVal) / (1.0 - minVal));
}


half ComputeCascadeIndex(float3 positionWS)
{
    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres[0].xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres[1].xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres[2].xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres[3].xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distances2 < _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return 4 - dot(weights, half4(4, 3, 2, 1));
}

struct CascadeShadowData
{
    half cascadeIndex;
    float4 cascadeShadowSphere;
};

CascadeShadowData ComputeCascadeShadowData(float3 positionWS)
{
    CascadeShadowData cascadeShadowData;
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    cascadeShadowData.cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    cascadeShadowData.cascadeIndex = 0;
#endif

    return cascadeShadowData;
}

float4 TransformWorldToShadowCoord(float3 positionWS)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    // utilize w component of positionWS to store linear depth range
    //shadowCoord.w = _MainLightShadowDepthRange[cascadeIndex];

    return shadowCoord;

    // URP's original
    // return float4(shadowCoord.xyz, 0);
}


//half MainLightRealtimeShadow(float4 shadowCoord)
//{
//#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
//    return 1.0h;
//#endif
//
//    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
//    half4 shadowParams = GetMainLightShadowParams();
//
//    return SampleShadowmap(TEXTURE2D_ARGS(_ShadowMap, sampler_ShadowMap), shadowCoord, shadowSamplingData.positionSS, shadowSamplingData, shadowParams, false);
//}


float4 GetShadowCoord(float3 positionWS)
{
    return TransformWorldToShadowCoord(positionWS);
}

struct ShadowSampleCoords
{
    float4 shadow_coord;
#if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    float4 shadow_coord1;
    half   blend;
#endif
    int    cascadeIndex;
    float2 positionSS;
    float3 positionWS;
};

ShadowSampleCoords GetShadowSampleData(float3 positionWS, float2 positionSS)
{
    ShadowSampleCoords data = (ShadowSampleCoords)0;
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = 0;
#endif

    data.shadow_coord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));
    data.positionSS = positionSS;
    data.positionWS = positionWS;
#if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    data.cascadeIndex = cascadeIndex;
    data.shadow_coord1 = mul(_MainLightWorldToShadow[cascadeIndex + 1], float4(positionWS, 1.0));
    data.blend = 0;
    half4 shadowParams = GetMainLightShadowParams();
    if (shadowParams.z > 0)
    {
        //caculate data.blend
        half activeShadowCount = shadowParams.w;
        if (cascadeIndex < activeShadowCount - 1.0)
        {
            //float3 positionVS = mul(UNITY_MATRIX_V, float4(positionWS, 1));
            //half4 z4 = (float4(positionVS.z, positionVS.z, positionVS.z, positionVS.z) - _LightSplitsNear) / (_LightSplitsFar - _LightSplitsNear);
            //half alpha = z4[cascadeIndex];//dot(z4 * cascadeWeights, half4(1, 1, 1, 1));

            float4 cullingSphere = _CascadeShadowSplitSpheres[(int)cascadeIndex];
            float3 center = cullingSphere.xyz;
            float distToSphereCenter = length(positionWS - center);
            float distToSphereSurface = cullingSphere.w - distToSphereCenter;
            half z = (distToSphereCenter - distToSphereSurface) / cullingSphere.w;
            half alpha = z;
            //if (distToSphereSurface < shadowParams.z && distToSphereSurface >= 0)
            //{
                //float blendDist = shadowParams.z; // *projOnForward;
                //blendDist = min(cullingSphere.w * 0.5, blendDist);
                //data.blend = saturate(1.0 - distToSphereSurface / blendDist);
            //}
            if (alpha > 1.0 - shadowParams.z)
            {
                alpha = (alpha - (1.0 - shadowParams.z)) / shadowParams.z;
                data.blend = saturate(alpha);
            }
        }
    }
#endif
    return data;
}

half MainLightRealtimeShadow(ShadowSampleCoords sampleData)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return 1.0h;
#endif

    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    half s1 = SampleShadowmap(TEXTURE2D_ARGS(_ShadowMap, sampler_ShadowMap), sampleData.shadow_coord, sampleData.positionSS, 
        sampleData.cascadeIndex, shadowSamplingData, shadowParams, false);
#if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    if (shadowParams.z > 0.01 )
    {
        half s2 = sampleData.cascadeIndex < shadowParams.w - 1 ? SampleShadowmap(TEXTURE2D_ARGS(_ShadowMap, sampler_ShadowMap), sampleData.shadow_coord1,
            sampleData.positionSS, sampleData.cascadeIndex + 1.0, shadowSamplingData, shadowParams, false) : s1;
        s1 = lerp(s1, s2, sampleData.blend);
    }
#endif
    //float blendDistance = max(length(sampleData.positionWS) / 5.0, 1.0);  //one meter for blend shadowmap
    //float t = saturate(max(_ShadowDistance - length(sampleData.positionWS), 0) / blendDistance);
    //s1 = LerpWhiteTo(s1, t);
    return s1;
}


float3 ApplyAdaptiveShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    /*
    float cos = saturate(dot(lightDirection, normalWS));
    float sin = sqrt(1 - cos * cos);
    float tan = min(1, sin / cos);
    //float scale = 1 - clamp(dot(normalWS, lightDirection), 0, 0.9);
    float depthScale = tan* _ShadowBias.z;
    float normalScale = sin * _ShadowBias.z;
    */
    float cos = dot(normalWS, lightDirection);
    float sin = length(cross(normalWS, lightDirection));
    float depthScale = max(0.1, sin * rcp(cos)) * _ShadowBias.z;
    float normalScale = saturate(sin * rcp(cos * cos)) * _ShadowBias.z;
    positionWS -= lightDirection * _ShadowBias.x * depthScale;
    positionWS -= normalWS * _ShadowBias.y * normalScale;
    return positionWS;
}


float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * _ShadowBias.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

struct VertexInput
{
    uint vertexID     : SV_VertexID;
};

struct v2f
{
    float4 positionCS      : SV_POSITION;
    float4 texcoord : TEXCOORD0;
};

v2f VertScreenSpaceShadow(VertexInput input)
{
    v2f o;

    o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);

    float4 projPos = o.positionCS * 0.5;
    projPos.xy = projPos.xy + projPos.w;

    o.texcoord.xy = GetFullScreenTriangleTexCoord(input.vertexID);
#if !UNITY_UV_STARTS_AT_TOP
    //if (_FlipY)
    o.texcoord.y = 1 - o.texcoord.y;
#endif
    o.texcoord.zw = projPos.xy;

    return o;
}

half4 FragScreenSpaceShadow(v2f input) : SV_Target
{
    float deviceDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r; //LoadCameraDepth(input.positionCS.xy);
    #if !UNITY_REVERSED_Z
    deviceDepth = 2.0 * deviceDepth - 1.0;
    #endif
    if (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE)
        return 1.0f;
    float2 positionSS = input.texcoord.xy;
    PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSpaceShadowmapSize.zw, deviceDepth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
    float3 positionWS = ComputeWorldSpacePosition(input.texcoord.xy, deviceDepth, UNITY_MATRIX_I_VP);

    //float blendDistance = _MainLightShadowParams.z;
    half4 final = 1;

    ShadowSampleCoords shadowSample = GetShadowSampleData(positionWS, positionSS);
    float attenuation = MainLightRealtimeShadow(shadowSample);
    final.rgb = attenuation;
    //ShadowSampleCoords shadowSample2 = GetShadowSampleData(posInput.positionWS, posInput.positionSS);
    //final.rgb = MainLightRealtimeShadow(shadowSample2);

    return final;
}


#endif
