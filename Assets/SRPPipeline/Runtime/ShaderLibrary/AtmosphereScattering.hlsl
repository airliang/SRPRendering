#ifndef ATMOSPHERE_SCATTERING_INCLUDED
#define ATMOSPHERE_SCATTERING_INCLUDED

#include "PipelineCore.hlsl"


//In-Scattering Equation:
//I(po, v, l, λ) = I(λ) F(θ) β_R,M ∫[pa, pb] ρ(h(p)) T(pc,p,λ) T(p,pa,λ) dp
//where pa is the point on the ray where it enters the atmosphere, pb is the point where it leaves the atmosphere, 
//pc is the point on the sunlight ray where it intersects the sphere, and p is any point on the ray between pa and pb.
//θ is the angle between light direction and view direction.

float _AtmosphereHeight;
float _EarthRadius;
const static float2 _HeightScales = float2(8000.0, 1200.0);
float3 _BetaRayleigh;   //β_R
float3 _BetaMie;        //β_M
float _MieG;
float3 _SunLightColor;

float RaySphereIntersection(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius)
{
    rayOrigin -= sphereCenter;
    float a = dot(rayDir, rayDir);
    float b = 2.0 * dot(rayOrigin, rayDir);
    float c = dot(rayOrigin, rayOrigin) - (sphereRadius * sphereRadius);
    float d = b * b - 4 * a * c;
    if (d < 0)
    {
        return -1;
    }
    else
    {
        d = sqrt(d);
        float x0 = -0.5 * (b + d) / a;
        float x1 = -0.5 * (b - d) / a;

        if (x0 < 0)
        {
            x0 = x1;
            if (x0 < 0)
            {
                return -1;
            }
            return x0;
        }
        return x0 > 0 ? min(x0, x1) : x1;
    }
}

//Henyey-Greenstein function
float GetHGMiePhase(float cosTheta, float g)
{
    float g2 = g * g;
    return ((3.0 * (1.0 - g2)) 
        / (2.0 * (2.0 + g2))) * ((1 + cosTheta * cosTheta) / (pow((1 + g2 - 2 * g * cosTheta), 3.0 / 2.0)));
}

float GetRayleighPhase(float cosTheta)
{
    return 0.75 * (1.0 + cosTheta * cosTheta);
}

float GetModifyRayleighPhase(float cosTheta)
{
    return (8.0 / 10.0) * ((7.0 / 5.0) + 0.5 * cosTheta);
}

//use traditional mie phase function to simulate sun rendering
//As g is close to 1, the phase function will be larger when cosTheta is close to 1.
float3 SunSimulation(float cosTheta)
{
    float g = 0.98;
    float g2 = g * g;

    float sun = pow(1.0 - g, 2.0) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
    return sun * 0.002;
}

//float GetAtmosphereDensityRayleigh(float height)
//{
//    return exp(-height / _RayleighHeightScale);
//}
//
//float GetAtmosphereDensityMie(float height)
//{
//    return exp(-height / _MieHeightScale);
//}

float2 GetAtmosphereDensity(float height)
{
    return exp(-height.xx / _HeightScales);
}

//transmittance calculation
float2 Transmittance(float3 rayStart, float3 rayEnd, float3 earthCenter)
{
    float distance = length(rayEnd - rayStart);
    float sampleCount = 64.0;
    float3 step = (rayEnd - rayStart) / sampleCount;
    float stepSize = length(step);
    float3 rayDir = normalize(rayEnd - rayStart);
    float2 densityIntegral = 0;
    for (float s = 0.5; s < sampleCount; s++)
    {
        float3 samplePoint = rayStart + step * s;
        float height = abs(length(samplePoint - earthCenter) - _EarthRadius);
        //tRayleigh += densityR * stepSize;
        //tMie += densityM * stepSize;
        densityIntegral += GetAtmosphereDensity(height) * stepSize;
    }
    //float3 tRayleigh = densityIntegral.x * _BetaRayleigh;
    //float3 tMie = densityIntegral.y * _BetaMie;
    //return exp(-(tRayleigh + tMie));
    return densityIntegral;
}

float4 IntegrateInScattering(float3 rayStart, float3 rayEnd, float3 sunLight, float3 earthCenter)
{
    float sampleCount = 64.0;
    float3 step = (rayEnd - rayStart) / sampleCount;
    float stepSize = length(step);
    float3 scatteringR = 0;
    float3 scatteringM = 0;
    float3 tRayleigh = 0;
    float3 tMie = 0;
    float3 preScatteringR = 0;
    float3 preScatteringM = 0;
    for (float i = 0.5; i < sampleCount; i++)
    {
        float3 samplePoint = rayStart + step * i;
        float height = abs(length(samplePoint - earthCenter) - _EarthRadius);
        float2 localDensity = exp(-height.xx / _HeightScales.xy);
        float3 tPAR = 0;
        float3 tPAM = 0;
        float2 densityPA = Transmittance(rayStart, samplePoint, earthCenter);
        float2 densityCP = 0;

        
        float intersection = RaySphereIntersection(samplePoint, -sunLight, earthCenter, _EarthRadius);
        
        if (intersection > 0)
        {
            // intersection with planet, write high density
            densityCP = 1e+20;
        }
        else
        {
            intersection = RaySphereIntersection(samplePoint, -sunLight, earthCenter, _EarthRadius + _AtmosphereHeight);
            float3 pc = samplePoint - intersection * sunLight;
            densityCP = Transmittance(samplePoint, pc, earthCenter);
        }
        //float2 intersection = RaySphereIntersection(samplePoint, -sunLight, earthCenter, _EarthRadius + _AtmosphereHeight);
        //float3 pc = samplePoint - intersection.y * sunLight;
        //float3 tPC = Transmittance(samplePoint, pc, earthCenter);
        float2 densityCPA = densityCP + densityPA;
        float3 tR = densityCPA.x * _BetaRayleigh;
        float3 tM = densityCPA.y * _BetaMie;
        float3 t = exp(-tR - tM);
        float3 currentScatteringR = t * localDensity.x;
        float3 currentScatteringM = t * localDensity.y;
        scatteringR += (currentScatteringR + preScatteringR) * stepSize * 0.5;
        scatteringM += (currentScatteringM + preScatteringM) * stepSize * 0.5;
        preScatteringR = currentScatteringR;
        preScatteringM = currentScatteringM;
    }

    return float4(scatteringR, scatteringM.x);
}

#endif
