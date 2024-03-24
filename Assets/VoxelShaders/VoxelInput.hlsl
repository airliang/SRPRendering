#ifndef VOXEL_INPUT_INCLUDED
#define VOXEL_INPUT_INCLUDED
#include "Assets/SRPPipeline/Runtime/ShaderLibrary/PipelineCore.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#define OneOver256 0.00390625

struct SurfaceData
{
    half3 albedo;
    half3 specular;
    half metallic;
    half smoothness;
    half3 normalTS;
    half3 emission;
    half occlusion;
    half alpha;
};

struct InputData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    half3   bakedGI;
    float4  shadowCoord;
    half    fogCoord;
};

TEXTURE2D_FLOAT(_Positions);
Texture2D<uint> _Colors;

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float3 IntToColor(int color)
{
    return float3(
        ((color >> 24) & 255) * OneOver256,
        ((color >> 16) & 255) * OneOver256,
        ((color >> 8) & 255) * OneOver256
        );
}

inline void InitializeLitSurfaceData(out SurfaceData outSurfaceData, half3 color)
{
    outSurfaceData = (SurfaceData)0;
    outSurfaceData.albedo = color;
    outSurfaceData.alpha = 1; //Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
}

void GenerateMatrices(uint instanceId)
{
    int u = instanceId % 128;
    int v = instanceId / 128;
    float4 position = _Positions.Load(int3(u, v, 0));
    float scale = position.w;
    float revertScale = 1.0f / scale;
    unity_ObjectToWorld = float4x4(float4(scale,0,0,position.x), float4(0, scale, 0, position.y), float4(0, 0, scale, position.z), float4(0, 0, 0, 1));
    unity_WorldToObject = float4x4(float4(revertScale,0,0,-position.x), 
        float4(0, revertScale, 0, -position.y), float4(0, 0, revertScale, -position.z), float4(0, 0, 0, 1));
}

half3 GetColor(uint instanceId)
{
    int u = instanceId % 128;
    int v = instanceId / 128;
    uint color = _Colors.Load(int3(u, v, 0));
    return IntToColor(color);
}

#endif // LIT_INPUT_INCLUDED
