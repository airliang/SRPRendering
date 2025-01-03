using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    

    public partial class RenderPasses
    {
        public class DeferredShadingParams
        {
            public static int _DepthTexture;
            public static int _FinalLighting;
            public static int _AlbedoMetallic;
            public static int _NormalSmoothness;
            public static int _ShadowMap;
            public static int _ScreenSpaceShadowmapTexture;
            //public static int _ScreenSize;
            //public static int _ProjInverse;
            //public static int _ViewInverse;
            public static int _DeferredShadingKernel = -1;
        }

        public class DeferredShadingPassData
        {
            public TextureHandle albedoMetallic;
            public TextureHandle normalSmoothness;
            public TextureHandle finalLighting;
            public TextureHandle depthTexture;
            public TextureHandle shadowMap;
            public Vector4 screenSize;
            public bool m_AdditionalLightsEnable;
            public bool ssaoEnable = false;
            public bool screenSpaceShadow = false;
            public int cascadeCount = 1;
            public eShadowType shadowType = eShadowType.PCF;
            public eAdditionalLightCullingFunction m_AdditionalLightCullingFunction;
            public ComputeBuffer m_LightVisibilityIndexBuffer;
            public ComputeShader cs;
            public int kernel;
        }

        static ProfilingSampler s_DeferredShadingPassProfiler = new ProfilingSampler("Deferred Shading Pass Profiler");

        public static void InitializeDeferredShadingParameters()
        {
            DeferredShadingParams._DepthTexture = Shader.PropertyToID("_DepthTexture");
            DeferredShadingParams._FinalLighting = Shader.PropertyToID("_FinalLighting");
            DeferredShadingParams._AlbedoMetallic = Shader.PropertyToID("_AlbedoMetallic");
            DeferredShadingParams._NormalSmoothness = Shader.PropertyToID("_NormalSmoothness");
            DeferredShadingParams._ShadowMap = Shader.PropertyToID("_ShadowMap");
            DeferredShadingParams._ScreenSpaceShadowmapTexture = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
            //DeferredShadingParams._ScreenSize = Shader.PropertyToID("_ScreenSize");
            //DeferredShadingParams._ProjInverse = Shader.PropertyToID("_ProjInverse");
            //DeferredShadingParams._ViewInverse = Shader.PropertyToID("_ViewInverse");
        }

        public static void DeferredShadingPass(RenderingData renderingData, TextureHandle albedoMetallic, TextureHandle normalSmoothness,
            TextureHandle depth, TextureHandle shadowMap, TextureHandle output, ComputeShader deferredLighting)
        {
            if (DeferredShadingParams._DeferredShadingKernel == -1)
            {
                DeferredShadingParams._DeferredShadingKernel = deferredLighting.FindKernel("CSMain");
            }

            using (var builder = renderingData.renderGraph.AddRenderPass<DeferredShadingPassData>("Deferred Pass", out var passData, s_DeferredShadingPassProfiler))
            {
                builder.AllowPassCulling(false);
                passData.albedoMetallic = builder.ReadTexture(albedoMetallic);
                passData.normalSmoothness = builder.ReadTexture(normalSmoothness);
                passData.depthTexture = builder.ReadTexture(depth);
                passData.shadowMap = builder.ReadTexture(shadowMap);
                passData.finalLighting = builder.WriteTexture(output);
                passData.screenSize = renderingData.cameraData.screenSize;
                passData.cs = deferredLighting;
                passData.kernel = DeferredShadingParams._DeferredShadingKernel;
                passData.m_AdditionalLightsEnable = renderingData.supportAdditionalLights;
                passData.m_AdditionalLightCullingFunction = InsanityPipeline.asset.AdditonalLightCullingFunction;
                passData.ssaoEnable = InsanityPipeline.asset.SSAOEnable;
                passData.screenSpaceShadow = InsanityPipeline.asset.ScreenSpaceShadow;
                switch (InsanityPipeline.asset.shadowCascadeOption)
                {
                    case eShadowCascadesOption.NoCascades:
                        passData.cascadeCount = 1;
                        break;
                    case eShadowCascadesOption.TwoCascades: 
                        passData.cascadeCount = 2;
                        break;
                    case eShadowCascadesOption.FourCascades: 
                        passData.cascadeCount = 4;
                        break;
                    default:
                        passData.cascadeCount = 1;
                        break;
                }
                

                if (passData.m_AdditionalLightsEnable && passData.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased)
                {
                    passData.m_LightVisibilityIndexBuffer = LightCulling.Instance.LightsVisibilityIndexBuffer;
                }

                builder.SetRenderFunc((DeferredShadingPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._AlbedoMetallic, data.albedoMetallic);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._NormalSmoothness, data.normalSmoothness);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._DepthTexture, data.depthTexture);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, data.screenSpaceShadow ? DeferredShadingParams._ScreenSpaceShadowmapTexture : DeferredShadingParams._ShadowMap, data.shadowMap);

                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._FinalLighting, data.finalLighting);
                    context.cmd.EnableShaderKeyword("_ADDITIONAL_LIGHTS");
                    if (data.ssaoEnable)
                    {
                        context.cmd.EnableShaderKeyword("_SSAO_ENABLE");
                    }
                    else
                    {
                        context.cmd.DisableShaderKeyword("_SSAO_ENABLE");
                    }
                    if (data.m_AdditionalLightsEnable)
                    {
                        CoreUtils.SetKeyword(context.cmd, "_TILEBASED_LIGHT_CULLING", data.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased);
                        if (data.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased)
                        {
                            context.cmd.SetGlobalBuffer(LightCulling.LightCullingShaderParams._LightVisibilityIndexBuffer, data.m_LightVisibilityIndexBuffer);
                        }
                    }
                    CoreUtils.SetKeyword(context.cmd, "_MAIN_LIGHT_SHADOWS", true);
                    //CoreUtils.SetKeyword(cmd, "_SHADOWS_SOFT", passData.m_SoftShadows);
                    CoreUtils.SetKeyword(context.cmd, "_MAIN_LIGHT_SHADOWS_CASCADE", data.cascadeCount > 1);
                    CoreUtils.SetKeyword(context.cmd, "_SHADOW_PCSS", data.shadowType == eShadowType.PCSS);
                   
                    CoreUtils.SetKeyword(context.cmd, "_SHADOW_VSM", data.shadowType == eShadowType.VSM);
                    CoreUtils.SetKeyword(context.cmd, "_SHADOW_EVSM", data.shadowType == eShadowType.EVSM);
                    CoreUtils.SetKeyword(context.cmd, "_SCREENSPACE_SHADOW", data.screenSpaceShadow);

                    int groupX = Mathf.CeilToInt(data.screenSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.screenSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }
        }
    }
}
