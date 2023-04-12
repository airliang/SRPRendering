using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static Insanity.InsanityPipeline;
using Unity.Collections;
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
        ShaderVariablesGlobal m_ShaderVariablesGlobalCB = new ShaderVariablesGlobal();
        SATRenderer m_satRenderer = new SATRenderer();

        void CleanupRenderGraph()
        {
            m_RenderGraph.Cleanup();
            m_RenderGraph = null;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            BeginFrameRendering(context, cameras);

            foreach (Camera camera in cameras)
            {
                BeginCameraRendering(context, camera);

                //Culling
                ScriptableCullingParameters cullingParams;
                
                if (!camera.TryGetCullingParameters(out cullingParams)) 
                    continue;
                cullingParams.shadowDistance = Mathf.Min(asset.shadowDistance, camera.farClipPlane);
                //cullingParams.shadowNearPlaneOffset = QualitySettings.shadowNearPlaneOffset;
                CullingResults cull = context.Cull(ref cullingParams);

                //Camera setup some builtin variables e.g. camera projection matrices etc
                context.SetupCameraProperties(camera);

                CameraData cameraData = CameraData.GetOrCreate(camera);
                cameraData.Update();
                //Execute graph 
                CommandBuffer cmdRG = CommandBufferPool.Get("ExecuteRenderGraph");

                UpdateGlobalConstantBuffers(cameraData, cmdRG);
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

                using (m_RenderGraph.RecordAndExecute(rgParams))
                {
                    DepthPrepassData depthPassData = Render_DepthPrePass(cameraData, m_RenderGraph, cull);

                    //RenderShadowMaps(m_RenderGraph, cull, in m_ShaderVariablesGlobalCB, m_sunLight, m_mainLightIndex, depthPassData);
                    ExecuteShadowInitPass(m_RenderGraph);
                    ShadowPassData shadowPassData = null;
                    TextureHandle shadowmap = TextureHandle.nullHandle;
                    if (m_shadowSettings.supportsMainLightShadows)
                    {
                        shadowPassData = RenderShadow(cameraData, m_RenderGraph, cull);

                        if (shadowPassData != null)
                        {
                            shadowmap = shadowPassData.m_Shadowmap;
                            if (m_ShadowMananger.NeedPrefilterShadowmap(shadowPassData))
                            {
                                PrefilterShadowPassData prefilterPassData = m_ShadowMananger.PrefilterShadowPass(
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

                    ForwardPassData forwardPassData = Render_OpaqueFowardPass(cameraData, m_RenderGraph, cull, depthPassData, shadowmap);
                    Render_SkyPass(cameraData, m_RenderGraph, depthPassData, asset.InsanityPipelineResources.materials.Skybox);
                    SATPassData satData = m_satRenderer.TestParallelScan(m_RenderGraph, asset.InsanityPipelineResources.shaders.ParallelScan);
                    if (satData != null)
                    {
                        //forwardPassData.m_Albedo = satData.m_OutputTexture;
                    }
                    FinalBlitPass(cameraData, m_RenderGraph, forwardPassData);
                }

                context.ExecuteCommandBuffer(cmdRG);
                CommandBufferPool.Release(cmdRG);

                //Submit camera rendering
                context.Submit();
                EndCameraRendering(context, camera);
            }
            
            EndFrameRendering(context, cameras);
        }

        void UpdateGlobalConstantBuffers(CameraData cameraData, CommandBuffer cmd)
        {
            UpdateShaderVariablesGlobalCB(cameraData, cmd);
        }

        void UpdateShaderVariablesGlobalCB(CameraData cameraData, CommandBuffer cmd)
        {
            cameraData.UpdateShaderVariablesGlobalCB(ref m_ShaderVariablesGlobalCB);

            ConstantBuffer.PushGlobal(cmd, m_ShaderVariablesGlobalCB, ShaderIDs._ShaderVariablesGlobal);
        }

        

        protected override void Dispose(bool disposing)
        {
            ClearFinalBlitPass();
            //ClearSkyPass();
            Graphics.ClearRandomWriteTargets();
            Graphics.SetRenderTarget(null);

            CleanupRenderGraph();
            ConstantBuffer.ReleaseAll();

            CameraData.ClearAll();
        }
    }
}


