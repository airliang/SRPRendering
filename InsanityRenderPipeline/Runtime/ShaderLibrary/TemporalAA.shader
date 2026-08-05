Shader "Insanity/TemporalAA"
{
    SubShader
    {
        Tags { "RenderPipeline" = "InsanityPipeline" }
        LOD 100

        Pass
        {
            Name "TemporalAA"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _USE_MOTION_VECTORS
            #pragma multi_compile_fragment _ _TAA_SRGB_BLEND

            #include "Fullscreen.hlsl"
            #include "ShaderVariablesGlobal.hlsl"
            #include "PipelineCore.hlsl"

            TEXTURE2D(_SourceTex);
            TEXTURE2D(_HistoryTex);
            TEXTURE2D(_CameraDepthTex);
            TEXTURE2D(_MotionVectorTex);

            float4 _MainTex_TexelSize;
            // x: g_WeightMax (min current-frame weight), y: 1/max motion pixels per frame, z: motion gamma, w: unused
            float4 _TAAParams;
            float _FirstFrame;

            float3 TAA_RGBToYCoCg(float3 rgb)
            {
                float y = dot(rgb, float3(0.25, 0.5, 0.25));
                float co = dot(rgb, float3(0.5, 0.0, -0.5)) + 0.5;
                float cg = dot(rgb, float3(-0.25, 0.5, -0.25)) + 0.5;
                return float3(y, co, cg);
            }

            float3 TAA_YCoCgToRGB(float3 yCoCg)
            {
                float y = yCoCg.x;
                float co = yCoCg.y - 0.5;
                float cg = yCoCg.z - 0.5;
                return float3(y + co - cg, y + cg, y - co - cg);
            }

            float SampleDeviceDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraDepthTex, s_point_clamp_sampler, uv).r;
            }

            float SampleLinear01Depth(float2 uv)
            {
                float deviceDepth = SampleDeviceDepth(uv);
                return 1.0 / (_ZBufferParams.x * deviceDepth + _ZBufferParams.y);
            }

            float3 SampleSourceRGB(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_SourceTex, s_point_clamp_sampler, uv).rgb;
            }

            float3 SrgbToLinearApprox(float3 srgb)
            {
                return srgb * srgb;
            }

            float3 LinearToSrgbApprox(float3 linearColor)
            {
                return sqrt(max(linearColor, 0.0));
            }

            float3 ToLinearColor(float3 color)
            {
            #ifdef _TAA_SRGB_BLEND
                return SrgbToLinearApprox(color);
            #else
                return color;
            #endif
            }

            float3 FromLinearColor(float3 color)
            {
            #ifdef _TAA_SRGB_BLEND
                return LinearToSrgbApprox(color);
            #else
                return color;
            #endif
            }

            // Catmull-Rom bicubic, 5 bilinear taps (Rainbow Six / MJP)
            float4 SampleHistoryBicubic(float2 uv)
            {
                float2 samplePos = uv * _MainTex_TexelSize.zw;
                float2 texPos1 = floor(samplePos - 0.5) + 0.5;
                float2 f = samplePos - texPos1;

                float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
                float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
                float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
                float2 w3 = f * f * (-0.5 + 0.5 * f);

                float2 w12 = w1 + w2;
                float2 texPos0 = (texPos1 - 1.0) * _MainTex_TexelSize.xy;
                float2 texPos3 = (texPos1 + 2.0) * _MainTex_TexelSize.xy;
                float2 texPos12 = (texPos1 + w2 / w12) * _MainTex_TexelSize.xy;

                float4 result = 0.0;
                result += SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, float2(texPos12.x, texPos0.y)) * (w12.x * w0.y);
                result += SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, float2(texPos0.x, texPos12.y)) * (w0.x * w12.y);
                result += SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, texPos12) * (w12.x * w12.y);
                result += SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, float2(texPos3.x, texPos12.y)) * (w3.x * w12.y);
                result += SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, float2(texPos12.x, texPos3.y)) * (w12.x * w3.y);
                return result;
            }

            void SearchDepthMinMax(float2 uv, out float zMin, out float zMax, out int2 minOffset)
            {
                zMin = 1e20;
                zMax = -1e20;
                minOffset = int2(0, 0);

                UNITY_UNROLL
                for (int y = -1; y <= 1; ++y)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; ++x)
                    {
                        float2 sampleUV = uv + float2(x, y) * _MainTex_TexelSize.xy;
                        float depth = SampleLinear01Depth(sampleUV);
                        if (depth < zMin)
                        {
                            zMin = depth;
                            minOffset = int2(x, y);
                        }
                        zMax = max(zMax, depth);
                    }
                }
            }

            float2 GetMotionVector(float2 uv, float2 positionCS, int2 minDepthOffset)
            {
            #ifdef _USE_MOTION_VECTORS
                float2 sampleUV = uv + float2(minDepthOffset) * _MainTex_TexelSize.xy;
                float deviceDepth = SampleDeviceDepth(sampleUV);
                // Sky / background: GBuffer has no MV. Derive UV motion from curr/prev view directions.
                if (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE)
                    return ComputeSkyMotionVectorUV(positionCS);
                return SAMPLE_TEXTURE2D(_MotionVectorTex, s_point_clamp_sampler, sampleUV).rg;
            #else
                float deviceDepth = SampleDeviceDepth(uv);
                if (deviceDepth == UNITY_RAW_FAR_CLIP_VALUE)
                    return ComputeSkyMotionVectorUV(positionCS);

                float4 clipPos = float4(uv * 2.0 - 1.0, deviceDepth, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                clipPos.y = -clipPos.y;
            #endif
                float4 worldPos = mul(_NonJitteredInvViewProjMatrix, clipPos);
                worldPos /= worldPos.w;
                // Camera-relative: unprojected WS is relative to current camera; prev VP expects prev-camera space.
                worldPos.xyz = GetPreviousFramePositionWS(worldPos.xyz);
                float4 prevClip = mul(_PrevNonJitteredViewProjMatrix, worldPos);
                prevClip /= prevClip.w;
            #if UNITY_UV_STARTS_AT_TOP
                prevClip.y *= _ProjectionParams.x;
            #endif
                float2 historyUV = prevClip.xy * 0.5 + 0.5;
                return uv - historyUV;
            #endif
            }

            float3 ClampHistory(float3 boundsMin, float3 boundsMax, float3 history)
            {
                float3 historyYCoCg = TAA_RGBToYCoCg(history);
                historyYCoCg = clamp(historyYCoCg, boundsMin, boundsMax);
                return TAA_YCoCgToRGB(historyYCoCg);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 positionCS = input.positionCS.xy;
                float weightMax = _TAAParams.x;

                if (_FirstFrame > 0.5)
                {
                    half4 current = SAMPLE_TEXTURE2D(_SourceTex, s_point_clamp_sampler, uv);
                    current.a = weightMax;
                    return current;
                }

                float zMin, zMax;
                int2 minDepthOffset;
                SearchDepthMinMax(uv, zMin, zMax, minDepthOffset);

                float divergentDepth = saturate(10.0 * (zMax - zMin) / max(0.001, zMax));

                float2 motionVector = GetMotionVector(uv, positionCS, minDepthOffset);
                float2 historyUV = uv - motionVector;

                if (any(historyUV < 0.0) || any(historyUV > 1.0))
                {
                    half4 current = SAMPLE_TEXTURE2D(_SourceTex, s_point_clamp_sampler, uv);
                    current.a = 1.0;
                    return current;
                }

                float4 accumulatedColorRGBA = saturate(SampleHistoryBicubic(historyUV));
                float accumulatedWeight = accumulatedColorRGBA.a;

                float motionLength = length(motionVector * _MainTex_TexelSize.zw);
                float motionLengthFactor = pow(saturate(motionLength * _TAAParams.y), _TAAParams.z);

                float2 ts = _MainTex_TexelSize.xy;
                float3 s10 = SampleSourceRGB(uv + float2(0.0, -ts.y));
                float3 s01 = SampleSourceRGB(uv + float2(-ts.x, 0.0));
                float3 s11 = SampleSourceRGB(uv);
                float3 s21 = SampleSourceRGB(uv + float2(ts.x, 0.0));
                float3 s12 = SampleSourceRGB(uv + float2(0.0, ts.y));

                float3 s10YCoCg = TAA_RGBToYCoCg(s10);
                float3 s01YCoCg = TAA_RGBToYCoCg(s01);
                float3 s11YCoCg = TAA_RGBToYCoCg(s11);
                float3 s21YCoCg = TAA_RGBToYCoCg(s21);
                float3 s12YCoCg = TAA_RGBToYCoCg(s12);

                float3 boundsMin = min(s10YCoCg, min(s01YCoCg, min(s11YCoCg, min(s21YCoCg, s12YCoCg))));
                float3 boundsMax = max(s10YCoCg, max(s01YCoCg, max(s11YCoCg, max(s21YCoCg, s12YCoCg))));

                float3 currentColor = saturate(s11);
                float3 clampedHistory = ClampHistory(boundsMin, boundsMax, accumulatedColorRGBA.rgb);

                float3 linearCurrent = ToLinearColor(currentColor);
                float3 linearHistory = ToLinearColor(clampedHistory);

                float weight = accumulatedWeight;
                weight = saturate(lerp(weight, 1.0, motionLengthFactor * (1.0 - divergentDepth)));
                weight = max(weight, motionLengthFactor);
                weight = max(weight, weightMax);

                float outputWeight = saturate(1.0 / (1.0 / weight + 1.0));

                float3 linearResult = linearCurrent * weight + linearHistory * (1.0 - weight);
                float3 gammaResult = FromLinearColor(linearResult);

                half4 result;
                result.rgb = gammaResult;
                result.a = outputWeight;
                return result;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
