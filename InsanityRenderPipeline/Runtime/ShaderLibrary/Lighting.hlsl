#ifndef LIGHTING_INCLUDED
#define LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "PipelineCore.hlsl"
#include "Shadow/Shadows.hlsl"
#include "RealtimeLights.hlsl"
#include "GlobalIllumination.hlsl"
#include "ImageBasedLighting.hlsl"

struct Light
{
    half3   direction;
    float3   color;     // fix precision problem.
    half    distanceAttenuation;
    half    shadowAttenuation;
    half    specularIntensity;
};

struct BRDFData
{
    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness^2 - 1.0
};

struct LightingData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half3   bakedGI;

    half occlusion;
    half alpha;
    half3 emission;

    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;
};

#ifdef _SSAO_ENABLE
TEXTURE2D(_AOMask);

half GetScreenSpaceAmbientOcclusion(float2 normalizedScreenSpaceUV)
{
    return SAMPLE_TEXTURE2D(_AOMask, sampler_LinearClamp, normalizedScreenSpaceUV).x;
}
#endif

///////////////////////////////////////////////////////////////////////////////
//                        Attenuation Functions                               /
///////////////////////////////////////////////////////////////////////////////


///////////////////////////////////////////////////////////////////////////////
//                      Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////

Light GetMainLight()
{
    Light light;
    light.direction = _MainLightPosition.xyz;
    // unity_LightData.z is 1 when not culled by the culling mask, otherwise 0.
    light.distanceAttenuation = _MainLightIntensity;
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;
    light.specularIntensity = 1.0;

    return light;
}

//Light GetMainLight(float4 shadowCoord)
//{
//    Light light = GetMainLight();
//    light.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);   //this is the shadow visibility
//
//    return light;
//}

Light GetMainLight(ShadowSampleCoords shadowSampleCoords)
{
    Light light = GetMainLight();
#ifdef _SCREENSPACE_SHADOW
    light.shadowAttenuation = SampleScreenSpaceShadowmap(shadowSampleCoords.positionSS.xy);
#else
    light.shadowAttenuation = MainLightRealtimeShadow(shadowSampleCoords);   //this is the shadow visibility
#endif

    return light;
}

Light GetAdditionalLight(int lightIndex, float3 positionWS)
{
    // Abstraction over Light input constants
    GPULight gpuLight = _GPUAdditionalLights[lightIndex];

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float intensity = gpuLight.color.a;
    float range = gpuLight.position.w;
    float3 lightPos = ApplyCameraTranslationToPosition(gpuLight.position.xyz);
    float3 lightVector = lightPos - positionWS;
    float distance = length(lightVector);
    
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);
    float spotRadian = gpuLight.direction.w;
    lightVector = normalize(lightVector);

    //half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    // full-float precision required on some platforms
    float attenuation = DoAttenuation(range * range, distance * distance) * (spotRadian > 0 ? DoSpotCone(spotRadian, gpuLight.direction.xyz, lightVector) : 1);
    attenuation *= intensity;
    Light light = (Light)0;
    light.direction = lightVector;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0; // This value can later be overridden in GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
    light.color = gpuLight.color.xyz;
    light.specularIntensity = 1.0;

    return light;
}

float square(float x) { return x * x; }


uint GetPerObjectLightIndexOffset()
{
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    return unity_LightData.x;
#else
    return 0;
#endif
}

// Returns a per-object index given a loop index.
// This abstract the underlying data implementation for storing lights/light indices
int GetPerObjectLightIndex(uint index)
{
/////////////////////////////////////////////////////////////////////////////////////////////
// Structured Buffer Path                                                                   /
//                                                                                          /
// Lights and light indices are stored in StructuredBuffer. We can just index them.         /
// Currently all non-mobile platforms take this path :(                                     /
// There are limitation in mobile GPUs to use SSBO (performance / no vertex shader support) /
/////////////////////////////////////////////////////////////////////////////////////////////
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    uint offset = unity_LightData.x;
    return _AdditionalLightsIndices[offset + index];

/////////////////////////////////////////////////////////////////////////////////////////////
// UBO path                                                                                 /
//                                                                                          /
// We store 8 light indices in float4 unity_LightIndices[2];                                /
// Due to memory alignment unity doesn't support int[] or float[]                           /
// Even trying to reinterpret cast the unity_LightIndices to float[] won't work             /
// it will cast to float4[] and create extra register pressure. :(                          /
/////////////////////////////////////////////////////////////////////////////////////////////
#elif !defined(SHADER_API_GLES)
    // since index is uint shader compiler will implement
    // div & mod as bitfield ops (shift and mask).

    // TODO: Can we index a float4? Currently compiler is
    // replacing unity_LightIndicesX[i] with a dp4 with identity matrix.
    // u_xlat16_40 = dot(unity_LightIndices[int(u_xlatu13)], ImmCB_0_0_0[u_xlati1]);
    // This increases both arithmetic and register pressure.
    return unity_LightIndices[index / 4][index % 4];
#else
    // Fallback to GLES2. No bitfield magic here :(.
    // We limit to 4 indices per object and only sample unity_4LightIndices0.
    // Conditional moves are branch free even on mali-400
    // small arithmetic cost but no extra register pressure from ImmCB_0_0_0 matrix.
    half2 lightIndex2 = (index < 2.0h) ? unity_LightIndices[0].xy : unity_LightIndices[0].zw;
    half i_rem = (index < 2.0h) ? index : index - 2.0h;
    return (i_rem < 1.0h) ? lightIndex2.x : lightIndex2.y;
#endif
}



///////////////////////////////////////////////////////////////////////////////
//                         BRDF Functions                                    //
///////////////////////////////////////////////////////////////////////////////

#define kDieletricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)



half ReflectivitySpecular(half3 specular)
{
#if defined(SHADER_API_GLES)
    return specular.r; // Red channel - because most metals are either monocrhome or with redish/yellowish tint
#else
    return max(max(specular.r, specular.g), specular.b);
#endif
}

half OneMinusReflectivityMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDieletricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = kDieletricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline void InitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, half alpha, out BRDFData outBRDFData)
{
#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = 1.0 - reflectivity;

    outBRDFData.diffuse = albedo * (half3(1.0h, 1.0h, 1.0h) - specular);
    outBRDFData.specular = specular;
#else

    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    outBRDFData.diffuse = albedo * oneMinusReflectivity;
    outBRDFData.specular = lerp(kDieletricSpec.rgb, albedo, metallic);
#endif

    outBRDFData.grazingTerm = saturate(smoothness + reflectivity);
    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness = max(PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness), HALF_MIN);
    outBRDFData.roughness2 = outBRDFData.roughness * outBRDFData.roughness;

    outBRDFData.normalizationTerm = outBRDFData.roughness * 4.0h + 2.0h;
    outBRDFData.roughness2MinusOne = outBRDFData.roughness2 - 1.0h;

#ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity;
#endif
}

half3 EnvironmentBRDF(BRDFData brdfData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    half3 c = indirectDiffuse * brdfData.diffuse;
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    c += surfaceReduction * indirectSpecular * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm);
    return c;
}

half3 EnvironmentBRDF(LightingData lightingData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
    half3 c = indirectDiffuse * lightingData.diffuse;
    float surfaceReduction = 1.0 / (lightingData.roughness2 + 1.0);
    c += surfaceReduction * indirectSpecular * lerp(lightingData.specular, lightingData.grazingTerm, fresnelTerm);
    return c;
}

//#define _SPECULARHIGHLIGHTS_OFF
// Based on Minimalist CookTorrance BRDF
// Implementation is slightly different from original derivation: http://www.thetenthplanet.de/archives/255
//
// * NDF [Modified] GGX
// * Modified Kelemen and Szirmay-Kalos for Visibility term
// * Fresnel approximated with 1/LdotH
half3 DirectBDRF(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, half specularIntensity)
{
#ifndef _SPECULARHIGHLIGHTS_OFF
    float3 halfDir = SafeNormalize(float3(lightDirectionWS)+float3(viewDirectionWS));

    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);

    // On platforms where half actually means something, the denominator has a risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    half3 color = specularTerm * brdfData.specular * specularIntensity + brdfData.diffuse;
    return color;
#else
    return brdfData.diffuse;
#endif
}

half3 DirectBDRF(LightingData lightingData, half3 lightDirectionWS, half specularIntensity)
{
#ifndef _SPECULARHIGHLIGHTS_OFF
    float3 halfDir = SafeNormalize(float3(lightDirectionWS)+float3(lightingData.viewDirectionWS));

    float NoH = saturate(dot(lightingData.normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * lightingData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = lightingData.roughness2 / ((d * d) * max(0.1h, LoH2) * lightingData.normalizationTerm);

    // On platforms where half actually means something, the denominator has a risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    half3 color = specularTerm * lightingData.specular * specularIntensity + lightingData.diffuse;
    return color;
#else
    return lightingData.diffuse;
#endif
}

half SpecularTerm(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
#ifndef _SPECULARHIGHLIGHTS_OFF

    float3 halfDir = SafeNormalize(float3(lightDirectionWS)+float3(viewDirectionWS));

    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);

    // On platforms where half actually means something, the denominator has a risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    return specularTerm;
#else
    return 1.0;
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                      Global Illumination                                  //
///////////////////////////////////////////////////////////////////////////////

// Samples SH L0, L1 and L2 terms
half3 SampleSH(half3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    real4 SHCoefficients[7];
    SHCoefficients[0] = unity_SHAr;
    SHCoefficients[1] = unity_SHAg;
    SHCoefficients[2] = unity_SHAb;
    SHCoefficients[3] = unity_SHBr;
    SHCoefficients[4] = unity_SHBg;
    SHCoefficients[5] = unity_SHBb;
    SHCoefficients[6] = unity_SHC;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

// SH Vertex Evaluation. Depending on target SH sampling might be
// done completely per vertex or mixed with L2 term per vertex and L0, L1
// per pixel. See SampleSHPixel
half3 SampleSHVertex(half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return max(half3(0, 0, 0), SampleSH(normalWS));
#elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
#endif

    // Fully per-pixel. Nothing to compute.
    return half3(0.0, 0.0, 0.0);
}

// SH Pixel Evaluation. Depending on target SH sampling might be done
// mixed or fully in pixel. See SampleSHVertex
half3 SampleSHPixel(half3 L2Term, half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return L2Term;
#elif defined(EVALUATE_SH_MIXED)
    half3 L0L1Term = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    return max(half3(0, 0, 0), L2Term + L0L1Term);
#endif

    // Default: Evaluate SH fully per-pixel
    return SampleSH(normalWS);
}

// Sample baked lightmap. Non-Direction and Directional if available.
// Realtime GI is not supported.
half3 SampleLightmap(float2 lightmapUV, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif

    half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);

    // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
    // However, universal pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
    // the compiler will optimize the transform away.
    half4 transformCoords = half4(1, 1, 0, 0);

#ifdef DIRLIGHTMAP_COMBINED
    return SampleDirectionalLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),
        TEXTURE2D_ARGS(unity_LightmapInd, samplerunity_Lightmap),
        lightmapUV, transformCoords, normalWS, encodedLightmap, decodeInstructions);
#elif defined(LIGHTMAP_ON)
    return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightmapUV, transformCoords, encodedLightmap, decodeInstructions);
#else
    return half3(0.0, 0.0, 0.0);
#endif
}

// We either sample GI from baked lightmap or from probes.
// If lightmap: sampleData.xy = lightmapUV
// If probe: sampleData.xyz = L2 SH terms
#ifdef LIGHTMAP_ON
#define SAMPLE_GI(lmName, shName, normalWSName) SampleLightmap(lmName, normalWSName)
#else
#define SAMPLE_GI(lmName, shName, normalWSName) SampleSHPixel(shName, normalWSName)
#endif

half3 BoxProjectedCubemapDirection(half3 reflectionWS, float3 positionWS, float4 cubemapPositionWS, float4 boxMin, float4 boxMax)
{
    // Is this probe using box projection?
    if (cubemapPositionWS.w > 0.0f)
    {
        float3 boxMinMax = (reflectionWS > 0.0f) ? boxMax.xyz : boxMin.xyz;
        half3 rbMinMax = half3(boxMinMax - positionWS) / reflectionWS;

        half fa = half(min(min(rbMinMax.x, rbMinMax.y), rbMinMax.z));

        half3 worldPos = half3(positionWS - cubemapPositionWS.xyz);

        half3 result = worldPos + reflectionWS * fa;
        return result;
    }
    else
    {
        return reflectionWS;
    }
}

///////////////////////////////////////////////////////////////////////////////
//                      Lighting Functions                                   //
///////////////////////////////////////////////////////////////////////////////
half3 LightingLambert(half3 lightColor, half3 lightDir, half3 normal)
{
    half NdotL = saturate(dot(normal, lightDir));
    return lightColor * NdotL;
}

half3 LightingSpecular(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir, half smoothness)
{
    float3 halfVec = SafeNormalize(float3(lightDir) + float3(viewDir));
    half NdotH = saturate(dot(normal, halfVec));
    half modifier = pow(NdotH, smoothness);
    half3 specularReflection = modifier;
    return lightColor * specularReflection;
}

half3 LightingPhysicallyBased(BRDFData brdfData, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half specularIntensity, half3 normalWS, half3 viewDirectionWS)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 radiance = lightColor * (lightAttenuation * NdotL);
    return DirectBDRF(brdfData, normalWS, lightDirectionWS, viewDirectionWS, specularIntensity) * radiance;
}

half3 LightingPhysicallyBased(LightingData lightingData, Light light)
{
    half NdotL = saturate(dot(lightingData.normalWS, light.direction));
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL);
    return DirectBDRF(lightingData, light.direction, light.specularIntensity) * radiance;
}

half4 GetRadiance(BRDFData brdfData, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half specularIntensity, half3 normalWS, half3 viewDirectionWS)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 radiance = lightColor * (lightAttenuation * NdotL);

    half3 specular = SpecularTerm(brdfData, normalWS, lightDirectionWS, viewDirectionWS) * radiance * specularIntensity;
    half luminanceSpec = Luminance(specular);
    return half4(radiance, luminanceSpec);
}

half4 GetRadiance(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS)
{
    return GetRadiance(brdfData, light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation, light.specularIntensity, normalWS, viewDirectionWS);
}

half3 LightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS)
{
    return LightingPhysicallyBased(brdfData, light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation, light.specularIntensity, normalWS, viewDirectionWS);
}

half3 AdditionalLightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS)
{
    return LightingPhysicallyBased(brdfData, light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation, light.specularIntensity, normalWS, viewDirectionWS);
}

#ifdef _TILEBASED_LIGHT_CULLING
#include "LightCullingInclude.hlsl"
StructuredBuffer<int> _LightVisibilityIndexBuffer;
void TileBasedAdditionalLightingFragmentBlinnPhong(float3 positionWS, float3 normalWS, float3 viewDirWS, float2 screenPos, 
    half smoothness, out half3 diffuse, out half3 specular)
{
    diffuse = 0;
    specular = 0;
    uint2 tileId = screenPos * (_TileNumber - 1);
    uint  lightIndexOffset = (tileId.y * _TileNumber.x + tileId.x) * MAX_LIGHT_NUM_PER_TILE;
    int lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset];
    for (int i = 0; i < MAX_LIGHT_NUM_PER_TILE && lightIndex >= 0; ++i)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS);
        diffuse += LightingLambert(light.color * light.distanceAttenuation, light.direction, normalWS);
        specular += LightingSpecular(light.color * light.distanceAttenuation, light.direction, normalWS, viewDirWS, smoothness);
        lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset + i + 1];
    }
}

void TileBasedAdditionalLightingFragmentPBR(BRDFData brdfData, float3 positionWS, float3 normalWS, float3 viewDirWS, float2 screenPos, out half3 outColor)
{
    outColor = 0;
    uint2 tileId = screenPos * (_TileNumber - 1);
    uint  lightIndexOffset = (tileId.y * _TileNumber.x + tileId.x) * MAX_LIGHT_NUM_PER_TILE;
    int lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset];
    for (int i = 0; i < MAX_LIGHT_NUM_PER_TILE && lightIndex >= 0; ++i)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS);
        outColor += AdditionalLightingPhysicallyBased(brdfData, light, normalWS, viewDirWS);
        lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset + i + 1];
    }
}
#endif

half3 VertexLighting(float3 positionWS, half3 normalWS)
{
    half3 vertexLightColor = half3(0.0, 0.0, 0.0);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    uint lightsCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0u; lightIndex < lightsCount; ++lightIndex)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS);
        half3 lightColor = light.color * light.distanceAttenuation;
        vertexLightColor += LightingLambert(lightColor, light.direction, normalWS);
    }
#endif

    return vertexLightColor;
}

half3 GlobalIllumination(BRDFData brdfData, half3 bakedGI, half occlusion, float3 positionWS, half3 normalWS, half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirectionWS)));

    half3 indirectDiffuse = bakedGI * occlusion;
    half3 indirectSpecular = EnviromentIBLSpecular(brdfData.perceptualRoughness, dot(normalWS, viewDirectionWS), reflectVector, brdfData.specular); //GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, occlusion);

    return EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
}

half4 FragmentBlinnPhong(InputData inputData, half3 diffuse, half4 specularGloss, half smoothness, half3 emission, half alpha, ShadowSampleCoords shadowSample)
{
    Light mainLight = GetMainLight(shadowSample);    

    half3 attenuatedLightColor = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
    half3 diffuseColor = inputData.bakedGI + LightingLambert(attenuatedLightColor, mainLight.direction, inputData.normalWS);
    half3 specularColor = LightingSpecular(attenuatedLightColor, mainLight.direction, inputData.normalWS, inputData.viewDirectionWS, smoothness);
    
#if defined(_ADDITIONAL_LIGHTS)
    #ifdef _TILEBASED_LIGHT_CULLING
    half3 diffuseAdditonal = 0;
    half3 specularAdditional = 0;
    TileBasedAdditionalLightingFragmentBlinnPhong(inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, inputData.positionSS, smoothness, diffuseAdditonal, specularAdditional);
    specularColor += specularAdditional;
    diffuseColor += diffuseAdditonal;
    #else
    uint pixelLightCount = GetAdditionalLightsCount();

    for (uint lightIndex = 0; lightIndex < min(pixelLightCount, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        //FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        diffuseColor += LightingLambert(light.color * light.distanceAttenuation, light.direction, inputData.normalWS);
    }
    #endif
#endif

    half3 finalColor = diffuseColor * diffuse + emission;
//#if defined(_SPECGLOSSMAP) || defined(_SPECULAR_COLOR)
    finalColor += specularColor * specularGloss.rgb;
//#endif

    return half4(finalColor, alpha);
}

///////////////////////////////////////////////////////////////////////////////
//                      Fragment Functions                                   //
//       Used by ShaderGraph and others builtin renderers                    //
///////////////////////////////////////////////////////////////////////////////
half4 FragmentPBR(InputData inputData, SurfaceData surface, ShadowSampleCoords shadowSample)
{
    // initialize brdf data.
    BRDFData brdfData;
    InitializeBRDFData(surface.albedo, surface.metallic, surface.specular, surface.smoothness, surface.alpha, brdfData);

    Light mainLight = GetMainLight(shadowSample);
    
#if _SSAO_ENABLE
    surface.occlusion = GetScreenSpaceAmbientOcclusion(inputData.positionSS);
#endif

    half3 color = GlobalIllumination(brdfData, inputData.bakedGI, surface.occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);
#if defined(_ADDITIONAL_LIGHTS)
    half3 diffuseAdditonal = 0;
    #ifdef _TILEBASED_LIGHT_CULLING
    TileBasedAdditionalLightingFragmentPBR(brdfData, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS, inputData.positionSS, diffuseAdditonal);

    #else
    uint pixelLightCount = GetAdditionalLightsCount();

    for (uint lightIndex = 0; lightIndex < min(pixelLightCount, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        //FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS);
        diffuseAdditonal += LightingPhysicallyBased(brdfData, additionalLight, inputData.normalWS, inputData.viewDirectionWS);
    }
    #endif
    color += diffuseAdditonal;
#endif
    color += surface.emission;
    return half4(color, surface.alpha);
}

#endif
