using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class GBufferPassData
    {
        public TextureHandle[] m_GBuffer;
        public TextureHandle m_Depth;
    }
    public partial class RenderPasses
    {
        public static void GBufferPass(RenderGraph renderGraph, TextureHandle[] gBuffers, TextureHandle depth)
        {
            using (var builder = renderGraph.AddRenderPass<GBufferPassData>("GBuffer Pass", out var passData, new ProfilingSampler("GBuffer Pass Profiler")))
            {
                passData.m_GBuffer = gBuffers;
                for (int i = 0; i < gBuffers.Length; i++)
                {
                    gBuffers[i] = builder.UseColorBuffer(gBuffers[i], i);
                }

                passData.m_Depth = builder.UseDepthBuffer(depth, DepthAccess.Read);
            }
        }
    }
}

