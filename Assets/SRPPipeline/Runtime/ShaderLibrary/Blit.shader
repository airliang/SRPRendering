Shader "Insanity/Blit"
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
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            //#pragma multi_compile _ _USE_DRAW_PROCEDURAL
            #pragma multi_compile_fragment _ _TONEMAPPING

            #pragma enable_d3d11_debug_symbols

            #include "Fullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "ColorConvert.hlsl"

            TEXTURE2D(_SourceTex);
            SAMPLER(sampler_SourceTex);
            float _Exposure;

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, input.uv);

             #ifdef _LINEAR_TO_SRGB_CONVERSION
                col = LinearToSRGB(col);
             #endif

#ifdef _TONEMAPPING
                col.rgb = ACESToneMapping(col.rgb, _Exposure);
#endif
                return col;
            }
            ENDHLSL
        }
    }
}
