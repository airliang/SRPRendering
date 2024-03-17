Shader "Insanity/AmbientSH"
{
	Properties
	{
        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0

        // Stencil State
        [HideInInspector] _StencilRef("__stencilRef", Float) = 128
        [HideInInspector] _StencilMask("__stencilMask", Float) = 128
        [HideInInspector] _StencilPass("__passOp", Float) = 0
        [HideInInspector] _StencilZFail("__zFailOp", Float) = 0
	}
	SubShader
	{
		Pass
		{
			Tags { "LightMode" = "DepthPrepass" }
			ColorMask 0
            ZWrite On
            Cull[_Cull]

			HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
			#pragma enable_d3d11_debug_symbols
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
			#include "LitInput.hlsl"
			#include "DepthOnlyPass.hlsl"
			
			ENDHLSL
		}

		Pass 
		{
			Tags { "LightMode" = "InsanityForward" }
			ZWrite Off
			ZTest Equal
            Cull[_Cull]

			HLSLPROGRAM
            //#pragma shader_feature_local _ALPHATEST_ON
			#pragma enable_d3d11_debug_symbols
			#pragma vertex LitPassVertex
			#pragma fragment AmbientSHFragment
            #pragma multi_compile_instancing
            //#pragma shader_feature_local _ALPHATEST_ON
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			//#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _SHADOW_PCSS
            #pragma multi_compile _ _VSM_SAT_FILTER
            #pragma multi_compile _ _SHADOW_VSM
            #pragma multi_compile _ _SHADOW_EVSM
			
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"

            half4 AmbientSHFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                //this function GetPositionInput here will not generate positionWS
                PositionInputs posInput = GetPositionInput(input.positionCS.xy, 1.0, uint2(input.positionCS.xy));

                float2 uv = posInput.positionNDC.xy;
                uint2 screenCoord = posInput.positionSS;

                //SurfaceData surfaceData;
                //InitializeLitSurfaceData(input.uv, surfaceData);

                InputData inputData;
                InitializeInputData(input, float3(0, 0, 1), inputData);
                half3 ambient = max(ShadeSH9(half4(inputData.normalWS, 1.0)), 0);
                half4 color = half4(ambient, 1.0);
                return color;
            }

			ENDHLSL			
		}
	}

            //CustomEditor "UnityEditor.Insanity.InsanityLitShaderGUI"
}
