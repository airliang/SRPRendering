#ifndef REALTIME_LIGHTS_INCLUDED
#define REALTIME_LIGHTS_INCLUDED

struct GPULight
{
    float4 position;  //xyz position w - range
    float4 direction; //xyz direction w spotlight angle 0 is point light
    float4 color;   // w - intensity
};

StructuredBuffer<GPULight> _GPUAdditionalLights;

// Compute the attenuation based on the range of the light.
half DoAttenuation(float rangeSqr, float distanceSqr)
{
    half attenuation = rcp(rangeSqr);
    float lightAtten = rcp(distanceSqr);
    half factor = half(distanceSqr * attenuation);

    half smoothFactor = saturate(half(1.0) - factor * factor);
    //smoothFactor = smoothFactor * smoothFactor;

    return lightAtten * smoothFactor;
    //return 1.0f - smoothstep(rangeSqr * 0.64f, rangeSqr, distanceSqr);
}

//https://www.3dgep.com/forward-plus/
float DoSpotCone(float spotRadian, float3 lightDirection, float3 spotDirection)
{
    // If the cosine angle of the light's direction 
    // vector and the vector from the light source to the point being 
    // shaded is less than minCos, then the spotlight contribution will be 0.
    float minCos = cos(spotRadian);
    // If the cosine angle of the light's direction vector
    // and the vector from the light source to the point being shaded
    // is greater than maxCos, then the spotlight contribution will be 1.
    float maxCos = lerp(minCos, 1, 0.5f);
    float cosAngle = dot(lightDirection, -spotDirection);
    // Blend between the minimum and maximum cosine angles.
    return smoothstep(minCos, maxCos, cosAngle);
}

// Matches Unity Vanila attenuation
// Attenuation smoothly decreases to light range.
float DistanceAttenuation(float distanceSqr, half2 distanceAttenuation)
{
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    float lightAtten = rcp(distanceSqr);

#if SHADER_HINT_NICE_QUALITY
    // Use the smoothing factor also used in the Unity lightmapper.
    half factor = distanceSqr * distanceAttenuation.x;
    half smoothFactor = saturate(1.0h - factor * factor);
    smoothFactor = smoothFactor * smoothFactor;
#else
    // We need to smoothly fade attenuation to light range. We start fading linearly at 80% of light range
    // Therefore:
    // fadeDistance = (0.8 * 0.8 * lightRangeSq)
    // smoothFactor = (lightRangeSqr - distanceSqr) / (lightRangeSqr - fadeDistance)
    // We can rewrite that to fit a MAD by doing
    // distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
    // distanceSqr *        distanceAttenuation.y            +             distanceAttenuation.z
    half smoothFactor = saturate(distanceSqr * distanceAttenuation.x + distanceAttenuation.y);
#endif

    return lightAtten * smoothFactor;
}

// UE4 light attenuation curve
/**
 * Returns a radial attenuation factor for a point light.
 * WorldLightVector is the vector from the position being shaded to the light, divided by the radius of the light.
 */
float RadialAttenuationMask(float3 WorldLightVector)
{
    float NormalizeDistanceSquared = dot(WorldLightVector, WorldLightVector);
    return 1.0f - clamp(NormalizeDistanceSquared, 0, 0.9999);
}
float RadialAttenuation(float3 WorldLightVector, half FalloffExponent)
{
	// UE3 (fast, but now we not use the default of 2 which looks quite bad):
    return pow(RadialAttenuationMask(WorldLightVector), FalloffExponent);
}


half AngleAttenuation(half3 spotDirection, half3 lightDirection, half2 spotAttenuation)
{
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}


int GetAdditionalLightsCount()
{
    return _AdditionalLightsCount;
}

#endif
