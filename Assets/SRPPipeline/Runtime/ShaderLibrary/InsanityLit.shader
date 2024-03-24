Shader"Insanity/Lit"
{
	Properties
	{
		[MainTexture] _BaseMap("Albedo", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _NormalMap("Normal", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0.0, 1.0)) = 1.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        // Blending state
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _ColorMaskShadow("__colormask", Float) = 0

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
			Name "ShadowCaster"
			Tags{"LightMode" = "ShadowCaster"}
            ColorMask [_ColorMaskShadow]
			ZWrite On
			ZTest LEqual
			Cull[_Cull]

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
            #pragma shader_feature_local _ALPHATEST_ON
			#pragma enable_d3d11_debug_symbols
			#pragma multi_compile _ _ADAPTIVE_SHADOW_BIAS
            #pragma multi_compile _ _SHADOW_VSM
            #pragma multi_compile _ _SHADOW_EVSM
			//#pragma prefer_hlslcc gles
			//#pragma exclude_renderers d3d11_9x
			#pragma target 3.0


			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			#include "LitInput.hlsl"
			#include "ShadowCasterPass.hlsl"
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
			#pragma fragment LitPassFragment
            #pragma multi_compile_instancing
            //#pragma shader_feature_local _ALPHATEST_ON
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS 
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SCREENSPACE_SHADOW
			#pragma multi_compile _ _SHADOW_PCSS
            #pragma multi_compile _ _VSM_SAT_FILTER
            #pragma multi_compile _ _SHADOW_VSM
            #pragma multi_compile _ _SHADOW_EVSM
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _TILEBASED_LIGHT_CULLING
            #pragma shader_feature_local _NORMALMAP
			
			#include "LitInput.hlsl"
			#include "LitForwardPass.hlsl"

			ENDHLSL			
		}

        Pass 
        {
            Tags { "LightMode" = "DebugView" }
            ZWrite Off
            ZTest Equal
            Cull[_Cull]

            HLSLPROGRAM
            //#pragma shader_feature_local _ALPHATEST_ON
			#pragma enable_d3d11_debug_symbols
			#pragma vertex DebugViewPassVertex
			#pragma fragment DebugViewPassFragment
            #pragma multi_compile_instancing
            //#pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _DEBUGVIEW
			
#include "LitInput.hlsl"
#include "DebugViewPass.hlsl"

			ENDHLSL
        }
	}

            CustomEditor "UnityEditor.Insanity.InsanityLitShaderGUI"
}
