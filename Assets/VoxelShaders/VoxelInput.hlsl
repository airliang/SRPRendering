#ifndef VOXEL_INPUT_INCLUDED
#define VOXEL_INPUT_INCLUDED
#include "Assets/SRPPipeline/Runtime/ShaderLibrary/PipelineCore.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct SurfaceData
{
    half3 albedo;
    half  alpha;
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


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


inline void InitializeLitSurfaceData(out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    outSurfaceData.albedo = albedoAlpha.rgb;
    outSurfaceData.alpha = 1; //Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
}

void GenerateMatrices(uint instanceId, float3 chunkPosition)
{
    int u = instanceId % 128;
    int v = instanceId / 128;
    float4 position = _Positions.Load(int3(u, v, 0)) + float4(chunkPosition, 0);
    float scale = position.w;
    float revertScale = 1.0f / scale;
    unity_ObjectToWorld = float4x4(float4(scale,0,0,position.x), float4(0, scale, 0, position.y), float4(0, 0, scale, position.z), float4(0, 0, 0, 1));
    unity_WorldToObject = float4x4(float4(revertScale,0,0,-position.x), 
        float4(0, revertScale, 0, -position.y), float4(0, 0, revertScale, -position.z), float4(0, 0, 0, 1));
}

#endif // LIT_INPUT_INCLUDED
