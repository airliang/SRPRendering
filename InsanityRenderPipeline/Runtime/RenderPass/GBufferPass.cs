using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class GBuffer
    {
        public enum GBufferIndex
        {
            AlbedoMetallic = 0,
            NormalSmoothness = 1,
            Specular = 2,
            TransformID = 3,
            MaxGBuffer = 4,
        };

        public TextureHandle[] GBufferAttachments;

        TextureDesc GetGBufferTextureDesc(GBufferIndex index, int width, int height)
        {
            switch (index)
            {
                case GBufferIndex.AlbedoMetallic:
                
                    return new TextureDesc(width, height)
                    {
                        colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                        dimension = TextureDimension.Tex2D,
                        filterMode = FilterMode.Point,
                        enableRandomWrite = true,
                        useMipMap = false,
                        name = "GBufferAlbedoMetallic"
                    };
                case GBufferIndex.NormalSmoothness:
                    return new TextureDesc(width, height)
                    {
                        colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                        dimension = TextureDimension.Tex2D,
                        filterMode = FilterMode.Point,
                        enableRandomWrite = true,
                        useMipMap = false,
                        name = "GBufferNormalSmoothness"
                    };
                case GBufferIndex.Specular:
                    return new TextureDesc(width, height)
                    {
                        colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                        dimension = TextureDimension.Tex2D,
                        filterMode = FilterMode.Point,
                        enableRandomWrite = true,
                        useMipMap = false,
                        name = "GBufferSpecular"
                    };
                default:
                    return new TextureDesc(width, height);
            }
        }

        public void CreateGBufferTextures(RenderGraph renderGraph)
        {
            if (GBufferAttachments == null)
            {
                GBufferAttachments = new TextureHandle[(int)GBufferIndex.MaxGBuffer];
            }
            int requestWidth = (int)GlobalRenderSettings.screenResolution.width;
            int requestHeight = (int)GlobalRenderSettings.screenResolution.height;
            
            for (int i = 0; i < 2; i++)
            {
                renderGraph.CreateTextureIfInvalid(GetGBufferTextureDesc((GBufferIndex)i, requestWidth, requestHeight), ref GBufferAttachments[i]);
                //GBufferAttachments[i] = renderGraph.CreateTexture(GetGBufferTextureDesc((GBufferIndex)i, requestWidth, requestHeight));
            }
        }
    }

    public class GBufferPassData
    {
        public TextureHandle m_AlbedoMetallic;
        public TextureHandle m_NormalSmoothness;
        public TextureHandle m_Depth;
        public RendererListHandle m_renderList_opaque;
    }
    public partial class RenderPasses
    {
        static ShaderTagId m_GBufferPass = new ShaderTagId("InsanityGBuffer");
        public static GBuffer s_GBuffer = new GBuffer();

        public static void GBufferPass(RenderingData renderingData, TextureHandle depth, out TextureHandle normal)
        {
            s_GBuffer.CreateGBufferTextures(renderingData.renderGraph);
            using (var builder = renderingData.renderGraph.AddRenderPass<GBufferPassData>("GBuffer Pass", out var passData, new ProfilingSampler("GBuffer Pass Profiler")))
            {
                passData.m_AlbedoMetallic = builder.UseColorBuffer(s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.AlbedoMetallic], 0);

                passData.m_NormalSmoothness = builder.UseColorBuffer(s_GBuffer.GBufferAttachments[(int)GBuffer.GBufferIndex.NormalSmoothness], 1);
                normal = passData.m_NormalSmoothness;

                builder.UseDepthBuffer(depth, DepthAccess.Read);

                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque =
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_GBufferPass, renderingData.cullingResults, renderingData.cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = renderingData.renderGraph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((GBufferPassData data, RenderGraphContext context) =>
                {
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                    RenderingEventManager.InvokeEvent(RenderingEvents.GBufferPassEvent, context.renderContext, context.cmd);
                });
            }
        }
    }
}

