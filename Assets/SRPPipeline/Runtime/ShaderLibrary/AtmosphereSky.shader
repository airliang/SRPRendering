Shader "Insanity/AtmosphereSky"
{
    Properties
    {
        //_MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "InsanityPipeline"}
        LOD 100

        Pass
        {
            Name "Atmosphere Scattering"
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION

            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "PipelineCore.hlsl"
            #include "AtmosphereScattering.hlsl"

            sampler3D _SkyboxLUT;
            sampler3D _MultipleScatteringLUT;
            float multipleScattering;
            float _RunderSun;

            float3 GetSkyViewDirWS(float2 positionCS)
            {
                float4 viewDirWS = mul(float4(positionCS.xy, 1.0f, 1.0f), _PixelCoordToViewDirWS);
                return normalize(viewDirWS.xyz);
            }

            struct Attributes
            {
                uint vertexID     : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
                return output;
            }

            //https://publications.lib.chalmers.se/records/fulltext/203057/203057.pdf
            half4 Fragment(Varyings input) : SV_Target
            {
                float3 rayStart = _WorldSpaceCameraPos;
                float3 viewDirWS = -GetSkyViewDirWS(input.positionCS.xy);
                float3 sunDir = -normalize(_MainLightPosition.xyz);
                // Reverse it to point into the scene
                float3 earthCenter = float3(0, -_EarthRadius, 0);
                float height = max(length(rayStart - earthCenter) - _EarthRadius, 0);
                float3 groundNormal = normalize(rayStart - earthCenter);
                float cosView = dot(groundNormal, viewDirWS);
                float cosSun = dot(groundNormal, -sunDir);

                float3 texCoords;
                texCoords.x = pow(height / _AtmosphereHeight, 0.5);
                float ch = -sqrt(height * (2 * _EarthRadius + height)) / (_EarthRadius + height);

                texCoords.y = cosView > ch ? (0.5 * pow((cosView - ch) / (1.0 - ch), 0.2) + 0.5) 
                    : (0.5 * pow((ch - cosView) / (1.0 + ch), 0.2));

                texCoords.z = 0.5 * ((atan(max(cosSun, -0.1975) * tan(1.26 * 1.1)) / 1.1) + (1 - 0.26));
                //texCoords = float3(0.9, 0.5, 0.5);
                half4 scattering = tex3D(_SkyboxLUT, texCoords);
                //return scattering;
                //
                float cosTheta = dot(viewDirWS, -sunDir);
                half3 scatteringR = scattering.rgb * GetModifyRayleighPhase(cosTheta) * _BetaRayleigh / (4.0 * PI);
                half3 sM = scattering.rgb * scattering.w / scattering.r;
                half3 scatteringM = sM * GetHGMiePhase(cosTheta, _MieG) * _BetaMie / (4.0 * PI);
                half3 skyColor = (scatteringR + scatteringM) * _MainLightIntensity * _SunLightColor;
                if (_RunderSun > 0)
                    skyColor += SunSimulation(cosTheta) * sM * _MainLightIntensity;
                return half4(skyColor, 1);
            }
            ENDHLSL
        }
    }
}
