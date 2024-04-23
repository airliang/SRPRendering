#ifndef SHADOW_PREFILTER
#define SHADOW_PREFILTER

#include "../Fullscreen.hlsl"
#include "Shadows.hlsl"

TEXTURE2D_FLOAT(_MainLightShadowmapBlur1); SAMPLER(sampler_MainLightShadowmapBlur1);
TEXTURE2D_FLOAT(_MainLightShadowmapBlur); SAMPLER(sampler_MainLightShadowmapBlur);
//TEXTURE2D(_MainLightShadowmapTexture);


//float4 _MainLightShadowmapSize;

//struct Attributes
//{
//    uint vertexID     : SV_VertexID;
//};
//
//struct Varyings
//{
//    float4 positionCS   : SV_POSITION;
//    float2 uv           : TEXCOORD0;
//    UNITY_VERTEX_INPUT_INSTANCE_ID
//    UNITY_VERTEX_OUTPUT_STEREO
//};


// Vertex shader are copied from CopyDepthPass.hlsl
/*
Varyings QuadVertexShader(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);

    //Texcoord holds the coordinates of the original rendering before post processing.
    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

    //output.positionCS = float4(input.positionHCS.xyz, 1.0);
#if UNITY_UV_STARTS_AT_TOP
    output.uv.y = 1.0 - output.uv.y;
#endif
    return output;
}
*/

#if GAUSSIAN9x9
const static int kTapCount = 5;
const static float kOffsets[] = {
    -3.23076923,
    -1.38461538,
        0.00000000,
        1.38461538,
        3.23076923
};
const static float kCoeffs[] = {
        0.07027027,
        0.31621622,
        0.22702703,
        0.31621622,
        0.07027027
};

float4 Gaussian(Varyings input, TEXTURE2D_FLOAT(SourceTex), SAMPLER(sampler_SourceTex), int2 dir)
{
    float4 color = float4(0, 0, 0, 0);

    float2 uv = input.uv;

    float2 off1 = dir * 1.3846153846;
    float2 off2 = dir * 3.2307692308;
    float2 tapUV = uv;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.2270270270;
    tapUV = uv + off1 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.3162162162;
    tapUV = uv - off1 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.3162162162;
    tapUV = uv + off2 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.0702702703;
    tapUV = uv - off2 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.0702702703;

    return color;
}
#elif GAUSSIAN5x5
const static int kTapCount = 3;
    const static float kOffsets[] = {
    -1.33333333,
        0.00000000,
        1.33333333
};
const static float kCoeffs[] = {
        0.35294118,
        0.29411765,
        0.35294118
};

float4 Gaussian(Varyings input, TEXTURE2D_FLOAT(SourceTex), SAMPLER(sampler_SourceTex), int2 dir)
{
    float4 color = float4(0, 0, 0, 0);

    float2 uv = input.uv;

    float2 off1 = dir * 1.3333333333333333;
    float2 tapUV = uv;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.29411764705882354;
    tapUV = uv + off1 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.35294117647058826;
    tapUV = uv - off1 * _MainLightShadowmapSize.xy;
    color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.35294117647058826;

    return color;
}
#elif GAUSSIAN13x13
const static int kTapCount = 7;
const static float kOffsets[] = {
    -5.176470588235294,
    -3.2941176470588234,
    -1.33333333,
    0.00000000,
    1.33333333,
    3.2941176470588234,
    5.176470588235294
};
const static float kCoeffs[] = {
    0.010381362401148057,
    0.09447039785044732,
    0.2969069646728344,
    0.1964825501511404,
    0.2969069646728344,
    0.09447039785044732,
    0.010381362401148057
};

const static float Coefficients[] =
{ 0.000272337, 0.00089296, 0.002583865, 0.00659813, 0.014869116,
 0.029570767, 0.051898313, 0.080381679, 0.109868729, 0.132526984,
 0.14107424,
 0.132526984, 0.109868729, 0.080381679, 0.051898313, 0.029570767,
 0.014869116, 0.00659813, 0.002583865, 0.00089296, 0.000272337 };

float4 Gaussian(Varyings input, TEXTURE2D_FLOAT(SourceTex), SAMPLER(sampler_SourceTex), int2 dir)
{
    float4 color = float4(0, 0, 0, 0);

    float2 uv = input.uv;

    //float2 off1 = dir * 1.3333333333333333;
    //float2 off2 = dir * 3.2941176470588234;
    //float2 off3 = dir * 5.176470588235294;
    //float2 tapUV = uv;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.1964825501511404;
    //tapUV = uv + off1 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.2969069646728344;
    //tapUV = uv - off1 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.2969069646728344;
    //tapUV = uv + off2 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.09447039785044732;
    //tapUV = uv - off2 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.09447039785044732;
    //tapUV = uv + off3 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.010381362401148057;
    //tapUV = uv - off3 * _MainLightShadowmapSize.xy;
    //color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * 0.010381362401148057;
    
    for (int Index = 0; Index < 21; Index++)
    {
        float2 tapUV = uv + dir * _MainLightShadowmapSize.xy * (Index - 10);
        color += SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0) * Coefficients[Index];
    }

    return color;
}
#elif GAUSSIAN3x3

const static int kTapCount = 4;
const static float2 kOffsets[] = {
   float2(-0.6, -0.6),
   float2(-0.6,  0.6),
   float2( 0.6,  -0.6),
   float2( 0.6, 0.6)
};
const static float kCoeffs[] = {
     0.25, 0.25, 0.25, 0.25
};

#endif
/*
float4 Gaussian(Varyings input, TEXTURE2D_FLOAT(SourceTex), SAMPLER(sampler_SourceTex), int2 dir)
{
    float4 blurredColor = 0.0;

    float2 uv = input.uv;

    UNITY_UNROLL
    for (int i = 0; i < kTapCount; i++)
    {
        float2 tapUV = uv + kOffsets[i] * dir * _MainLightShadowmapSize.xy;
        float4 color = SAMPLE_TEXTURE2D_LOD(SourceTex, sampler_SourceTex, tapUV, 0);
        blurredColor += kCoeffs[i] * color;
    }

    return blurredColor;
}
*/

#if defined(GAUSSIAN9x9) || defined(GAUSSIAN5x5) || defined(GAUSSIAN13x13)
float4 FragBlurH(Varyings input) : SV_Target
{
    return Gaussian(input, TEXTURE2D_ARGS(_ShadowMap, s_linear_clamp_sampler), int2(1, 0));
}

float4 FragBlurV(Varyings input) : SV_Target
{
    return Gaussian(input, TEXTURE2D_ARGS(_MainLightShadowmapBlur, s_linear_clamp_sampler), int2(0, 1));
}
#endif

float4 FragBlurOnePass(Varyings input) : SV_Target
{
    float4 blurredColor = 0.0;

    float2 uv = input.uv;

    UNITY_UNROLL
    for (int i = 0; i < kTapCount; i++)
    {
        float2 tapUV = uv + kOffsets[i] * _MainLightShadowmapSize.xy;
        float4 color = SAMPLE_TEXTURE2D_LOD(_ShadowMap, s_linear_clamp_sampler, tapUV, 0);
        blurredColor += kCoeffs[i] * color;
    }

    return blurredColor;
}


#endif
