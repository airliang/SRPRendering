#ifndef LIT_INPUT_INCLUDED
#define LIT_INPUT_INCLUDED
#include "PipelineCore.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct SurfaceData
{
    half3 albedo;
    half metallic;
    half smoothness;
    half3 normalTS;
    half  alpha;
};

struct InputData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    half3   bakedGI;
    float4  shadowCoord;
    float2  positionSS;
    half    fogCoord;
};

TEXTURE2D(_BaseMap);            
SAMPLER(sampler_BaseMap);

#ifdef _NORMALMAP
TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);
#endif

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _Cutoff;
#ifdef _NORMALMAP
half _NormalScale;
#endif
CBUFFER_END

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
#if defined(_ALPHATEST_ON)
    clip(alpha - cutoff);
#endif

    return alpha;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv);
}

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM( normalMap, sampler_normalMap), half scale = 1.0h)
{
#ifdef _NORMALMAP
    half4 n = SAMPLE_TEXTURE2D_MEGA(normalMap, sampler_normalMap, uv);
    return UnpackNormalScale(n, scale);
#else
    return half3(0.0h, 0.0h, 1.0h);
#endif
}

inline void InitializeLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
    outSurfaceData.albedo = albedoAlpha.rgb;
    outSurfaceData.alpha = _BaseColor.a * albedoAlpha.a; //Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
#ifdef _NORMALMAP
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_MEGA_ARGS(_NormalMap, sampler_NormalMap), _NormalScale);
#else
    outSurfaceData.normalTS = half3(0, 0, 1);
#endif
    outSurfaceData.metallic = 0;
    outSurfaceData.smoothness = 0;

}

#endif // LIT_INPUT_INCLUDED
