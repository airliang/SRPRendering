using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public partial class RenderPasses
    {
        static ShaderTagId m_DepthNormalPrePassId = new ShaderTagId("DepthNormalPrepass");
        static ProfilingSampler s_DepthNormalProfiler = new ProfilingSampler("DepthNormalPrepass Profiler");

        private static TextureHandle CreateNormalTexture(RenderGraph graph, int width, int height, int msaaSamples, string name)
        {
            TextureDesc colorRTDesc = new TextureDesc(width, height);
            colorRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, false);
            colorRTDesc.depthBufferBits = 0;
            colorRTDesc.msaaSamples = (MSAASamples)msaaSamples;
            colorRTDesc.enableRandomWrite = false;
            colorRTDesc.clearBuffer = true;
            colorRTDesc.clearColor = Color.black;
            colorRTDesc.name = name;

            return graph.CreateTexture(colorRTDesc);
        }

        public static void Render_DepthNormalPass(RenderingData renderingData, out TextureHandle normalHandle, TextureHandle depth)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<DepthPrepassData>("DepthNormalPrepass", out var passData, s_DepthNormalProfiler))
            {
                float width = GlobalRenderSettings.ResolutionRate * renderingData.cameraData.camera.pixelWidth;
                float height = GlobalRenderSettings.ResolutionRate * renderingData.cameraData.camera.pixelHeight;
                normalHandle = CreateNormalTexture(renderingData.renderGraph, (int)width, (int)height, (int)InsanityPipeline.asset.MSAASamples, "CameraNormal");
                normalHandle = builder.UseColorBuffer(normalHandle, 0);
                builder.UseDepthBuffer(depth, DepthAccess.Write);

                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque =
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_DepthNormalPrePassId, renderingData.cullingResults, renderingData.cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = renderingData.renderGraph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);

                builder.AllowPassCulling(false);

                //Builder
                builder.SetRenderFunc((DepthPrepassData data, RenderGraphContext context) =>
                {
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                    RenderingEventManager.InvokeEvent(RenderingEvents.DepthNormalPassEvent, context.renderContext, context.cmd);
                });
            }
        }
    }
}

