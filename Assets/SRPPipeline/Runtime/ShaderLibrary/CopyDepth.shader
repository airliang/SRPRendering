Shader "Insanity/CopyDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "InsanityPipeline"}
        LOD 100

        Pass
        {
            Name    "CopyDepth"
            ZTest Always
            ZWrite Off
            //Cull Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Frag
            #pragma multi_compile _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8

            #pragma enable_d3d11_debug_symbols

            #include "Fullscreen.hlsl"
#include "CopyDepthPass.hlsl"

            ENDHLSL
        }
    }
}
