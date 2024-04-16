using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public partial class RenderPasses
    {
        static private ProfilingSampler s_ClearProfilingSampler = new ProfilingSampler("Clear Targets");

        private class ClearTargetPassData
        {
            internal TextureHandle color;
            internal TextureHandle depth;

            internal RTClearFlags clearFlags;
            internal Color clearColor;
        }

        internal static void ClearTargetPass(RenderGraph graph, RTClearFlags clearFlags, Color clearColor, InsanityRenderer.FrameRenderSets frameRenderSets)
        {
            using (var builder = graph.AddRenderPass<ClearTargetPassData>("Clear Targets Pass", out var passData, s_ClearProfilingSampler))
            {
                passData.color = builder.UseColorBuffer(frameRenderSets.cameraColor, 0);
                passData.depth = builder.UseDepthBuffer(frameRenderSets.cameraDepth, DepthAccess.Write);
                passData.clearFlags = clearFlags;
                passData.clearColor = clearColor;

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((ClearTargetPassData data, RenderGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(data.clearFlags, data.clearColor, 1, 0);
                });
            }
        }
    }
}

