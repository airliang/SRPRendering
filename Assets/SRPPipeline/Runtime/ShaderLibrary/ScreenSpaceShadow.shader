Shader "Insanity/ScreenSpaceShadow"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "InsanityPipeline"}
        LOD 100

        Pass
        {
            Name "Blit"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertScreenSpaceShadow
            #pragma fragment FragScreenSpaceShadow
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            //#pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOW_PCSS

            #pragma enable_d3d11_debug_symbols

            #include "Shadow/Shadows.hlsl"

            ENDHLSL
        }
    }
}
