#ifndef VOXEL_INSTANCE_PASS_INCLUDED
#define VOXEL_INSTANCE_PASS_INCLUDED
//#include "Assets/SRPPipeline/Runtime/ShaderLibrary/PipelineCore.hlsl"
//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Assets/SRPPipeline/Runtime/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Assets/SRPPipeline/Runtime/ShaderLibrary/Shadow/Shadows.hlsl"


struct VoxelAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    //UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VoxelVaryings
{
    float4 positionCS               : SV_POSITION;
    float3 positionWS               : TEXCOORD0;
    float3 normalWS                 : TEXCOORD1;
    half3 color                    : TEXCOORD2;
    uint   instanceID : SV_InstanceID;
    //UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 positionCS   : SV_POSITION;
    //UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 _ChunkPosition;

void InitializeInputData(VoxelVaryings input, out InputData inputData)
{
    inputData = (InputData)0;

    //#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
    //#endif

    //half3 viewDirWS = SafeNormalize(input.viewDirWS);

    inputData.normalWS = normalize(input.normalWS.xyz);

    //inputData.viewDirectionWS = viewDirWS;

    //#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    //    inputData.shadowCoord = input.shadowCoord;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.bakedGI = max(half3(0, 0, 0), ShadeSH9(half4(inputData.normalWS, 1.0)));
}

VoxelVaryings VoxelInstanceVertex(VoxelAttributes input, uint instanceID : SV_InstanceID)
{
    VoxelVaryings output;
    //UNITY_SETUP_INSTANCE_ID(input);
//#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    GenerateMatrices(instanceID, _ChunkPosition);
//#endif
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.positionWS = TransformObjectToWorld(input.positionOS).xyz;
    output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
    output.color = GetColor(instanceID);
    output.instanceID = instanceID;
    //UNITY_TRANSFER_INSTANCE_ID(input, output);
    return output;
}

half4 VoxelInstanceFragment(VoxelVaryings input) : SV_Target
{
    //UNITY_SETUP_INSTANCE_ID(input);
    //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    //this function GetPositionInput here will not generate positionWS
    PositionInputs posInput = GetPositionInput(input.positionCS.xy, 1.0/* / _ScaledScreenParams.xy*/, uint2(input.positionCS.xy));

    float2 uv = posInput.positionNDC.xy;
    uint2 screenCoord = posInput.positionSS;

    SurfaceData surfaceData;
    InitializeLitSurfaceData(surfaceData, input.color);

    InputData inputData;

    InitializeInputData(input, inputData);
    ShadowSampleCoords shadowSample = GetShadowSampleData(input.positionWS, posInput.positionSS.xy);
    half4 color = FragmentBlinnPhong(inputData, surfaceData.albedo, 0, 0, 0, surfaceData.alpha, shadowSample);//half4(surfaceData.albedo, surfaceData.alpha);
    return color;
}

DepthVaryings DepthOnlyVertex(VoxelAttributes input, uint instanceID : SV_InstanceID)
{
    DepthVaryings output;
//#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    GenerateMatrices(instanceID, _ChunkPosition);
//#endif
    output.positionCS = TransformObjectToHClip(input.positionOS);
    //UNITY_SETUP_INSTANCE_ID(input);
    return output;
}

half4 DepthOnlyFragment(VoxelVaryings input) : SV_TARGET
{
    //UNITY_SETUP_INSTANCE_ID(input);

    return 0;
}


float4 GetShadowPositionHClip(VoxelAttributes input)
{
    
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_SHADOW_VSM)
    float4 positionCS = TransformWorldToHClip(positionWS);
#else
#if defined(_ADAPTIVE_SHADOW_BIAS)
    float4 positionCS = TransformWorldToHClip(ApplyAdaptiveShadowBias(positionWS, normalWS, _LightDirection));
#else
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
#endif
#endif

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}


DepthVaryings ShadowPassVertex(VoxelAttributes input, uint instanceID : SV_InstanceID)
{
    DepthVaryings output;
    //#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    GenerateMatrices(instanceID, _ChunkPosition);
    //#endif
    output.positionCS = GetShadowPositionHClip(input);
    return output;
}

half4 ShadowPassFragment(DepthVaryings input) : SV_TARGET
{
#if defined(_SHADOW_VSM) || defined(_SHADOW_EVSM)
    float depth = input.positionCS.z;
#if UNITY_REVERSED_Z
    depth = 1.0 - depth;
#endif
#if defined(_SHADOW_EVSM)
    float pos = exp(_ShadowExponents.x * depth);
    float neg = -exp(-_ShadowExponents.y * depth);
    return half4(pos, pos * pos, neg, neg * neg);
#else
    float dx = ddx(depth);
    float dy = ddy(depth);

    //float firstMoment = depth; // redundant
    float secondMoment = depth * depth + 0.25 * (dx * dx + dy * dy);
    return half4(depth, secondMoment, 0, 0);
#endif
#else
    return 0;
#endif
}

#endif // VOXEL_INSTANCE_PASS_INCLUDED
