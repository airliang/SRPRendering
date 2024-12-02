#ifndef IMAGE_BASED_LIGHTING
#define IMAGE_BASED_LIGHTING

#include "ShaderVariablesGlobal.hlsl"

TEXTURE2D(_BRDFLUTTex);
TEXTURECUBE(_ATMOSPHERE_SPECULAR);

#define PI 3.14159265359

inline half Pow5(half x)
{
    return x * x * x * x * x;
}

inline half3 Pow5(half3 x)
{
    return x * x * x * x * x;
}

inline half4 Pow5(half4 x)
{
    return x * x * x * x * x;
}

//F(v,h)¹«Ê½ cosTheta = v dot h
half3 FresnelSchlick(float cosTheta, half3 F0)
{
    return F0 + (1.0 - F0) * Pow5(1.0 - cosTheta);
}

half3 FresnelSchlickRoughness(float cosTheta, half3 F0, float roughness)
{
    float oneminusroughness = 1.0 - roughness;
    return F0 + (max(half3(oneminusroughness, oneminusroughness, oneminusroughness), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

half3 DiffuseLambert(half3 diffuse)
{
    return diffuse / PI;
}


//alpha = roughness * roughness
float NormalDistribution_GGX(float alpha, float ndh)
{
    if (ndh == 0)
        return 0;
    float alphaPow = alpha * alpha;
    float t = ndh * ndh * (alphaPow - 1) + 1;
    return alphaPow / (PI * t * t);
	//float ndhPow2 = ndh * ndh;
	//float tanSita_pow = (1 - ndhPow2) / (ndh * ndh + 0.00001);
	//float t = alphaPow + tanSita_pow;
	//float D = alphaPow * ndh / (ndhPow2 * ndhPow2 * PI * t * t);
	//return D;
}

float GGX_GSF(float roughness, float ndv, float ndl)
{
	//float tan_ndv_pow = (1 - ndv * ndv) / (ndv * ndv + 0.00001);

	//return (ndl / ndv) * 2 / (1 + sqrt(1 + roughness * roughness * tan_ndv_pow));
    float k = roughness / 2;


    float SmithL = (ndl) / (ndl * (1 - k) + k);
    float SmithV = (ndv) / (ndv * (1 - k) + k);


    float Gs = (SmithL * SmithV);
    return Gs;
}

float RoughnessToAlpha(float roughness)
{
    roughness = max(roughness, 0.0001);
    float x = log(roughness);
    return 1.62142f + 0.819955f * x + 0.1734f * x * x +
		0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
}

float BeckmannNormalDistribution(float roughness, float NdotH)
{
    float roughnessSqr = roughness * roughness;
    float NdotHSqr = NdotH * NdotH;
    return max(0.000001, (1.0 / (3.1415926535 * roughnessSqr * NdotHSqr * NdotHSqr)) * exp((NdotHSqr - 1) / (roughnessSqr * NdotHSqr)));
}

float BeckmannDistribution(float roughnessX, float roughnessY, float NdotH, float cosPhi)
{
    if (NdotH <= 0)
        return 0.0001;
    float cosTheta = NdotH;
    float sinTheta = sqrt(1.0 - cosTheta);
    float tanTheta = sinTheta / cosTheta;
    float tan2Theta = tanTheta * tanTheta;

    float cos4Theta = cosTheta * cosTheta * cosTheta * cosTheta;
    float alphax = RoughnessToAlpha(roughnessX);
    float alphay = RoughnessToAlpha(roughnessY);
    float cos2Phi = cosPhi * cosPhi;
    float sin2Phi = 1.0 - cos2Phi;
    return exp(-tan2Theta * (cos2Phi / (alphax * alphax) +
		sin2Phi / (alphay * alphay))) /
		(PI * alphax * alphay * cos4Theta);
}

//
float Smith_schilck(float roughness, float ndv, float ndl)
{
    float k = (roughness + 1) * (roughness + 1) / 8;
    float Gv = ndv / (ndv * (1 - k) + k);
    float Gl = ndl / (ndl * (1 - k) + k);
    return Gv * Gl;
}

float Schilck_GSF(float roughness, float ndv, float ndl)
{
    float roughnessSqr = roughness * roughness;
    float Gv = ndv / (ndv * (1 - roughnessSqr) + roughnessSqr);
    float Gl = ndl / (ndl * (1 - roughnessSqr) + roughnessSqr);
    return Gv * Gl;
}

float MixFunction(float i, float j, float x)
{
    return j * x + i * (1.0 - x);
}

float SchlickFresnel(float i)
{
    float x = clamp(1.0 - i, 0.0, 1.0);
    float x2 = x * x;
    return x2 * x2 * x;
}

float F0(float NdotL, float NdotV, float LdotH, float roughness)
{
    float FresnelLight = SchlickFresnel(NdotL);
    float FresnelView = SchlickFresnel(NdotV);
    float FresnelDiffuse90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
    return MixFunction(1, FresnelDiffuse90, FresnelLight) * MixFunction(1, FresnelDiffuse90, FresnelView);
}

half3 EnviromentIBLSpecular(float roughness, float NdV, float3 R, float3 kS)
{
    const float MAX_REFLECTION_LOD = 5.0;
    float mipLevel = floor(roughness * MAX_REFLECTION_LOD);
    float r_max = 1.0 / MAX_REFLECTION_LOD;
    float mipT = roughness * MAX_REFLECTION_LOD - mipLevel;
    half3 specularEnvColor = SAMPLE_TEXTURECUBE_LOD(_ATMOSPHERE_SPECULAR, s_linear_repeat_sampler, R, mipLevel).rgb;
    half3 specularEnvColor2 = SAMPLE_TEXTURECUBE_LOD(_ATMOSPHERE_SPECULAR, s_linear_repeat_sampler, R, mipLevel + 1).rgb;
    specularEnvColor = lerp(specularEnvColor, specularEnvColor2, mipT);
    half3 brdf = SAMPLE_TEXTURE2D_LOD(_BRDFLUTTex, s_point_repeat_sampler, half2(NdV, roughness), 0);
    half3 indirectSpecular = specularEnvColor * (kS * brdf.x + brdf.y);
    return indirectSpecular;

}

#endif
