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

            #include "Fullscreen.hlsl"
            #include "ShaderVariablesGlobal.hlsl"
            #include "PipelineCore.hlsl"

            TEXTURE2D(_SourceTex);
            TEXTURE2D(_HistoryTex);
            TEXTURE2D(_CameraDepthTex);
            TEXTURE2D(_MotionVectorTex);

            float4 _MainTex_TexelSize;
            // x: base feedback, y: linear depth reject scale (1/meters), z: base variance gamma, w: far adapt strength
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

            float SampleDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraDepthTex, s_point_clamp_sampler, uv).r;
            }

            float TAA_Linear01Depth(float deviceDepth)
            {
                return 1.0 / (_ZBufferParams.x * deviceDepth + _ZBufferParams.y);
            }

            float TAA_LinearEyeDepth(float deviceDepth)
            {
                return 1.0 / (_ZBufferParams.z * deviceDepth + _ZBufferParams.w);
            }

            // 0 = near/stable, 1 = far/sub-pixel (needs stronger temporal filtering)
            float GetFarFactor(float deviceDepth)
            {
                float linear01 = TAA_Linear01Depth(deviceDepth);
                return smoothstep(0.2, 0.9, linear01) * _TAAParams.w;
            }

            float2 GetDilatedVelocity(float2 uv)
            {
            #ifdef _USE_MOTION_VECTORS
                float bestDepth = -1.0;
                float2 bestVelocity = 0.0;

                UNITY_UNROLL
                for (int x = -1; x <= 1; ++x)
                {
                    UNITY_UNROLL
                    for (int y = -1; y <= 1; ++y)
                    {
                        float2 sampleUV = uv + float2(x, y) * _MainTex_TexelSize.xy;
                        float depth = SampleDepth(sampleUV);
                    #if UNITY_REVERSED_Z
                        if (depth > bestDepth)
                    #else
                        if (bestDepth < 0.0 || depth < bestDepth)
                    #endif
                        {
                            bestDepth = depth;
                            bestVelocity = SAMPLE_TEXTURE2D(_MotionVectorTex, s_point_clamp_sampler, sampleUV).rg;
                        }
                    }
                }

                return bestVelocity;
            #else
                return 0.0;
            #endif
            }

            float2 GetHistoryUV(float2 uv, float farFactor)
            {
            #ifdef _USE_MOTION_VECTORS
                float2 velocity = GetDilatedVelocity(uv);
                return uv - velocity;
            #else
                float deviceDepth = SampleDepth(uv);
                float4 clipPos = float4(uv * 2.0 - 1.0, deviceDepth, 1.0);
                float4 worldPos = mul(_NonJitteredInvViewProjMatrix, clipPos);
                worldPos /= worldPos.w;

                float4 prevClip = mul(_PrevNonJitteredViewProjMatrix, worldPos);
                prevClip /= prevClip.w;
            #if UNITY_UV_STARTS_AT_TOP
                prevClip.y *= _ProjectionParams.x;
            #endif
                return prevClip.xy * 0.5 + 0.5;
            #endif
            }

            float3 ClipHistoryVariance(float3 history, float2 uv, float gamma)
            {
                float3 m1 = 0.0;
                float3 m2 = 0.0;

                UNITY_UNROLL
                for (int x = -1; x <= 1; ++x)
                {
                    UNITY_UNROLL
                    for (int y = -1; y <= 1; ++y)
                    {
                        float3 sampleRGB = SAMPLE_TEXTURE2D(_SourceTex, s_point_clamp_sampler, uv + float2(x, y) * _MainTex_TexelSize.xy).rgb;
                        float3 sampleYCoCg = TAA_RGBToYCoCg(sampleRGB);
                        m1 += sampleYCoCg;
                        m2 += sampleYCoCg * sampleYCoCg;
                    }
                }

                m1 /= 9.0;
                m2 /= 9.0;
                float3 sigma = sqrt(max(0.0, m2 - m1 * m1));
                float3 minYCoCg = m1 - gamma * sigma;
                float3 maxYCoCg = m1 + gamma * sigma;

                float3 historyYCoCg = TAA_RGBToYCoCg(history);
                historyYCoCg = clamp(historyYCoCg, minYCoCg, maxYCoCg);
                return TAA_YCoCgToRGB(historyYCoCg);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half4 current = SAMPLE_TEXTURE2D(_SourceTex, s_point_clamp_sampler, uv);

                if (_FirstFrame > 0.5)
                    return current;

                float currentDepth = SampleDepth(uv);
                float farFactor = GetFarFactor(currentDepth);

                float2 historyUV = GetHistoryUV(uv, farFactor);
                if (any(historyUV < 0.0) || any(historyUV > 1.0))
                    return current;

                half4 history = SAMPLE_TEXTURE2D(_HistoryTex, s_linear_clamp_sampler, historyUV);

                float eyeDepthCurrent = TAA_LinearEyeDepth(currentDepth);
                float eyeDepthHistory = TAA_LinearEyeDepth(SampleDepth(historyUV));
                float depthReject = saturate(abs(eyeDepthCurrent - eyeDepthHistory) * _TAAParams.y);
                // Far sub-pixel surfaces have unreliable depth; trust history more.
                depthReject *= (1.0 - farFactor * 0.75);

                float varianceGamma = lerp(_TAAParams.z, _TAAParams.z * 2.0, farFactor);
                history.rgb = ClipHistoryVariance(history.rgb, uv, varianceGamma);

                float feedback = lerp(_TAAParams.x, _TAAParams.x * 0.2, farFactor);
                feedback = lerp(feedback, 1.0, depthReject);

                half4 result;
                result.rgb = lerp(history.rgb, current.rgb, feedback);
                result.a = current.a;
                return result;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
