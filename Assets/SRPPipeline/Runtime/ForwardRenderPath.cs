using Insanity;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class ForwardRenderPath : RenderPath
    {
        private Material m_finalBlitMaterial;
        private ComputeShader m_parallelScan;
        Light m_sunLight;
        //ShadowManager m_ShadowMananger;
        int m_mainLightIndex = -1;

        public ForwardRenderPath(ForwardRendererData data, InsanityPipeline pipeline)
        {
            this.currentPipeline = pipeline;
            m_finalBlitMaterial = CoreUtils.CreateEngineMaterial(data.ForwardPathResources.shaders.Blit);
        }
        public override void RenderFrame(ScriptableRenderContext context, RenderingData renderingData, ref CullingResults cull)
        {
            InsanityPipelineAsset asset = InsanityPipeline.asset;

            

            CommandBuffer cmdRG = CommandBufferPool.Get("ExecuteRenderGraph");

            InsanityPipeline.UpdateGlobalConstantBuffers(renderingData.cameraData, cmdRG, ref InsanityPipeline.m_ShaderVariablesGlobalCB);
            if (cull.visibleLights.Length > 0)
            {
                ProcessVisibleLights(ref cull);
                PrepareGPULightData(cmdRG, ref cull, renderingData.cameraData);
            }
            //m_RenderingData.sunLight = m_sunLight;

            RenderGraphParameters rgParams = new RenderGraphParameters()
            {
                executionName = "Forward_RenderGraph_Execute",
                commandBuffer = cmdRG,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount
            };

            RenderingEventManager.BeforeExecuteRenderGraph(renderingData.renderGraph, renderingData.cameraData.camera);
            using (renderingData.renderGraph.RecordAndExecute(rgParams))
            {
                DepthPrepassData depthPassData = RenderPasses.Render_DepthPrePass(renderingData);

                ShadowManager.Instance.ExecuteShadowInitPass(renderingData.renderGraph);
                ShadowPassData shadowPassData = null;
                TextureHandle shadowmap = TextureHandle.nullHandle;
                if (ShadowManager.Instance.shadowSettings.supportsMainLightShadows)
                {
                    shadowPassData = InsanityPipeline.RenderShadow(renderingData.cameraData, renderingData.renderGraph, renderingData.cullingResults,
                        /*asset.InsanityPipelineResources.shaders.ParallelScan*/m_parallelScan);

                    if (shadowPassData != null)
                    {

                        shadowmap = shadowPassData.m_Shadowmap;
                        if (ShadowManager.Instance.NeedPrefilterShadowmap(shadowPassData))
                        {
                            PrefilterShadowPassData prefilterPassData = ShadowManager.Instance.PrefilterShadowPass(
                                renderingData.renderGraph, shadowPassData);
                            //shadowPassData.m_Shadowmap = prefilterPassData.m_BlurShadowmap;
                            if (prefilterPassData.m_filterRadius != eGaussianRadius.eGausian3x3)
                                shadowmap = prefilterPassData.m_Shadowmap;
                            else
                                shadowmap = prefilterPassData.m_BlurShadowmap;
                        }
                    }

                    //if (shadowPassData != null)
                    //    RenderScreenSpaceShadow(cameraData, m_RenderGraph, shadowPassData, depthPassData);
                }

                ForwardPassData forwardPassData = RenderPasses.Render_OpaqueFowardPass(renderingData, depthPassData.m_Albedo, depthPassData.m_Depth, shadowmap);
                Atmosphere atmosphere = Atmosphere.Instance;
                if (asset.PhysicalBasedSky)
                {
                    //BakeAtmosphereSH(ref context, asset.AtmosphereResources);
                    Atmosphere.Instance.BakeSkyToSHAmbient(ref context, asset.AtmosphereResources, m_sunLight);
                    atmosphere.Update();
                    RenderPasses.Render_PhysicalBaseSky(renderingData, depthPassData.m_Albedo, depthPassData.m_Depth, asset);
                }
                else
                {
                    Cubemap cubemap = asset.InsanityPipelineResources.materials.Skybox.GetTexture("_Cubemap") as Cubemap;
                    atmosphere.BakeCubemapToSHAmbient(ref context, asset.AtmosphereResources, cubemap);
                    atmosphere.Update();
                    RenderPasses.Render_SkyPass(renderingData, depthPassData.m_Albedo, depthPassData.m_Depth, asset.InsanityPipelineResources.materials.Skybox);
                }
                //SATPassData satData = m_satRenderer.TestParallelScan(m_RenderGraph, asset.InsanityPipelineResources.shaders.ParallelScan);
                //if (satData != null)
                //{
                //    forwardPassData.m_Albedo = satData.GetFinalOutputTexture();
                //}
                //if (asset.PCSSSATEnable && shadowPassData != null)
                //{
                //    forwardPassData.m_Albedo = shadowPassData.m_ShadowmapSAT;
                //}
                RenderPasses.FinalBlitPass(renderingData, forwardPassData.m_Albedo, m_finalBlitMaterial);


            }

            context.ExecuteCommandBuffer(cmdRG);
            CommandBufferPool.Release(cmdRG);

            //Submit camera rendering
            context.Submit();
        }

        public override void Dispose()
        {
            CoreUtils.Destroy(m_finalBlitMaterial); 
            m_finalBlitMaterial = null;
        }

        void ProcessVisibleLights(ref CullingResults cullResults)
        {

            m_mainLightIndex = InsanityPipeline.GetMainLightIndex(cullResults.visibleLights);
            if (m_mainLightIndex >= 0)
                m_sunLight = cullResults.visibleLights[m_mainLightIndex].light;
            //update light to shader constants

        }

        void PrepareGPULightData(CommandBuffer cmd, ref CullingResults cullResults, CameraData cameraData)
        {
            InsanityPipeline.UpdateLightVariablesGlobalCB(cmd, m_sunLight);

            bool mainLightCastShadows = false;
            if (InsanityPipeline.asset.shadowDistance > 0)
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
    }
}

