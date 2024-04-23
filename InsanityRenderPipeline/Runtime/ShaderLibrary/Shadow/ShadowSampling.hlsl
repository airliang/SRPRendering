#ifndef SHADOW_SAMPLING_INCLUDED
#define SHADOW_SAMPLING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "PCSS.hlsl"

struct ShadowSamplingData
{
    half4 shadowOffset0;
    half4 shadowOffset1;
    half4 shadowOffset2;
    half4 shadowOffset3;
    float4 shadowmapSize;
    half softShadowQuality;
};

#define SOFT_SHADOW_QUALITY_OFF    half(0.0)
#define SOFT_SHADOW_QUALITY_LOW    half(1.0)
#define SOFT_SHADOW_QUALITY_MEDIUM half(2.0)
#define SOFT_SHADOW_QUALITY_HIGH   half(3.0)

const static float RECEIVER_PLANE_MIN_FRACTIONAL_ERROR = 0.025;

/**
 * Computes the receiver plane depth bias for the given shadow coord in screen space.
 *
 *		http://amd-dev.wpengine.netdna-cdn.com/wordpress/media/2012/10/Isidoro-ShadowMapping.pdf
 *      https://e79l3u2iro.feishu.cn/docx/AG5Ddcx78osYGkxiCnecqxOOnTh
 */
float2 GetReceiverPlaneDepthBias(float3 shadowCoord)
{
    float2 biasUV;
    float3 duvdx = ddx(shadowCoord);
    float3 duvdy = ddy(shadowCoord);

    biasUV.x = duvdy.y * duvdx.z - duvdx.y * duvdy.z;
    biasUV.y = duvdx.x * duvdy.z - duvdy.x * duvdx.z;
    biasUV *= 1.0f / ((duvdx.x * duvdy.y) - (duvdx.y * duvdy.x));
    return biasUV;
}

real SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    // 4-tap hardware comparison
    real4 attenuation4;
    attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset0.xyz);
    attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset1.xyz);
    attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset2.xyz);
    attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset3.xyz);
    return dot(attenuation4, 0.25);
}

real SampleShadowmapFilteredMediumQuality(TEXTURE2D_SHADOW_PARAM( ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    real fetchesWeights[9];
    real2 fetchesUV[9];
    SampleShadow_ComputeSamples_Tent_5x5(samplingData.shadowmapSize, shadowCoord.xy, fetchesWeights, fetchesUV);

    return fetchesWeights[0] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[0].xy, shadowCoord.z))
                + fetchesWeights[1] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[1].xy, shadowCoord.z))
                + fetchesWeights[2] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[2].xy, shadowCoord.z))
                + fetchesWeights[3] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[3].xy, shadowCoord.z))
                + fetchesWeights[4] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[4].xy, shadowCoord.z))
                + fetchesWeights[5] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[5].xy, shadowCoord.z))
                + fetchesWeights[6] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[6].xy, shadowCoord.z))
                + fetchesWeights[7] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[7].xy, shadowCoord.z))
                + fetchesWeights[8] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[8].xy, shadowCoord.z));
}

real SampleShadowmapFilteredHighQuality(TEXTURE2D_SHADOW_PARAM( ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    real fetchesWeights[16];
    real2 fetchesUV[16];
    SampleShadow_ComputeSamples_Tent_7x7(samplingData.shadowmapSize, shadowCoord.xy, fetchesWeights, fetchesUV);

    return fetchesWeights[0] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[0].xy, shadowCoord.z))
                + fetchesWeights[1] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[1].xy, shadowCoord.z))
                + fetchesWeights[2] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[2].xy, shadowCoord.z))
                + fetchesWeights[3] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[3].xy, shadowCoord.z))
                + fetchesWeights[4] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[4].xy, shadowCoord.z))
                + fetchesWeights[5] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[5].xy, shadowCoord.z))
                + fetchesWeights[6] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[6].xy, shadowCoord.z))
                + fetchesWeights[7] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[7].xy, shadowCoord.z))
                + fetchesWeights[8] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[8].xy, shadowCoord.z))
                + fetchesWeights[9] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[9].xy, shadowCoord.z))
                + fetchesWeights[10] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[10].xy, shadowCoord.z))
                + fetchesWeights[11] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[11].xy, shadowCoord.z))
                + fetchesWeights[12] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[12].xy, shadowCoord.z))
                + fetchesWeights[13] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[13].xy, shadowCoord.z))
                + fetchesWeights[14] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[14].xy, shadowCoord.z))
                + fetchesWeights[15] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[15].xy, shadowCoord.z));
}

real SampleShadowmapFiltered(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData)
{
    real attenuation;

//#if defined(SHADER_API_MOBILE) || defined(SHADER_API_SWITCH)
//    // 4-tap hardware comparison
//    real4 attenuation4;
//    attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset0.xyz);
//    attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset1.xyz);
//    attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset2.xyz);
//    attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz + samplingData.shadowOffset3.xyz);
//    attenuation = dot(attenuation4, 0.25);
//#else
//    float fetchesWeights[9];
//    float2 fetchesUV[9];
//    SampleShadow_ComputeSamples_Tent_5x5(samplingData.shadowmapSize, shadowCoord.xy, fetchesWeights, fetchesUV);

//    attenuation = fetchesWeights[0] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[0].xy, shadowCoord.z));
//    attenuation += fetchesWeights[1] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[1].xy, shadowCoord.z));
//    attenuation += fetchesWeights[2] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[2].xy, shadowCoord.z));
//    attenuation += fetchesWeights[3] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[3].xy, shadowCoord.z));
//    attenuation += fetchesWeights[4] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[4].xy, shadowCoord.z));
//    attenuation += fetchesWeights[5] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[5].xy, shadowCoord.z));
//    attenuation += fetchesWeights[6] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[6].xy, shadowCoord.z));
//    attenuation += fetchesWeights[7] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[7].xy, shadowCoord.z));
//    attenuation += fetchesWeights[8] * SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, float3(fetchesUV[8].xy, shadowCoord.z));
//#endif
    if (samplingData.softShadowQuality == SOFT_SHADOW_QUALITY_OFF)
    {
        attenuation = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz);
    }
    else if (samplingData.softShadowQuality == SOFT_SHADOW_QUALITY_LOW)
    {
        attenuation = SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else if(samplingData.softShadowQuality == SOFT_SHADOW_QUALITY_MEDIUM)
    {
        attenuation = SampleShadowmapFilteredMediumQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    else // SOFT_SHADOW_QUALITY_HIGH
    {
        attenuation = SampleShadowmapFilteredHighQuality(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
    }
    return
attenuation;
}

#ifdef _SHADOW_PCSS


real SampleShadowmapPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float2 posSS, 
    float cascadeScale, int frameCount, SamplerState samp, float2 ShadowMapTexelSize)
{
    float random = InterleavedGradientNoise(posSS.xy, frameCount);
    float2 receiverPlaneDepthBias = GetReceiverPlaneDepthBias(shadowCoord.xyz);
    float fractionalSamplingError = 2.0 * dot(ShadowMapTexelSize.xy, abs(receiverPlaneDepthBias));
    fractionalSamplingError = min(fractionalSamplingError, RECEIVER_PLANE_MIN_FRACTIONAL_ERROR);

#if defined(UNITY_REVERSED_Z)
    fractionalSamplingError *= -1.0;
#endif

    shadowCoord.z -= fractionalSamplingError;
    real attenuation = PCSS(shadowCoord, receiverPlaneDepthBias, random, cascadeScale, ShadowMap, sampler_ShadowMap, samp);
    return attenuation;
}
#endif

//remap formula: y = (2(x - minval))^3
//I want to remap the all the value less than minval as 0.
float remappingShadow(float shadow_factor, float minVal)
{
    float remap = 2 * (shadow_factor - minVal);
    return saturate(remap * remap * remap);//saturate((shadow_factor - minVal) / (1.0 - minVal));
}

//remap the shadow pMax: https://therealmjp.github.io/posts/shadow-sample-update/
float Linstep(float a, float b, float v)
{
    return saturate((v - a) / (b - a));
}

// Reduces VSM light bleedning
float ReduceLightBleeding(float pMax, float amount)
{
    // Remove the [0, amount] tail and linearly rescale (amount, 1].
    return Linstep(amount, 1.0f, pMax);
}

real ChebyshevEquation(float t, float mean, float variance)
{
    float temp = t - mean;
    float pmax = variance / (variance + temp * temp);

    float p = step(t, mean);
    return max(p, pmax);
}

#if _SHADOW_VSM

real SampleVarianceShadowmap(Texture2D shadowMap, SamplerState samp, float4 shadowCoord)
{
    float2 uv = shadowCoord.xy;
    float4 shadow = SAMPLE_TEXTURE2D_LOD(shadowMap, samp, uv, 0);
    //float4 shadow = SAMPLE_TEXTURE2D(shadowMap, samp, uv);
    float mean = shadow.r;
    float z = shadowCoord.z;
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    float minVariance = 0.0001;
    float variance = max(shadow.g - mean * mean, minVariance);
    float pMax = ChebyshevEquation(z, mean, variance);
    return ReduceLightBleeding(pMax, 0.2);//pMax < 1 ? remappingShadow(pMax, 0.5) : pMax;
}

#endif

#if _SHADOW_EVSM
real SampleExponentialVarianceShadowmap(Texture2D shadowMap, SamplerState samp, float4 shadowCoord, float2 exponentConstants, float lightBleedingAmount)
{
    float2 uv = shadowCoord.xy;
    float4 moments = SAMPLE_TEXTURE2D(shadowMap, samp, uv);

    
    float z = shadowCoord.z;
#if defined(UNITY_REVERSED_Z)
    z = 1 - z;
#endif
    float minVariance = 0.05;
    float posExp = exp(exponentConstants.x * z);
    float negExp = -exp(-exponentConstants.y * z);

    float variance = max(moments.g - moments.r * moments.r, minVariance);
    float posShadow = ChebyshevEquation(posExp, moments.r, variance);
    posShadow = ReduceLightBleeding(posShadow, lightBleedingAmount);
    variance = max(moments.a - moments.b * moments.b, minVariance);
    float negShadow = ChebyshevEquation(negExp, moments.b, variance);
    negShadow = ReduceLightBleeding(negShadow, lightBleedingAmount);
    float pMax = min(posShadow, negShadow);
    return pMax;//pMax < 1 ? remappingShadow(pMax, 0.5) : pMax;
}
#endif
#endif
