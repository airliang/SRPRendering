using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static Insanity.InsanityPipeline;

namespace Insanity
{
    public partial class InsanityPipeline
    {
        LightVariablesGlobal m_LightVariablesGlobalCB = new LightVariablesGlobal();
        Light m_sunLight;
        ShadowManager m_ShadowMananger;
        int m_mainLightIndex = -1;
        ShadowSettings m_shadowSettings;

        static int GetMainLightIndex(NativeArray<VisibleLight> visibleLights)
        {
            int totalVisibleLights = visibleLights.Length;

            if (totalVisibleLights == 0)
                return -1;

            Light sunLight = RenderSettings.sun;
            int brightestDirectionalLightIndex = -1;
            float brightestLightIntensity = 0.0f;

            VisibleLight currVisibleLight = visibleLights[0];
            Light currLight = currVisibleLight.light;

            // Particle system lights have the light property as null. We sort lights so all particles lights
            // come last. Therefore, if first light is particle light then all lights are particle lights.
            // In this case we either have no main light or already found it.
            if (currLight == null)
                return -1;

            if (currLight == sunLight)
                return 0;

            // In case no shadow light is present we will return the brightest directional light
            if (currVisibleLight.lightType == LightType.Directional && currLight.intensity > brightestLightIntensity)
            {
                brightestLightIntensity = currLight.intensity;
                brightestDirectionalLightIndex = 0;
            }


            return brightestDirectionalLightIndex;
        }

        void ProcessVisibleLights(ref CullingResults cullResults)
        {

            m_mainLightIndex = GetMainLightIndex(cullResults.visibleLights);
            if (m_mainLightIndex >= 0)
                m_sunLight = cullResults.visibleLights[m_mainLightIndex].light;
            //update light to shader constants

        }

        void PrepareGPULightData(CommandBuffer cmd, ref CullingResults cullResults, CameraData cameraData)
        {
            UpdateLightVariablesGlobalCB(cmd);

            bool mainLightCastShadows = false;
            if (asset.shadowDistance > 0)
            {
                mainLightCastShadows = (m_mainLightIndex != -1 && m_sunLight != null &&
                                        m_sunLight.shadows != LightShadows.None);
            }

            //Prepare shadow datas
            if (m_ShadowMananger == null)
                m_ShadowMananger = new ShadowManager();

            InitShadowSettings(mainLightCastShadows);

            Matrix4x4 invViewProjection = Matrix4x4.identity;

            for (int i = 0; i < m_shadowSettings.mainLightShadowCascadesCount; i++)
            {
                ShadowRequest shadowRequest = m_ShadowMananger.GetShadowRequest(i);
                m_ShadowMananger.UpdateDirectionalShadowRequest(shadowRequest, m_shadowSettings, cullResults.visibleLights[m_mainLightIndex],
                    ref cullResults, i, m_mainLightIndex, cameraData.mainViewConstants.worldSpaceCameraPos, out invViewProjection);

                m_ShadowMananger.SetShadowRequestSetting(shadowRequest, i, cameraData.mainViewConstants.worldSpaceCameraPos, invViewProjection);
            }
        }

        int GetLightShadowResolution(Light light)
        {
            switch (light.shadowResolution)
            {
                case LightShadowResolution.FromQualitySettings:
                    return 512;
                case LightShadowResolution.Low:
                    return 512;
                case LightShadowResolution.Medium:
                    return 1024;
                case LightShadowResolution.High:
                    return 2048;
                case LightShadowResolution.VeryHigh:
                    return 4096;
                default:
                    return 512;
            }
        }

        void InitShadowSettings(bool mainLightCastShadows)
        {
            if (m_shadowSettings == null)
                m_shadowSettings = new ShadowSettings();


            m_shadowSettings.supportsMainLightShadows = SystemInfo.supportsShadows /*&& settings.supportsMainLightShadows*/ && mainLightCastShadows;
            m_shadowSettings.maxShadowDistance = asset.shadowDistance;
            m_shadowSettings.supportSoftShadow = asset.supportsSoftShadows;
            m_shadowSettings.shadowType = asset.ShadowType;
            m_shadowSettings.shadowPCFFilter = asset.PCFFilter;
            m_shadowSettings.cascade2Split = asset.cascade2Split;
            m_shadowSettings.cascade4Split = asset.cascade4Split;
            m_shadowSettings.adaptiveShadowBias = asset.adaptiveShadowBias;
            m_shadowSettings.depthBias = asset.shadowDepthBias;
            m_shadowSettings.normalBias = asset.shadowNormalBias;
            m_shadowSettings.csmBlendDistance = asset.CSMBlendDistance;
            m_shadowSettings.csmBlendEnable = asset.enableCSMBlend;
            m_shadowSettings.pcssSoftness = asset.PCSSSoftness;
            m_shadowSettings.pcssSoftnessFalloff = asset.PCSSSoftnessFalloff;
            m_shadowSettings.pcssSatEnable = asset.PCSSSATEnable;
            m_shadowSettings.mainLightResolution = GetLightShadowResolution(m_sunLight);
            m_shadowSettings.prefilterGaussianRadius = asset.ShadowPrefilterGaussian;
            m_shadowSettings.exponentialConstants = asset.EVSMExponentConstants;
            m_shadowSettings.lightBleedingReduction = asset.LightBleedingReduction;
            if (asset.shadowCascadeOption == ShadowCascadesOption.NoCascades)
            {
                m_shadowSettings.mainLightShadowCascadesCount = 1;
            }
            else if (asset.shadowCascadeOption == ShadowCascadesOption.TwoCascades)
            {
                m_shadowSettings.mainLightShadowCascadesCount = 2;
            }
            else
                m_shadowSettings.mainLightShadowCascadesCount = 4;

            m_ShadowMananger.Setup(m_shadowSettings);
        }

        void UpdateLightVariablesGlobalCB(CommandBuffer cmd)
        {
            m_LightVariablesGlobalCB._MainLightPosition = new Vector4(0, 1, 0, 0);
            if (m_sunLight != null)
            {
                Vector4 dir = -m_sunLight.transform.localToWorldMatrix.GetColumn(2);
                m_LightVariablesGlobalCB._MainLightPosition = new Vector4(dir.x, dir.y, dir.z, 0.0f);
                m_LightVariablesGlobalCB._MainLightColor = m_sunLight.color;
                m_LightVariablesGlobalCB._MainLightIntensity = m_sunLight.intensity;
            }

            ConstantBuffer.PushGlobal(cmd, m_LightVariablesGlobalCB, ShaderIDs._LightVariablesGlobal);
        }

        class PushGlobalCameraParamPassData
        {
            public CameraData cameraData;
            public ShaderVariablesGlobal globalCB;
        }

        void PushGlobalCameraParams(RenderGraph renderGraph, CameraData cameraData)
        {
            using (var builder = renderGraph.AddRenderPass<PushGlobalCameraParamPassData>("Push Global Camera Parameters", out var passData))
            {
                passData.cameraData = cameraData;
                passData.globalCB = m_ShaderVariablesGlobalCB;

                builder.SetRenderFunc(
                    (PushGlobalCameraParamPassData data, RenderGraphContext context) =>
                    {
                        data.cameraData.UpdateShaderVariablesGlobalCB(ref data.globalCB);
                        ConstantBuffer.PushGlobal(context.cmd, data.globalCB, ShaderIDs._ShaderVariablesGlobal);
                    });
            }
        }

        void ExecuteShadowInitPass(RenderGraph graph)
        {
            m_ShadowMananger.ExecuteShadowInitPass(graph);
        }

        ShadowPassData RenderShadow(CameraData cameraData, RenderGraph graph, CullingResults cull, ComputeShader scanCS)
        {
            ShadowPassData shadowPassData = m_ShadowMananger.RenderShadowMap(graph, cull, m_ShaderVariablesGlobalCB);
            if (shadowPassData.m_ShadowType == ShadowType.PCSS)
            {
                if (m_shadowSettings.pcssSatEnable)
                {
                    SATPassData satData = m_ShadowMananger.GenerateShadowmapSAT(graph, shadowPassData, scanCS);
                    if (satData != null)
                    {
                        shadowPassData.m_ShadowmapSAT = satData.GetFinalOutputTexture();
                    }
                }
            }
            PushGlobalCameraParams(graph, cameraData);
            return shadowPassData;
        }

        ScreenSpaceShadowPassData RenderScreenSpaceShadow(CameraData cameraData, RenderGraph graph, ShadowPassData shadowPassData, DepthPrepassData depthData)
        {
            return m_ShadowMananger.Render_ScreenSpaceShadow(graph, cameraData.camera, shadowPassData.m_Shadowmap, depthData.m_Depth);
        }

        void ClearScreenSpaceShadowPass()
        {
            m_ShadowMananger.Clear();
        }
    }
}

