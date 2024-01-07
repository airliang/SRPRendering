using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public partial class InsanityPipeline
    {
        public static LightVariablesGlobal m_LightVariablesGlobalCB = new LightVariablesGlobal();
        Light m_sunLight;
        //ShadowManager m_ShadowMananger;
        int m_mainLightIndex = -1;
        //ShadowSettings m_shadowSettings;

        public static int GetMainLightIndex(NativeArray<VisibleLight> visibleLights)
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
            UpdateLightVariablesGlobalCB(cmd, m_sunLight);

            bool mainLightCastShadows = false;
            if (asset.shadowDistance > 0)
            {
                mainLightCastShadows = (m_mainLightIndex != -1 && m_sunLight != null &&
                                        m_sunLight.shadows != LightShadows.None);
            }

            //Prepare shadow datas

            InitShadowSettings(mainLightCastShadows, ref cullResults, cameraData, m_mainLightIndex);

            
        }

        void InitShadowSettings(bool mainLightCastShadows, ref CullingResults cullResults, CameraData cameraData, int lightIndex)
        {
            ShadowManager.Instance.Setup(mainLightCastShadows, ref cullResults, cameraData, lightIndex);
        }

        public static void UpdateLightVariablesGlobalCB(CommandBuffer cmd, Light mainLight)
        {
            m_LightVariablesGlobalCB._MainLightPosition = new Vector4(0, 1, 0, 0);
            if (mainLight != null)
            {
                Vector4 dir = -mainLight.transform.localToWorldMatrix.GetColumn(2);
                m_LightVariablesGlobalCB._MainLightPosition = new Vector4(dir.x, dir.y, dir.z, 0.0f);
                m_LightVariablesGlobalCB._MainLightColor = mainLight.color;
                m_LightVariablesGlobalCB._MainLightIntensity = mainLight.intensity;
            }

            ConstantBuffer.PushGlobal(cmd, m_LightVariablesGlobalCB, ShaderIDs._LightVariablesGlobal);
        }

        class PushGlobalCameraParamPassData
        {
            public CameraData cameraData;
            public ShaderVariablesGlobal globalCB;
        }

        static void PushGlobalCameraParams(RenderGraph renderGraph, CameraData cameraData)
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
            ShadowManager.Instance.ExecuteShadowInitPass(graph);
        }

        public static ShadowPassData RenderShadow(CameraData cameraData, RenderGraph graph, CullingResults cull, ComputeShader scanCS)
        {
            ShadowPassData shadowPassData = ShadowManager.Instance.RenderShadowMap(graph, cull, m_ShaderVariablesGlobalCB);
            if (shadowPassData != null && shadowPassData.m_ShadowType == ShadowType.VSM)
            {
                if (ShadowManager.Instance.shadowSettings.vsmSatEnable)
                {
                    SATPassData satData = ShadowManager.Instance.GenerateVSMSAT(graph, shadowPassData, scanCS);
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
            return ShadowManager.Instance.Render_ScreenSpaceShadow(graph, cameraData.camera, shadowPassData.m_Shadowmap, depthData.m_Depth);
        }

        void ClearScreenSpaceShadowPass()
        {
            ShadowManager.Instance.Clear();
        }
    }
}

