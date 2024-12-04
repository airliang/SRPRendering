#ifndef LIT_FORWARD_PASS_INCLUDED
#define LIT_FORWARD_PASS_INCLUDED
#include "Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Shadow/Shadows.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GBufferOutput
{
    half4 AlbedoMetallic : SV_Target0;
    half4 NormalSmoothness : SV_Target1;
    //half4 Specular : SV_Target2;
};

struct Varyings
{
    float4 positionCS               : SV_POSITION;
    float2 uv                       : TEXCOORD0;

//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
//#endif

    float3 normalWS                 : TEXCOORD2;

#if defined(_NORMALMAP)
    float4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

    float3 viewDirWS                : TEXCOORD4;

    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light

//#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    //float4 shadowCoord              : TEXCOORD6;
//#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
};

float3 computeTangentFromNormal(float3 normal) {
    float3 tangent;
    float3 c1 = cross(normal, float3(0.0, 0.0, 1.0));
    float3 c2 = cross(normal, float3(0.0, 1.0, 0.0));
    if (length(c1) > length(c2))
        tangent = c1;
    else
        tangent = c2;
    return normalize(tangent);
}

void InitializeInputData(Varyings input, float3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, uint2(input.positionCS.xy));

    inputData.positionWS = input.positionWS;
    inputData.positionSS = posInput.positionNDC.xy;

    half3 viewDirWS = SafeNormalize(input.viewDirWS);

#ifdef _NORMALMAP
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
    inputData.normalWS = SafeNormalize(inputData.normalWS);
#else
    inputData.normalWS = normalize(input.normalWS.xyz);
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings GBufferPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    //UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
    //half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;

#ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;

    return output;
}


// Used in Standard (Physically Based) shader
GBufferOutput GBufferPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    //this function GetPositionInput here will not generate positionWS
    //PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, uint2(input.positionCS.xy));

    //float2 uv = posInput.positionNDC.xy;
    //uint2 screenCoord = posInput.positionSS;

    SurfaceData surfaceData;
    InitializeLitSurfaceData(input.uv, surfaceData);

    InputData inputData;

    InitializeInputData(input, surfaceData.normalTS, inputData);
    GBufferOutput output = (GBufferOutput)0;
    output.AlbedoMetallic = half4(surfaceData.albedo, surfaceData.metallic);
    output.NormalSmoothness = half4(input.normalWS * 0.5 + 0.5, surfaceData.smoothness);
    return output;
}

#endif // LIT_FORWARD_PASS_INCLUDED
