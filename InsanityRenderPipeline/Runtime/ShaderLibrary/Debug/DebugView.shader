Shader "Insanity/DebugViewBlit"
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
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            //#pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #pragma enable_d3d11_debug_symbols
#pragma enable_vulkan_debug_symbols

            #include "../Fullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "../ColorConvert.hlsl"
            #include "DebugViewCommon.hlsl"
            #include "../LightCullingInclude.hlsl"
            //Texture2D<int> _TileVisibleCount;
            TEXTURE2D(_DepthTexture);
            SAMPLER(sampler_DepthTexture);
            TEXTURE2D(_NormalTexture);
            SAMPLER(sampler_NormalTexture);
            TEXTURE2D(_AOMask);
            SAMPLER(sampler_AOMask);
            TEXTURE2D(_AlbedoTexture);
            SAMPLER(sampler_AlbedoTexture);
            StructuredBuffer<int> _LightVisibilityIndexBuffer;

            static const uint nbColours = 10;
            static const float4 colours[nbColours] =
            {
                float4(0, 0, 0, 255),
                float4(2, 25, 147, 255),
                float4(0, 149, 255, 255),
                float4(0, 253, 255, 255),
                float4(142, 250, 0, 255),
                float4(255, 251, 0, 255),
                float4(255, 147, 0, 255),
                float4(255, 38, 0, 255),
                float4(148, 17, 0, 255),
                float4(255, 0, 255, 255)
            };

            half4 GetTileVisibleLightDebugColor(int lightCount)
            {
                return colours[min(lightCount, nbColours - 1)] / 255.0;
            }

            half4 Fragment(Varyings input) : SV_Target
            {   
                half4 col = 0; // SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, input.uv);
                
                if (_DebugViewMode == DebugTileBasedCullingResult)
                {
                    uint2 screenCoord = input.uv * _ScreenSize.xy;
                    uint2 tileId = uint2(floor(screenCoord / TILE_SIZE));
                    uint lightCount = 0;
                    uint lightIndexOffset = (tileId.y * _TileNumber.x + tileId.x) * MAX_LIGHT_NUM_PER_TILE;
                    int lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset];
                    for (int i = 0; i < MAX_LIGHT_NUM_PER_TILE && lightIndex >= 0; ++i)
                    {
                        lightCount++;
                        lightIndex = _LightVisibilityIndexBuffer[lightIndexOffset + i + 1];
                    }
                    col = GetTileVisibleLightDebugColor(lightCount);
                    col.a = lightCount > 0 ? 0.75 : 0.5;
                }
                else if (_DebugViewMode == DebugDepth)
                {
                    float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_DepthTexture, input.uv);
#if UNITY_REVERSED_Z
                    depth = depth > 0 ? (1.0 - depth) : 0;
#endif
                    col = half4(depth, depth, depth, 1);
                }
                else if (_DebugViewMode == DebugLinearDepth)
                {
                    float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_DepthTexture, input.uv);
            #if UNITY_REVERSED_Z
                    depth = depth > 0 ? (1.0 - depth) : 0;
            #endif
                    float4 clipPos = float4(input.uv * 2.0 - 1.0, depth, 1);
                    float4 viewPos = mul(_ProjInverse, clipPos);
                    viewPos /= viewPos.w;
                    col = half4(viewPos.z, viewPos.z, viewPos.z, 1);
                }
                else if (_DebugViewMode == DebugSSAO)
                {
                    half ao = SAMPLE_TEXTURE2D(_AOMask, s_linear_clamp_sampler, input.uv).r;
                    col = half4(ao, ao, ao, 1);
                }
                else if (_DebugViewMode == DebugNormal)
                {
                    col = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv);
                    col.a = 1;
                }
                else if (_DebugViewMode == DebugAlbedo)
                {
                    col = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, input.uv);
                    col.a = 1;
                }
                else if (_DebugViewMode == DebugMetallic)
                {
                    col = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, input.uv);
                    col = float4(col.aaa, 1);
                }
                else if (_DebugViewMode == DebugSmoothness)
                {
                    col = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv);
                    col = float4(col.aaa, 1);
                }

        return col;
}
            ENDHLSL
        }
    }
}
