Shader "Insanity/Shadow PreFilter"
{
    SubShader
    {
        Pass
        {
            Name "ShadowFilterH"
            Tags{"LightMode" = "Shadow PreFilter"}
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
			#pragma vertex FullscreenVert
			#pragma fragment FragBlurH
            #pragma multi_compile GAUSSIAN5x5 GAUSSIAN9x9 GAUSSIAN13x13
            #pragma enable_d3d11_debug_symbols
			#include "ShadowPreFilter.hlsl"

			ENDHLSL
        }

        Pass
        {
            Name "ShadowFilterV"
            Tags{"LightMode" = "Shadow PreFilter"}
            Cull Off
            ZWrite Off
            ZTest Always


            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment FragBlurV
            #pragma multi_compile GAUSSIAN5x5 GAUSSIAN9x9 GAUSSIAN13x13
            #pragma enable_d3d11_debug_symbols
            #include "ShadowPreFilter.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowFilterOnePass"
            Tags{"LightMode" = "Shadow PreFilter"}
            Cull Off
            ZWrite Off
            ZTest Always


            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment FragBlurOnePass
            #pragma multi_compile GAUSSIAN3x3
            #pragma enable_d3d11_debug_symbols
            #include "ShadowPreFilter.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Lit Render Pipeline/FallbackError"
}
