using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class DepthPrepassData
    {
        public RendererListHandle m_renderList_opaque;
        public TextureHandle m_Depth;
        public TextureHandle m_Albedo;
    }



    public partial class RenderPasses
    {
        static ShaderTagId m_DepthPrePassId = new ShaderTagId("DepthPrepass");

        private static TextureHandle CreateDepthTexture(RenderGraph graph, Camera camera)
        {
            //Texture description
            float width = GlobalRenderSettings.ResolutionRate * camera.pixelWidth;
            float height = GlobalRenderSettings.ResolutionRate * camera.pixelHeight;
            TextureDesc depthRTDesc = new TextureDesc((int)width, (int)height);
            depthRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Depth, false);
            depthRTDesc.depthBufferBits = DepthBits.Depth24;
            depthRTDesc.msaaSamples = MSAASamples.None;
            depthRTDesc.enableRandomWrite = false;
            depthRTDesc.clearBuffer = true;
            depthRTDesc.clearColor = Color.black;
            depthRTDesc.name = "Depth";

            return graph.CreateTexture(depthRTDesc);
        }

        private static TextureHandle CreateColorTexture(RenderGraph graph, Camera camera, string name)
        {
            bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);

            //Texture description
            float width = GlobalRenderSettings.ResolutionRate * camera.pixelWidth;
            float height = GlobalRenderSettings.ResolutionRate * camera.pixelHeight;
            TextureDesc colorRTDesc = new TextureDesc((int)width, (int)height);
            colorRTDesc.colorFormat = GlobalRenderSettings.HDREnable ? GraphicsFormat.R16G16B16A16_SFloat 
                : GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);
            colorRTDesc.depthBufferBits = 0;
            colorRTDesc.msaaSamples = MSAASamples.None;
            colorRTDesc.enableRandomWrite = false;
            colorRTDesc.clearBuffer = true;
            colorRTDesc.clearColor = Color.black;
            colorRTDesc.name = name;

            return graph.CreateTexture(colorRTDesc);
        }

        public static DepthPrepassData Render_DepthPrePass(RenderingData renderingData)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<DepthPrepassData>("DepthPrepass", out var passData, new ProfilingSampler("DepthPrepass Profiler")))
            {
                //Textures - Multi-RenderTarget
                TextureHandle Depth = CreateDepthTexture(renderingData.renderGraph, renderingData.cameraData.camera);
                passData.m_Depth = builder.UseDepthBuffer(Depth, DepthAccess.ReadWrite);
                TextureHandle Albedo = CreateColorTexture(renderingData.renderGraph, renderingData.cameraData.camera, "Albedo");
                passData.m_Albedo = Albedo;

                //Renderers
                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque = 
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_DepthPrePassId, renderingData.cullingResults, renderingData.cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = renderingData.renderGraph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);

                builder.AllowPassCulling(false);

                //Builder
                builder.SetRenderFunc((DepthPrepassData data, RenderGraphContext context) =>
                {
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                    RenderingEventManager.InvokeEvent(RenderingEvents.DepthPassEvent, context.renderContext, context.cmd);
                });

                return passData;
            }
        }
    }
}


