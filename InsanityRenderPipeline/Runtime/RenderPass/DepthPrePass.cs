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
        //public TextureHandle m_Depth;
        //public TextureHandle m_Albedo;
    }



    public partial class RenderPasses
    {
        static ShaderTagId m_DepthPrePassId = new ShaderTagId("DepthPrepass");
        static ProfilingSampler s_DepthPrePassProfiler = new ProfilingSampler("DepthPrepass Profiler");

        public static DepthPrepassData Render_DepthPrePass(RenderingData renderingData, TextureHandle depth)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<DepthPrepassData>("DepthPrepass", out var passData, s_DepthPrePassProfiler))
            {
                //Textures - Multi-RenderTarget
                //TextureHandle Depth = CreateDepthTexture(renderingData.renderGraph, renderingData.cameraData.camera);
                builder.UseDepthBuffer(depth, DepthAccess.Write);
                
                //passData.m_Albedo = Albedo;

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


