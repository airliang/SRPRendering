#ifndef FULLSCREEN_INCLUDED
#define FULLSCREEN_INCLUDED

#include "PipelineCore.hlsl"

uniform int _FlipY;

#if _USE_DRAW_PROCEDURAL
void GetProceduralQuad(in uint vertexID, out float4 positionCS, out float2 uv)
{
    positionCS = GetQuadVertexPosition(vertexID);
    positionCS.xy = positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f);
    uv = GetQuadTexCoord(vertexID) * _ScaleBias.xy + _ScaleBias.zw;
}
#endif

struct Attributes
{
//#if _USE_DRAW_PROCEDURAL
    uint vertexID     : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
//#else
//    float4 positionOS : POSITION;
//    float2 uv         : TEXCOORD0;
//#endif
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
    output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);

    //Texcoord holds the coordinates of the original rendering before post processing.
    output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

//    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//    output.uv = input.uv;
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
