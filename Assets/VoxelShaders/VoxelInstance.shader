Shader "Insanity/VoxelInstance"
{
	Properties
	{
		//[MainTexture] _BaseMap("Albedo", 2D) = "white" {}
		[MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
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
			#include "VoxelInput.hlsl"
			#include "VoxelInstancePass.hlsl"
			
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags{"LightMode" = "ShadowCaster"}
            ColorMask 0
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

			#pragma target 3.0


			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment

			#include "VoxelInput.hlsl"
			#include "VoxelInstancePass.hlsl"
			ENDHLSL
		}

		Pass 
		{
			Tags { "LightMode" = "InsanityForward" }
			ZWrite On
			ZTest LEqual
            Cull[_Cull]

			HLSLPROGRAM

			#pragma enable_d3d11_debug_symbols
			#pragma vertex VoxelInstanceVertex
			#pragma fragment VoxelInstanceFragment
            #pragma multi_compile_instancing
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _SHADOW_PCSS
            #pragma multi_compile _ _VSM_SAT_FILTER
            #pragma multi_compile _ _SHADOW_VSM
            #pragma multi_compile _ _SHADOW_EVSM
			
			#include "VoxelInput.hlsl"
			#include "VoxelInstancePass.hlsl"

			ENDHLSL			
		}
	}

            CustomEditor "UnityEditor.Insanity.InsanityLitShaderGUI"
}
