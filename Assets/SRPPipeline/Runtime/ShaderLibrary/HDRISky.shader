Shader "Insanity/HDRISky"
{
    Properties
    {
        _Cubemap("Environment map", CUBE) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "InsanityPipeline"}
        LOD 100

        Pass
        {
            Name "HDRISky"
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION

            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "PipelineCore.hlsl"

            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);
            float4 _Cubemap_HDR;

            float3 GetSkyViewDirWS(float2 positionCS)
            {
                float2 positionNDC = positionCS * _ScreenSize.zw;
#if UNITY_REVERSED_Z
                float depth = 0;
#else
                float depth = 1;
#endif
                float4 viewDirWS = float4(ComputeWorldSpacePosition(positionNDC, depth, UNITY_MATRIX_I_VP), 1.0);//mul(float4(positionCS.xy, 1.0f, 1.0f), _PixelCoordToViewDirWS);
                return normalize(viewDirWS.xyz);
            }

            struct Attributes
            {
                uint vertexID     : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 viewDirWS = GetSkyViewDirWS(input.positionCS.xy);
                // Reverse it to point into the scene
                float3 dir = viewDirWS;
                half3 skyColor = DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(_Cubemap, sampler_Cubemap, dir, 0), _Cubemap_HDR);
                return half4(skyColor, 1);
            }
            ENDHLSL
        }
    }
}
