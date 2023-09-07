using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    
    public class ForwardPassData
    {
        public RendererListHandle m_renderList_opaque;
        public RendererListHandle m_renderList_transparent;
        public TextureHandle m_Albedo;
        public TextureHandle m_ShadowMap;
    }

    public partial class InsanityPipeline
    {
        ShaderTagId m_ForwardPass = new ShaderTagId("InsanityForward");


        public ForwardPassData Render_OpaqueFowardPass(CameraData cameraData, RenderGraph graph, CullingResults cull, 
            DepthPrepassData depthData, TextureHandle shadowmap)
        {
            using (var builder = graph.AddRenderPass<ForwardPassData>("Opaque Forward Pass", out var passData, 
                new ProfilingSampler("Opaque Forward Pass Profiler")))
            {
                //TextureHandle Albedo = CreateColorTexture(graph, cameraData.camera, "Albedo");
                passData.m_Albedo = builder.UseColorBuffer(depthData.m_Albedo, 0);
                builder.UseDepthBuffer(depthData.m_Depth, DepthAccess.Read);
                //if (shadowData != null)
                if (shadowmap.IsValid())
                    passData.m_ShadowMap = builder.ReadTexture(shadowmap);
                // Renderers
                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque = 
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_ForwardPass, cull, cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = graph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);

                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Transparent = 
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_ForwardPass, cull, cameraData.camera);
               

                builder.SetRenderFunc((ForwardPassData data, RenderGraphContext context) =>
                {
                    if (data.m_ShadowMap.IsValid())
                        context.cmd.SetGlobalTexture("_ShadowMap", data.m_ShadowMap);
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                    RenderingEventManager.InvokeEvent(RenderingEvents.OpaqueForwardPassEvent, context.renderContext, context.cmd);
                });
                return passData;
            }
        }
    }
}


