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



    public partial class InsanityPipeline
    {
        ShaderTagId m_DepthPrePassId = new ShaderTagId("DepthPrepass");

        private TextureHandle CreateDepthTexture(RenderGraph graph, Camera camera)
        {
            bool colorRT_sRGB = false;

            //Texture description
            TextureDesc colorRTDesc = new TextureDesc(camera.pixelWidth, camera.pixelHeight);
            colorRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Depth, colorRT_sRGB);
            colorRTDesc.depthBufferBits = DepthBits.Depth24;
            colorRTDesc.msaaSamples = MSAASamples.None;
            colorRTDesc.enableRandomWrite = false;
            colorRTDesc.clearBuffer = true;
            colorRTDesc.clearColor = Color.black;
            colorRTDesc.name = "Depth";

            return graph.CreateTexture(colorRTDesc);
        }

        private TextureHandle CreateColorTexture(RenderGraph graph, Camera camera, string name)
        {
            bool colorRT_sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);

            //Texture description
            TextureDesc colorRTDesc = new TextureDesc(camera.pixelWidth, camera.pixelHeight);
            colorRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, colorRT_sRGB);
            colorRTDesc.depthBufferBits = 0;
            colorRTDesc.msaaSamples = MSAASamples.None;
            colorRTDesc.enableRandomWrite = false;
            colorRTDesc.clearBuffer = true;
            colorRTDesc.clearColor = Color.black;
            colorRTDesc.name = name;

            return graph.CreateTexture(colorRTDesc);
        }

        public DepthPrepassData Render_DepthPrePass(CameraData cameraData, RenderGraph graph, CullingResults cull)
        {
            using (var builder = graph.AddRenderPass<DepthPrepassData>("DepthPrepass", out var passData, new ProfilingSampler("DepthPrepass Profiler")))
            {
                //Textures - Multi-RenderTarget
                TextureHandle Depth = CreateDepthTexture(graph, cameraData.camera);
                passData.m_Depth = builder.UseDepthBuffer(Depth, DepthAccess.ReadWrite);
                TextureHandle Albedo = CreateColorTexture(graph, cameraData.camera, "Albedo");
                passData.m_Albedo = Albedo;

                //Renderers
                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque = 
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_DepthPrePassId, cull, cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = graph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);

                //Builder
                builder.SetRenderFunc((DepthPrepassData data, RenderGraphContext context) =>
                {
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                });

                return passData;
            }
        }
    }
}


