using Insanity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEditor.VersionControl;

namespace Insanity
{
    public class InsanityRenderer
    {
        private Material m_finalBlitMaterial;
        private Material m_debugViewBlitMaterial = null;
        private Material m_copyDepthMaterial = null;
        private ComputeShader m_parallelScan;
        private ComputeShader m_tilebasedLightCulling;
        private ComputeShader m_DeferredLighting;
        private ComputeShader m_TileBasedDeferredLighting;
        Light m_sunLight;
        //ShadowManager m_ShadowMananger;
        int m_mainLightIndex = -1;
        RendererData m_RendererData;
        public InsanityPipeline currentPipeline = null;
        public SSAOSettings m_ssaoSettings = new SSAOSettings();

        internal class FrameRenderSets
        {
            // backbuffer
            internal TextureHandle backBufferColor;
            //internal TextureHandle backBufferDepth;

            // forward pass camera targets
            internal RTHandle m_CameraColorHandle;
            internal RTHandle m_CameraDepthHandle;
            internal TextureHandle cameraColor;
            internal TextureHandle cameraDepth;
            internal TextureHandle cameraDepthResolved;
            internal TextureHandle cameraNormal;

            internal TextureHandle mainShadowsTexture;
            internal TextureHandle additionalShadowsTexture;
            internal TextureHandle screenSpaceShadowTexture;

            internal TextureHandle ssaoMask;

            // gbuffer targets
            internal TextureHandle[] gbuffer;

            public void Release()
            {
                m_CameraColorHandle?.Release();
                m_CameraDepthHandle?.Release();
            }
        }

        FrameRenderSets m_FrameRenderSets = new FrameRenderSets();

        public InsanityRenderer(RendererData data, InsanityPipeline pipeline)
        {
            this.currentPipeline = pipeline;
            m_RendererData = data;

            m_finalBlitMaterial = CoreUtils.CreateEngineMaterial(InsanityPipeline.asset.InsanityPipelineResources.shaders.Blit);
            m_debugViewBlitMaterial = CoreUtils.CreateEngineMaterial(InsanityPipeline.asset.InsanityPipelineResources.shaders.DebugViewBlit);
            m_copyDepthMaterial = CoreUtils.CreateEngineMaterial(InsanityPipeline.asset.InsanityPipelineResources.shaders.CopyDepth);
            m_tilebasedLightCulling = InsanityPipeline.asset.InsanityPipelineResources.shaders.TileBasedLightCulling;
            m_DeferredLighting = InsanityPipeline.asset.InsanityPipelineResources.shaders.DeferredLighting;
            m_TileBasedDeferredLighting = InsanityPipeline.asset.InsanityPipelineResources.shaders.TileBasedDeferredLighting;
            m_ssaoSettings.ssao = InsanityPipeline.asset.InsanityPipelineResources.shaders.HBAO;
            m_ssaoSettings.blur = InsanityPipeline.asset.InsanityPipelineResources.shaders.SSAOBlur;

            m_ssaoSettings.duarBlur = InsanityPipeline.asset.InsanityPipelineResources.shaders.SSAODualBlur;
            m_ssaoSettings.temporalFilter = InsanityPipeline.asset.InsanityPipelineResources.shaders.SSAOTemporalFilter;
            m_ssaoSettings.upSample = InsanityPipeline.asset.InsanityPipelineResources.shaders.SSAOUpSample;
            //m_ssaoSettings.blueNoiseTexture = InsanityPipeline.asset.InsanityPipelineResources.internalTextures.SSAONoiseTexture;
            RenderPasses.InitializeSSAOShaderParameters();
            if(m_RendererData.RenderingPath == RendererData.eRenderingPath.Deferred)
            {
                RenderPasses.InitializeDeferredShadingParameters();
            }
        }

        void CreateFrameRenderSets(RenderGraph renderGraph, ScriptableRenderContext context, RenderingData renderingData)
        {
            RenderTargetIdentifier rtBackbuffer = renderingData.cameraData.camera.targetTexture != null ?
                new RenderTargetIdentifier(renderingData.cameraData.camera.targetTexture) : BuiltinRenderTextureType.CameraTarget;

            m_FrameRenderSets.backBufferColor = renderGraph.ImportBackbuffer(rtBackbuffer);

            RenderTextureDescriptor colorDescriptor = renderingData.cameraData.GetCameraTargetDescriptor(InsanityPipeline.asset.ResolutionRate, InsanityPipeline.asset.HDREnable, (int)InsanityPipeline.asset.MSAASamples);
            colorDescriptor.useMipMap = false;
            colorDescriptor.autoGenerateMips = false;
            colorDescriptor.depthBufferBits = 0;
            RTHandleUtils.ReAllocateIfNeeded(ref m_FrameRenderSets.m_CameraColorHandle,
                colorDescriptor,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Color");

            RenderTextureDescriptor depthDescriptor = colorDescriptor;
            depthDescriptor.bindMS = colorDescriptor.msaaSamples > 1;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
            depthDescriptor.depthBufferBits = (int)DepthBits.Depth32;
            RTHandleUtils.ReAllocateIfNeeded(ref m_FrameRenderSets.m_CameraDepthHandle, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "Depth");

            if (m_RendererData.RenderingPath == RendererData.eRenderingPath.Forward)
            {
                RTHandleUtils.ReAllocateIfNeeded(ref m_FrameRenderSets.m_CameraColorHandle,
                colorDescriptor,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "Color");
                m_FrameRenderSets.cameraColor = renderGraph.ImportTexture(m_FrameRenderSets.m_CameraColorHandle);
            }
            else if (m_RendererData.RenderingPath == RendererData.eRenderingPath.Deferred)
            {
                TextureDesc cameraColorDesc = new TextureDesc(colorDescriptor.width, colorDescriptor.height);
                cameraColorDesc.colorFormat = colorDescriptor.graphicsFormat;//GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.R8, false);
                cameraColorDesc.depthBufferBits = 0;
                cameraColorDesc.msaaSamples = MSAASamples.None;
                cameraColorDesc.enableRandomWrite = true;
                cameraColorDesc.name = "Color";
                cameraColorDesc.filterMode = FilterMode.Point;
                m_FrameRenderSets.cameraColor = renderGraph.CreateTexture(cameraColorDesc);
            }
            m_FrameRenderSets.cameraDepth = renderGraph.ImportTexture(m_FrameRenderSets.m_CameraDepthHandle);
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

            RenderingEventManager.BeforeExecuteRenderGraph(renderingData.renderGraph, renderingData.cameraData.camera);
            using (renderingData.renderGraph.RecordAndExecute(rgParams))
            {
                if (m_RendererData.RenderingPath == RendererData.eRenderingPath.Forward)
                {
                    RenderGraphForwardPath(context, cmdRG, renderingData);
                }
                else
                {
                    RenderGraphDeferredPath(context, cmdRG, renderingData);
                }
            }

            context.ExecuteCommandBuffer(cmdRG);
            CommandBufferPool.Release(cmdRG);

            //Submit camera rendering
            context.Submit();
        }

        private void RenderSSAO(CommandBuffer cmdRG, RenderingData renderingData)
        {
            InsanityPipelineAsset asset = InsanityPipeline.asset;
            if (asset.SSAOEnable)
            {
                m_ssaoSettings.halfResolution = true;
                m_ssaoSettings.radius = asset.SSAORadius;
                m_ssaoSettings.maxRadiusInPixel = asset.MaxRadiusInPixel;
                m_ssaoSettings.horizonBias = asset.HBAOHorizonBias;
                m_ssaoSettings.halfResolution = asset.AOHalfResolution;
                m_ssaoSettings.intensity = asset.AOIntensity;
                m_ssaoSettings.aoFadeStart = asset.AOFadeStart;
                m_ssaoSettings.aoFadeEnd = asset.AOFadeEnd;
                m_ssaoSettings.selfOcclusionBiasViewSpace = asset.AOSelfOcclusionBias;
                m_ssaoSettings.blurMethod = asset.SSAOBlurMethod;
                m_ssaoSettings.enableTemporalFilter = asset.EnableTemperalFilter;
                m_ssaoSettings.bilateralAggressiveness = asset.BilateralAggressiveness;
                m_ssaoSettings.needUpSample = asset.SSAOUpSample;
                if (m_ssaoSettings.blueNoiseTexture == null)
                {
                    RenderPasses.CreateNoiseTexture(out m_ssaoSettings.blueNoiseTexture);
                }
                RenderPasses.Render_HBAOPass(renderingData, m_FrameRenderSets.cameraDepthResolved, m_FrameRenderSets.cameraNormal, out m_FrameRenderSets.ssaoMask, m_ssaoSettings);
            }
            CoreUtils.SetKeyword(cmdRG, "_SSAO_ENABLE", asset.SSAOEnable);
        }

        private void RenderGraphForwardPath(ScriptableRenderContext context, CommandBuffer cmdRG, RenderingData renderingData)
        {
            InsanityPipelineAsset asset = InsanityPipeline.asset;
            CreateFrameRenderSets(renderingData.renderGraph, context, renderingData);
            RenderPasses.ClearTargetPass(renderingData.renderGraph, RTClearFlags.ColorDepth, Color.black, m_FrameRenderSets);
            if (asset.PhysicalBasedSky)
            {
                //BakeAtmosphereSH(ref context, asset.AtmosphereResources);
                Atmosphere.Instance.BakeSkyToSHAmbient(renderingData.renderGraph, ref context, asset.AtmosphereResources, m_sunLight);
            }

            InsanityPipeline.UpdateLightVariablesGlobalCB(renderingData.renderGraph, m_sunLight, LightCulling.Instance);
            if (IsNormalPassEnable())
            {
                RenderPasses.Render_DepthNormalPass(renderingData, out m_FrameRenderSets.cameraNormal, m_FrameRenderSets.cameraDepth);
            }
            else
                RenderPasses.Render_DepthPrePass(renderingData, m_FrameRenderSets.cameraDepth);
            RenderPasses.CopyDepthPass(renderingData, out m_FrameRenderSets.cameraDepthResolved, m_FrameRenderSets.cameraDepth, m_copyDepthMaterial, (int)asset.MSAASamples);

            LightCulling.TileBasedLightCullingData lightCullingData = null;
            if (renderingData.supportAdditionalLights && LightCulling.Instance.ValidAdditionalLightsCount > 0)
            {
                //LightCulling.Instance.ExecuteTileFrustumCompute(renderingData, m_forwardRenderData.ForwardPathResources.shaders.TileFrustumCompute);
                lightCullingData = LightCulling.Instance.ExecuteTilebasedLightCulling(renderingData, m_FrameRenderSets.cameraDepthResolved,
                    m_tilebasedLightCulling);
            }

            if (DebugView.NeedDebugView())
            {
                DebugView.DebugViewPreparePass(renderingData);
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

                        if (ShadowManager.Instance.shadowSettings.requiresScreenSpaceShadowResolve)
                        {
                            ShadowManager.Instance.Render_ScreenSpaceShadow(renderingData.renderGraph, renderingData.cameraData.camera, shadowmap, m_FrameRenderSets.cameraDepthResolved);
                        }
                    }
                }

                RenderSSAO(cmdRG, renderingData);

                ForwardPassData forwardPassData = RenderPasses.Render_OpaqueFowardPass(renderingData, m_FrameRenderSets.cameraDepth, m_FrameRenderSets.cameraColor, shadowmap);
                Atmosphere atmosphere = Atmosphere.Instance;
                if (asset.PhysicalBasedSky)
                {
                    //Atmosphere.Instance.BakeSkyToSHAmbient(renderingData.renderGraph, ref context, asset.AtmosphereResources, m_sunLight);
                    atmosphere.Update();
                    RenderPasses.Render_PhysicalBaseSky(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.cameraDepth, asset);
                }
                else
                {
                    Cubemap cubemap = asset.InsanityPipelineResources.materials.Skybox.GetTexture("_Cubemap") as Cubemap;
                    atmosphere.BakeCubemapToSHAmbient(ref context, asset.AtmosphereResources, cubemap);
                    atmosphere.Update();
                    RenderPasses.Render_SkyPass(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.cameraDepth, asset.InsanityPipelineResources.materials.Skybox);
                }

                RenderPasses.FinalBlitPass(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.backBufferColor, m_finalBlitMaterial);
                if (DebugView.NeedDebugView())
                {
                    DebugView.DebugViewGPUResources textures = new DebugView.DebugViewGPUResources();
                    textures.m_Depth = m_FrameRenderSets.cameraDepthResolved;
                    textures.m_LightVisibilityIndexBuffer = lightCullingData != null ? lightCullingData.lightVisibilityIndexBuffer : LightCulling.Instance.LightsVisibilityIndexBuffer;
                    textures.m_TileVisibleLightCount = lightCullingData != null ? lightCullingData.tileVisibleLightCounts : TextureHandle.nullHandle;
                    textures.m_Normal = m_FrameRenderSets.cameraNormal;
                    textures.m_SSAO = m_FrameRenderSets.ssaoMask;
                    DebugView.ShowDebugPass(renderingData, ref textures, m_FrameRenderSets.backBufferColor, m_debugViewBlitMaterial, (int)DebugView.debugViewType);
                }
                else
                {

                }
            }
        }

        private void RenderGraphDeferredPath(ScriptableRenderContext context, CommandBuffer cmdRG, RenderingData renderingData)
        {
            InsanityPipelineAsset asset = InsanityPipeline.asset;
            CreateFrameRenderSets(renderingData.renderGraph, context, renderingData);
            RenderPasses.ClearTargetPass(renderingData.renderGraph, RTClearFlags.ColorDepth, Color.black, m_FrameRenderSets);
            if (asset.PhysicalBasedSky)
            {
                //BakeAtmosphereSH(ref context, asset.AtmosphereResources);
                Atmosphere.Instance.BakeSkyToSHAmbient(renderingData.renderGraph, ref context, asset.AtmosphereResources, m_sunLight);
            }

            RenderPasses.Render_DepthPrePass(renderingData, m_FrameRenderSets.cameraDepth);
            RenderPasses.CopyDepthPass(renderingData, out m_FrameRenderSets.cameraDepthResolved, m_FrameRenderSets.cameraDepth, m_copyDepthMaterial, (int)asset.MSAASamples);

            //LightCulling.TileBasedLightCullingData lightCullingData = null;
            //if (renderingData.supportAdditionalLights && LightCulling.Instance.ValidAdditionalLightsCount > 0)
            //{
            //    lightCullingData = LightCulling.Instance.ExecuteTilebasedLightCulling(renderingData, m_FrameRenderSets.cameraDepthResolved,
            //        m_tilebasedLightCulling);
            //}

            if (DebugView.NeedDebugView())
            {
                DebugView.DebugViewPreparePass(renderingData);
            }

            InsanityPipeline.UpdateLightVariablesGlobalCB(renderingData.renderGraph, m_sunLight, LightCulling.Instance);

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

                    if (ShadowManager.Instance.shadowSettings.requiresScreenSpaceShadowResolve)
                    {
                        ScreenSpaceShadowPassData ssShadowPassData = ShadowManager.Instance.Render_ScreenSpaceShadow(renderingData.renderGraph, renderingData.cameraData.camera, shadowmap, m_FrameRenderSets.cameraDepthResolved);
                        shadowmap = ssShadowPassData.m_SSShadowmap;
                    }
                }
            }

            RenderPasses.GBufferPass(renderingData, m_FrameRenderSets.cameraDepth, out m_FrameRenderSets.cameraNormal);

            RenderSSAO(cmdRG, renderingData);

            //RenderPasses.DeferredShadingPass(renderingData, RenderPasses.s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.AlbedoMetallic],
            //    RenderPasses.s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.NormalSmoothness], m_FrameRenderSets.cameraDepthResolved, shadowmap, m_FrameRenderSets.cameraColor, m_DeferredLighting);
            LightCulling.TileBasedDeferredShadingData tileBasedDeferredData = LightCulling.Instance.TileBasedDeferredShading(renderingData, RenderPasses.s_GBuffer.GBufferAttachments, m_FrameRenderSets.cameraDepthResolved, shadowmap, m_FrameRenderSets.cameraColor, m_TileBasedDeferredLighting);

            Atmosphere atmosphere = Atmosphere.Instance;
            if (asset.PhysicalBasedSky)
            {
                atmosphere.Update();
                RenderPasses.Render_PhysicalBaseSky(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.cameraDepth, asset);
            }
            else
            {
                Cubemap cubemap = asset.InsanityPipelineResources.materials.Skybox.GetTexture("_Cubemap") as Cubemap;
                atmosphere.BakeCubemapToSHAmbient(ref context, asset.AtmosphereResources, cubemap);
                atmosphere.Update();
                RenderPasses.Render_SkyPass(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.cameraDepth, asset.InsanityPipelineResources.materials.Skybox);
            }

            RenderPasses.FinalBlitPass(renderingData, m_FrameRenderSets.cameraColor, m_FrameRenderSets.backBufferColor, m_finalBlitMaterial);

            if (DebugView.NeedDebugView())
            {
                DebugView.DebugViewGPUResources textures = new DebugView.DebugViewGPUResources();
                textures.m_Depth = m_FrameRenderSets.cameraDepthResolved;
                //textures.m_LightVisibilityIndexBuffer = lightCullingData != null ? lightCullingData.lightVisibilityIndexBuffer : LightCulling.Instance.LightsVisibilityIndexBuffer;
                textures.m_TileVisibleLightCount = tileBasedDeferredData.tileVisibleLightCounts;
                textures.m_GBufferNormalSmoothness = RenderPasses.s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.NormalSmoothness];
                textures.m_SSAO = m_FrameRenderSets.ssaoMask;
                textures.m_GBufferAlbedoMetallic = RenderPasses.s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.AlbedoMetallic];
                DebugView.ShowDebugPass(renderingData, ref textures, m_FrameRenderSets.backBufferColor, m_debugViewBlitMaterial, (int)DebugView.debugViewType, RendererData.eRenderingPath.Deferred);
            }
        }

        public void Dispose()
        {
            m_FrameRenderSets.Release();
            CoreUtils.Destroy(m_finalBlitMaterial);
            m_finalBlitMaterial = null;
            CoreUtils.Destroy(m_debugViewBlitMaterial);
            m_debugViewBlitMaterial = null;
            CoreUtils.Destroy(m_copyDepthMaterial);
            m_copyDepthMaterial = null;
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
                    InsanityPipeline.asset.TileSize);
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

        bool IsNormalPassEnable()
        {
            return InsanityPipeline.asset.SSAOEnable || DebugView.debugViewType == DebugView.DebugViewType.WorldSpaceNormal;
        }
    }
}

