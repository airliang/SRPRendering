#ifndef DEBUGVIEW_PASS_INCLUDED
#define DEBUGVIEW_PASS_INCLUDED
#include "LightCullingInclude.hlsl"
#include "Lighting.hlsl"
#include "DebugViewCommon.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS : TEXCOORD1;
//#endif
    float3 normalWS : TEXCOORD2;

#if defined(_NORMAL_MAP)
    float4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif
    float3 viewDirWS : TEXCOORD4;

    UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
};

    float3 computeTangentFromNormal(float3 normal)
    {
        float3 tangent;
        float3 c1 = cross(normal, float3(0.0, 0.0, 1.0));
        float3 c2 = cross(normal, float3(0.0, 1.0, 0.0));
        if (length(c1) > length(c2))
            tangent = c1;
        else
            tangent = c2;
        return normalize(tangent);
    }

    void InitializeInputData(Varyings input, out InputData inputData)
    {
        inputData = (InputData) 0;

//#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
        inputData.positionWS = input.positionWS;
//#endif

        half3 viewDirWS = SafeNormalize(input.viewDirWS);

        inputData.normalWS = normalize(input.normalWS.xyz);

        inputData.viewDirectionWS = viewDirWS;

//#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
//    inputData.shadowCoord = input.shadowCoord;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
        inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

        inputData.bakedGI = max(half3(0, 0, 0), ShadeSH9(half4(inputData.normalWS, 1.0)));
    }

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
    Varyings DebugViewPassVertex(Attributes input)
    {
        Varyings output = (Varyings) 0;

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

#ifdef _NORMAL_MAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

        output.positionWS = vertexInput.positionWS;
        output.positionCS = vertexInput.positionCS;

        return output;
    }


// Used in Standard (Physically Based) shader
    half4 DebugViewPassFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(input);
    //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    //this function GetPositionInput here will not generate positionWS
        PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, uint2(input.positionCS.xy));

        float2 uv = posInput.positionNDC.xy;
        uint2 screenCoord = posInput.positionSS;

        SurfaceData surfaceData;
        InitializeLitSurfaceData(input.uv, surfaceData);

        InputData inputData;

        InitializeInputData(input, inputData);
        
        if (_DebugViewMode == DebugNormal)
        {
            return half4(inputData.normalWS, 1);
        }

        return 0;
    }

#endif
