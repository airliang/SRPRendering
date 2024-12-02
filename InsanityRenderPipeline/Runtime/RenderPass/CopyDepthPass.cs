using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class CopyDepthPassData
    {
        public TextureHandle m_Destination;
        public TextureHandle m_Source;
        public Material m_CopyMaterial;
        public int m_MSAASamples;
    }

    public partial class RenderPasses
    {
        public static class CopyDepthKeywordStrings
        {
            /// <summary> Keyword used for no Multi Sampling Anti-Aliasing (MSAA). </summary>
            public const string DepthNoMsaa = "_DEPTH_NO_MSAA";

            /// <summary> Keyword used for Multi Sampling Anti-Aliasing (MSAA) with 2 per pixel sample count. </summary>
            public const string DepthMsaa2 = "_DEPTH_MSAA_2";

            /// <summary> Keyword used for Multi Sampling Anti-Aliasing (MSAA) with 4 per pixel sample count. </summary>
            public const string DepthMsaa4 = "_DEPTH_MSAA_4";

            /// <summary> Keyword used for Multi Sampling Anti-Aliasing (MSAA) with 8 per pixel sample count. </summary>
            public const string DepthMsaa8 = "_DEPTH_MSAA_8";
        }
        public static void CopyDepthPass(RenderingData renderingData, out TextureHandle dest, in TextureHandle source, Material copyDepthMaterial, int msaaSamples)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<CopyDepthPassData>("Copy Depth Pass", out var passData, new ProfilingSampler("Copy Depth Pass Profiler")))
            {
                //var depthDescriptor = renderingData.cameraData.GetCameraTargetDescriptor(InsanityPipeline.asset.ResolutionRate, InsanityPipeline.asset.HDREnable, (int)InsanityPipeline.asset.MSAASamples);
                //depthDescriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                //depthDescriptor.depthStencilFormat = GraphicsFormat.None;
                //depthDescriptor.depthBufferBits = 0;
                //depthDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA
                float width = GlobalRenderSettings.ResolutionRate * renderingData.cameraData.camera.pixelWidth;
                float height = GlobalRenderSettings.ResolutionRate * renderingData.cameraData.camera.pixelHeight;
                TextureDesc depthRTDesc = new TextureDesc((int)width, (int)height);
                depthRTDesc.colorFormat = GraphicsFormat.R16_SFloat;
                depthRTDesc.depthBufferBits = 0;
                depthRTDesc.msaaSamples = MSAASamples.None;
                depthRTDesc.enableRandomWrite = false;
                depthRTDesc.name = "DepthTextureResolved";
                dest = renderingData.renderGraph.CreateTexture(depthRTDesc);

                passData.m_Destination = builder.UseColorBuffer(dest, 0);
                passData.m_Source = builder.ReadTexture(source);
                passData.m_CopyMaterial = copyDepthMaterial;
                passData.m_MSAASamples = msaaSamples;

                builder.SetRenderFunc((CopyDepthPassData data, RenderGraphContext context) =>
                {
                    switch (data.m_MSAASamples)
                    {
                        case 8:
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa2);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa4);
                            context.cmd.EnableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa8);
                            break;

                        case 4:
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa2);
                            context.cmd.EnableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa4);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa8);
                            break;

                        case 2:
                            context.cmd.EnableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa2);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa4);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa8);
                            break;

                        // MSAA disabled, auto resolve supported or ms textures not supported
                        default:
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa2);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa4);
                            context.cmd.DisableShaderKeyword(CopyDepthKeywordStrings.DepthMsaa8);
                            break;
                    }
                    context.cmd.SetGlobalTexture("_CameraDepthAttachment", data.m_Source);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.m_CopyMaterial, 0, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}

