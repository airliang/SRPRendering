#ifndef COPY_DEPTH_PASS_INCLUDED
#define COPY_DEPTH_PASS_INCLUDED

#if defined(_DEPTH_MSAA_2)
    #define MSAA_SAMPLES 2
#elif defined(_DEPTH_MSAA_4)
    #define MSAA_SAMPLES 4
#elif defined(_DEPTH_MSAA_8)
    #define MSAA_SAMPLES 8
#else
    #define MSAA_SAMPLES 1
#endif

#define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
#define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
#define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_CameraDepthAttachment, uv, sampleIndex)
#define SAMPLE(uv) SAMPLE_DEPTH_TEXTURE(_CameraDepthAttachment, sampler_CameraDepthAttachment, uv)


#if MSAA_SAMPLES == 1
    DEPTH_TEXTURE(_CameraDepthAttachment);
    SAMPLER(sampler_CameraDepthAttachment);
    
#else
    DEPTH_TEXTURE_MS(_CameraDepthAttachment, MSAA_SAMPLES);
    float4 _CameraDepthAttachment_TexelSize;
#endif

#if UNITY_REVERSED_Z
    #define DEPTH_DEFAULT_VALUE 1.0
    #define DEPTH_OP min
#else
    #define DEPTH_DEFAULT_VALUE 0.0
    #define DEPTH_OP max
#endif

float SampleDepth(float2 uv)
{
#if MSAA_SAMPLES == 1
    return SAMPLE(uv);
    
#else
    int2 coord = int2(uv * _CameraDepthAttachment_TexelSize.zw);
    float outDepth = DEPTH_DEFAULT_VALUE;

    UNITY_UNROLL
    for (int i = 0; i < MSAA_SAMPLES; ++i)
        outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
    return outDepth;
#endif
}


float Frag(Varyings input) : SV_Target
{
    //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return SampleDepth(input.uv);
}

#endif
