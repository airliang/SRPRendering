using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
//using UnityEngine.Experimental.GlobalIllumination;

namespace Insanity
{
    public partial class InsanityPipeline : RenderPipeline
    {
        internal static class ShaderIDs
        {
            public static readonly int _ShaderVariablesGlobal = Shader.PropertyToID("ShaderVariablesGlobal");
            public static readonly int _LightVariablesGlobal = Shader.PropertyToID("LightVariablesGlobal");
            public static readonly int _MainlightShadowVariablesGlobal = Shader.PropertyToID("MainlightShadowVariablesGlobal");
            public static readonly int _GPUAdditionalLights = Shader.PropertyToID("_GPUAdditionalLights");
            public static readonly int _DebugViewVariables = Shader.PropertyToID("DebugViewVariables");
        }

        public static InsanityPipelineAsset asset
        {
            get => GraphicsSettings.currentRenderPipeline as InsanityPipelineAsset;
        }

        public static float maxShadowBias
        {
            get => 10.0f;
        }


        RenderGraph m_RenderGraph = new RenderGraph("Insanity");
        public static ShaderVariablesGlobal m_ShaderVariablesGlobalCB = new ShaderVariablesGlobal();
        RenderingData m_RenderingData = new RenderingData();
        InsanityRenderer m_CurrentRenderer = null;
        
        //SATRenderer m_satRenderer = new SATRenderer();

        void CleanupRenderGraph()
        {
            m_RenderGraph.Cleanup();
            m_RenderGraph = null;
        }

        void UpdateGlobalRenderSettings(Camera camera)
        {
            GlobalRenderSettings.HDREnable = asset.HDREnable;
            GlobalRenderSettings.HDRExposure = asset.Exposure;
            GlobalRenderSettings.ResolutionRate = asset.ResolutionRate;
            GlobalRenderSettings.screenResolution = new Rect(camera.pixelRect.x, camera.pixelRect.y,
                camera.pixelRect.width * GlobalRenderSettings.ResolutionRate, camera.pixelRect.height * GlobalRenderSettings.ResolutionRate);
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            GraphicsSettings.useScriptableRenderPipelineBatching = asset.UseSRPBatcher;
            BeginFrameRendering(context, cameras);

            foreach (Camera camera in cameras)
            {
                UpdateGlobalRenderSettings(camera);
                BeginCameraRendering(context, camera);
                m_CurrentRenderer = asset.Renderer;
                //Culling
                ScriptableCullingParameters cullingParams;
                
                if (!camera.TryGetCullingParameters(out cullingParams)) 
                    continue;
                cullingParams.shadowDistance = Mathf.Min(asset.shadowDistance, camera.farClipPlane);
                //cullingParams.shadowNearPlaneOffset = QualitySettings.shadowNearPlaneOffset;
                CullingResults cull = context.Cull(ref cullingParams);
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                //Camera setup some builtin variables e.g. camera projection matrices etc
                context.SetupCameraProperties(camera);

                CameraData cameraData = CameraData.GetOrCreate(camera);
                cameraData.Update();

                m_RenderingData.cameraData = cameraData;
                m_RenderingData.renderGraph = m_RenderGraph;
                m_RenderingData.cullingResults = cull;
                m_RenderingData.supportAdditionalLights = asset.AdditionalLightEnable;
                DebugView.debugViewType = (DebugView.DebugViewType)asset.CurrentDebugMode;

                if (m_CurrentRenderer != null)
                {
                    m_CurrentRenderer.RenderFrame(context, m_RenderingData, ref cull);
                }
                else
                {
                    /*
                    //Execute graph 
                    CommandBuffer cmdRG = CommandBufferPool.Get("ExecuteRenderGraph");

                    UpdateGlobalConstantBuffers(cameraData, cmdRG, ref m_ShaderVariablesGlobalCB);
                    if (cull.visibleLights.Length > 0)
                    {
                        ProcessVisibleLights(ref cull);
                        PrepareGPULightData(cmdRG, ref cull, cameraData);
                    }

                    RenderGraphParameters rgParams = new RenderGraphParameters()
                    {
                        executionName = "Insanity_RenderGraph_Execute",
                        commandBuffer = cmdRG,
                        scriptableRenderContext = context,
                        currentFrameIndex = Time.frameCount
                    };

                    RenderingEventManager.BeforeExecuteRenderGraph(m_RenderGraph, camera);
                    using (m_RenderGraph.RecordAndExecute(rgParams))
                    {
                        DepthPrepassData depthPassData = RenderPasses.Render_DepthPrePass(m_RenderingData);

                        //RenderShadowMaps(m_RenderGraph, cull, in m_ShaderVariablesGlobalCB, m_sunLight, m_mainLightIndex, depthPassData);
                        ExecuteShadowInitPass(m_RenderGraph);
                        ShadowPassData shadowPassData = null;
                        TextureHandle shadowmap = TextureHandle.nullHandle;
                        if (ShadowManager.Instance.shadowSettings.supportsMainLightShadows)
                        {
                            shadowPassData = RenderShadow(cameraData, m_RenderGraph, cull, asset.InsanityPipelineResources.shaders.ParallelScan);

                            if (shadowPassData != null)
                            {
                                shadowmap = shadowPassData.m_Shadowmap;
                                if (ShadowManager.Instance.NeedPrefilterShadowmap(shadowPassData))
                                {
                                    PrefilterShadowPassData prefilterPassData = ShadowManager.Instance.PrefilterShadowPass(
                                        m_RenderGraph, shadowPassData);
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

                        ForwardPassData forwardPassData = RenderPasses.Render_OpaqueFowardPass(m_RenderingData, depthPassData.m_Albedo, depthPassData.m_Depth, shadowmap);
                        Atmosphere atmosphere = Atmosphere.Instance;
                        if (asset.PhysicalBasedSky)
                        {
                            RenderPasses.BakeAtmosphereSH(ref context, asset.AtmosphereResources, m_sunLight);
                            atmosphere.Update();
                            RenderPasses.Render_PhysicalBaseSky(m_RenderingData, depthPassData.m_Albedo, depthPassData.m_Depth, asset);
                        }
                        else
                        {
                            Cubemap cubemap = asset.InsanityPipelineResources.materials.Skybox.GetTexture("_Cubemap") as Cubemap;
                            atmosphere.BakeCubemapToSHAmbient(ref context, asset.AtmosphereResources, cubemap);
                            atmosphere.Update();
                            RenderPasses.Render_SkyPass(m_RenderingData, depthPassData.m_Albedo, depthPassData.m_Depth, asset.InsanityPipelineResources.materials.Skybox);
                        }

                        RenderPasses.FinalBlitPass(m_RenderingData, forwardPassData.m_Albedo, null);
                    }

                    context.ExecuteCommandBuffer(cmdRG);
                    CommandBufferPool.Release(cmdRG);

                    //Submit camera rendering
                    context.Submit();
                    */
                }
                
                EndCameraRendering(context, camera);
            }
            
            EndFrameRendering(context, cameras);
        }

        public static void UpdateGlobalConstantBuffers(CameraData cameraData, CommandBuffer cmd, ref ShaderVariablesGlobal shaderVariablesGlobalCB)
        {
            UpdateShaderVariablesGlobalCB(cameraData, cmd, ref shaderVariablesGlobalCB);
        }

        static void UpdateShaderVariablesGlobalCB(CameraData cameraData, CommandBuffer cmd, ref ShaderVariablesGlobal shaderVariablesGlobalCB)
        {
            cameraData.UpdateShaderVariablesGlobalCB(ref shaderVariablesGlobalCB);

            ConstantBuffer.PushGlobal(cmd, shaderVariablesGlobalCB, ShaderIDs._ShaderVariablesGlobal);
        }

        

        protected override void Dispose(bool disposing)
        {
            //ClearFinalBlitPass();
            //ClearSkyPass();
            Graphics.ClearRandomWriteTargets();
            Graphics.SetRenderTarget(null);
            LightCulling.Instance.Dispose();
            CleanupRenderGraph();
            ConstantBuffer.ReleaseAll();

            CameraData.ClearAll();

            //if (m_atmosphere != null)
            {
                Atmosphere.Instance.Release();
                //m_atmosphere = null;
            }

            ShadowManager.Instance.Clear();
        }

        private void InitializeRenderPipeline()
        {

        }
    }
}


