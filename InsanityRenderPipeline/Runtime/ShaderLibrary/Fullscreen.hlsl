#ifndef FULLSCREEN_INCLUDED
#define FULLSCREEN_INCLUDED
#include "PipelineCore.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"


uniform int _FlipY;


struct Attributes
{
//#if _USE_DRAW_PROCEDURAL
    uint vertexID     : SV_VertexID;
//#else
//    float4 positionOS : POSITION;
//    float2 uv         : TEXCOORD0;
//#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
};

Varyings FullscreenVert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    

    //Texcoord holds the coordinates of the original rendering before post processing.
//#if _USE_DRAW_PROCEDURAL
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
//#else
//    output.positionCS = float4(input.positionOS.xyz, 1);
//    output.uv = input.uv;
//#endif
    
#if UNITY_UV_STARTS_AT_TOP
    if (_FlipY)
        output.uv.y = 1 - output.uv.y;
#endif
    return output;
}

Varyings Vert(Attributes input)
{
    return FullscreenVert(input);
}

#endif
