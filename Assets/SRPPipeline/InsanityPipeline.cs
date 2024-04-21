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
            public static readonly int _BRDFLUTTex = Shader.PropertyToID("_BRDFLUTTex");
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

        public InsanityPipeline()
        {
            
        }

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
            RenderPasses.Initialize();
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

            RTHandleUtils.Cleanup();
            RenderPasses.Cleanup();
        }

        private void InitializeRenderPipeline()
        {

        }
    }
}


