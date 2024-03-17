using Insanity;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class InsanityRenderer
    {
        private Material m_finalBlitMaterial;
        private Material m_debugViewBlitMaterial = null;
        private ComputeShader m_parallelScan;
        Light m_sunLight;
        //ShadowManager m_ShadowMananger;
        int m_mainLightIndex = -1;
        RendererData m_RendererData;
        public InsanityPipeline currentPipeline = null;

        public InsanityRenderer(RendererData data, InsanityPipeline pipeline)
        {
            this.currentPipeline = pipeline;
            m_RendererData = data;
            m_finalBlitMaterial = CoreUtils.CreateEngineMaterial(data.DataResources.shaders.Blit);
            m_debugViewBlitMaterial = CoreUtils.CreateEngineMaterial("Insanity/DebugViewBlit");
        }
        public void RenderFrame(ScriptableRenderContext context, RenderingData renderingData, ref CullingResults cull)
        {
            

            CommandBuffer cmdRG = CommandBufferPool.Get("ExecuteRenderGraph");

            InsanityPipeline.UpdateGlobalConstantBuffers(renderingData.cameraData, cmdRG, ref InsanityPipeline.m_ShaderVariablesGlobalCB);
            if (cull.visibleLights.Length > 0)
            {
                ProcessVisibleLights(ref cull);
                PrepareGPULightData(ref cull, renderingData);
            }

            RenderGraphParameters rgParams = new RenderGraphParameters()
            {
                executionName = "Forward_RenderGraph_Execute",
                commandBuffer = cmdRG,
                scriptableRenderContext = context,
                currentFrameIndex = Time.frameCount
            };

            InsanityPipelineAsset asset = InsanityPipeline.asset;
            RenderingEventManager.BeforeExecuteRenderGraph(renderingData.renderGraph, renderingData.cameraData.camera);
            using (renderingData.renderGraph.RecordAndExecute(rgParams))
            {
                InsanityPipeline.UpdateLightVariablesGlobalCB(renderingData.renderGraph, m_sunLight, LightCulling.Instance);
                DepthPrepassData depthPassData = RenderPasses.Render_DepthPrePass(renderingData);
                LightCulling.TileBasedLightCullingData lightCullingData = null;
                if (renderingData.supportAdditionalLights && LightCulling.Instance.ValidAdditionalLightsCount > 0)
                {
                    //LightCulling.Instance.ExecuteTileFrustumCompute(renderingData, m_forwardRenderData.ForwardPathResources.shaders.TileFrustumCompute);
                    lightCullingData = LightCulling.Instance.ExecuteTilebasedLightCulling(renderingData, depthPassData.m_Depth,
                        m_RendererData.DataResources.shaders.TileBasedLightCulling);
                }

                if (DebugView.NeedDebugView())
                {
                    DebugView.DebugViewForwardPass(renderingData, depthPassData.m_Albedo, depthPassData.m_Depth);
                    //DebugView.DebugViewTextures textures = new DebugView.DebugViewTextures();
                    //textures.m_Depth = depthPassData.m_Depth;
                    //textures.m_TileVisibleLightCount = lightCullingData.tileVisibleLightCounts;
                    //DebugView.ShowDebugPass(renderingData, ref textures, m_debugViewBlitMaterial);
                }
                //else
                {
                    ShadowManager.Instance.ExecuteShadowInitPass(renderingData.renderGraph);
                    ShadowPassData shadowPassData = null;
                    TextureHandle shadowmap = TextureHandle.nullHandle;
                    if (ShadowManager.Instance.shadowSettings.supportsMainLightShadows)
                    {
                        shadowPassData = InsanityPipeline.RenderShadow(renderingData.cameraData, renderingData.renderGraph, renderingData.cullingResults, m_parallelScan);

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

                    RenderPasses.FinalBlitPass(renderingData, forwardPassData.m_Albedo, m_finalBlitMaterial);
                    if (DebugView.NeedDebugView())
                    {
                        DebugView.DebugViewGPUResources textures = new DebugView.DebugViewGPUResources();
                        textures.m_Depth = depthPassData.m_Depth;
                        textures.m_LightVisibilityIndexBuffer = lightCullingData != null ? lightCullingData.lightVisibilityIndexBuffer : LightCulling.Instance.LightsVisibilityIndexBuffer;
                        DebugView.ShowDebugPass(renderingData, ref textures, m_debugViewBlitMaterial);
                    }
                }
            }

            context.ExecuteCommandBuffer(cmdRG);
            CommandBufferPool.Release(cmdRG);

            //Submit camera rendering
            context.Submit();
        }

        private void RenderGraphForwardPath(ScriptableRenderContext context, CommandBuffer cmdRG, RenderingData renderingData)
        {
            InsanityPipelineAsset asset = InsanityPipeline.asset;
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
                InsanityPipeline.UpdateLightVariablesGlobalCB(renderingData.renderGraph, m_sunLight, LightCulling.Instance);
                DepthPrepassData depthPassData = RenderPasses.Render_DepthPrePass(renderingData);
                LightCulling.TileBasedLightCullingData lightCullingData = null;
                if (renderingData.supportAdditionalLights && LightCulling.Instance.ValidAdditionalLightsCount > 0)
                {
                    //LightCulling.Instance.ExecuteTileFrustumCompute(renderingData, m_forwardRenderData.ForwardPathResources.shaders.TileFrustumCompute);
                    lightCullingData = LightCulling.Instance.ExecuteTilebasedLightCulling(renderingData, depthPassData.m_Depth,
                        m_RendererData.DataResources.shaders.TileBasedLightCulling);
                }

                if (DebugView.NeedDebugView())
                {
                    DebugView.DebugViewForwardPass(renderingData, depthPassData.m_Albedo, depthPassData.m_Depth);
                    //DebugView.DebugViewTextures textures = new DebugView.DebugViewTextures();
                    //textures.m_Depth = depthPassData.m_Depth;
                    //textures.m_TileVisibleLightCount = lightCullingData.tileVisibleLightCounts;
                    //DebugView.ShowDebugPass(renderingData, ref textures, m_debugViewBlitMaterial);
                }
                //else
                {
                    ShadowManager.Instance.ExecuteShadowInitPass(renderingData.renderGraph);
                    ShadowPassData shadowPassData = null;
                    TextureHandle shadowmap = TextureHandle.nullHandle;
                    if (ShadowManager.Instance.shadowSettings.supportsMainLightShadows)
                    {
                        shadowPassData = InsanityPipeline.RenderShadow(renderingData.cameraData, renderingData.renderGraph, renderingData.cullingResults, m_parallelScan);

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

                    RenderPasses.FinalBlitPass(renderingData, forwardPassData.m_Albedo, m_finalBlitMaterial);
                    if (DebugView.NeedDebugView())
                    {
                        DebugView.DebugViewGPUResources textures = new DebugView.DebugViewGPUResources();
                        textures.m_Depth = depthPassData.m_Depth;
                        textures.m_LightVisibilityIndexBuffer = lightCullingData != null ? lightCullingData.lightVisibilityIndexBuffer : LightCulling.Instance.LightsVisibilityIndexBuffer;
                        DebugView.ShowDebugPass(renderingData, ref textures, m_debugViewBlitMaterial);
                    }
                }
            }

            context.ExecuteCommandBuffer(cmdRG);
            CommandBufferPool.Release(cmdRG);

            //Submit camera rendering
            context.Submit();
        }

        public void Dispose()
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
            InsanityPipeline.m_LightVariablesGlobalCB._AdditionalLightsCount = cullResults.visibleLights.Length - 1;
        }

        void PrepareGPULightData(ref CullingResults cullResults, RenderingData renderingData)
        {
            if (renderingData.supportAdditionalLights) 
            {
                LightCulling.Instance.SetupAdditionalLights(cullResults.visibleLights, renderingData.cameraData);
                LightCulling.Instance.SetupTiles((int)GlobalRenderSettings.screenResolution.width, (int)GlobalRenderSettings.screenResolution.height,
                    m_RendererData.TileSize);
            }
            

            bool mainLightCastShadows = false;
            if (InsanityPipeline.asset.shadowDistance > 0)
            {
                mainLightCastShadows = (m_mainLightIndex != -1 && m_sunLight != null &&
                                        m_sunLight.shadows != LightShadows.None);
            }

            //Prepare shadow datas

            InitShadowSettings(mainLightCastShadows, ref cullResults, renderingData.cameraData, m_mainLightIndex);


        }

        void InitShadowSettings(bool mainLightCastShadows, ref CullingResults cullResults, CameraData cameraData, int lightIndex)
        {
            ShadowManager.Instance.Setup(mainLightCastShadows, ref cullResults, cameraData, lightIndex);
        }
    }
}

