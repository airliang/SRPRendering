using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static Insanity.InsanityPipeline;

namespace Insanity
{
    public class DebugView
    {
        public enum DebugViewType
        {
            None,
            TileBasedCullingResult,
            Depth,
            LinearDepth,
            WorldSpaceNormal,
            TriangleOverDraw,
        }

        unsafe public struct DebugViewVariables
        {
            public int debugViewType;
            public float depthScale;
            public Vector2 pad;
            //public Vector3 pad;
        }

        public static DebugViewType debugViewType = DebugViewType.None;

        public static bool NeedDebugView()
        {
            return debugViewType != DebugViewType.None;
        }

        public class DebugViewPassData
        {
            public TextureHandle m_displayTexture;
            public Material m_finalBlitMaterial;
            public RendererListHandle m_renderList_opaque;
            public TextureHandle m_Albedo;
            public bool m_AdditionalLightsEnable;
            public DebugViewVariables m_debugViewVariable;
        }

        static ShaderTagId m_DebugViewShaderTag = new ShaderTagId("DebugView");
        public static DebugViewVariables m_DebugViewVariablesCB = new DebugViewVariables();

        public static DebugViewPassData DebugViewPreparePass(RenderingData renderingData/*, TextureHandle colorTarget, TextureHandle depthTarget*/)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<DebugViewPassData>("DebugView Prepare Pass", out var passData,
                new ProfilingSampler("DebugView Prepare Pass Profiler")))
            {
                m_DebugViewVariablesCB.debugViewType = (int)debugViewType;
                passData.m_debugViewVariable = m_DebugViewVariablesCB;

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DebugViewPassData data, RenderGraphContext context) =>
                {
                    ConstantBuffer.PushGlobal(context.cmd, passData.m_debugViewVariable, ShaderIDs._DebugViewVariables);
                    context.renderContext.ExecuteCommandBuffer(context.cmd);
                    context.cmd.Clear();

                });
                return passData;
            }
        }

        public struct DebugViewGPUResources
        {
            public TextureHandle m_Depth;
            //public TextureHandle m_TileVisibleLightCount;
            public TextureHandle m_Normal;
            public TextureHandle m_Overdraw;
            public ComputeBuffer m_LightVisibilityIndexBuffer;
        }

        public class DebugViewBlitPassData
        {
            //public TextureHandle m_Source;
            public TextureHandle m_Depth;
            //public TextureHandle m_TileVisibleLightCount;
            public ComputeBuffer m_LightVisibilityIndexBuffer;
            public TextureHandle m_Normal;
            public TextureHandle m_Overdraw;
            public bool flip;
            public Material m_finalBlitMaterial;
            public int m_DebugViewMode;
            public TextureHandle m_Dest;
        }

        

        public static void ShowDebugPass(RenderingData renderingData, ref DebugViewGPUResources debugViewTextures, TextureHandle colorTarget, Material finalBlitMaterial, int debugViewMode)
        {
            if (finalBlitMaterial == null)
                finalBlitMaterial = CoreUtils.CreateEngineMaterial(asset.InsanityPipelineResources.shaders.DebugViewBlit);

            using (var builder = renderingData.renderGraph.AddRenderPass<DebugViewBlitPassData>("DebugViewBlitPass", out var passData, new ProfilingSampler("DebugView Blit Profiler")))
            {
                //if (debugViewType == DebugViewType.Depth)
                {
                    passData.m_Depth = builder.ReadTexture(debugViewTextures.m_Depth);
                }
                //else if (debugViewType == DebugViewType.WorldSpaceNormal)
                {
                    //passData.m_Normal = builder.ReadTexture(debugViewTextures.m_Normal);
                    //passData.m_Overdraw = builder.ReadTexture(debugViewTextures.m_Overdraw);
                }
                //else if (debugViewType == DebugViewType.TileBasedCullingResult)
                {
                    passData.m_LightVisibilityIndexBuffer = debugViewTextures.m_LightVisibilityIndexBuffer;
                }
                passData.m_Dest = builder.UseColorBuffer(colorTarget, 0);
                passData.flip = renderingData.cameraData.isMainGameView;
                passData.m_finalBlitMaterial = finalBlitMaterial;
                passData.m_finalBlitMaterial.SetInt("_FlipY", passData.flip ? 1 : 0);
                passData.m_DebugViewMode = debugViewMode;

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DebugViewBlitPassData data, RenderGraphContext context) =>
                {
                    data.m_finalBlitMaterial.SetInt("_DebugViewMode", data.m_DebugViewMode);
                    context.cmd.SetGlobalTexture("_DepthTexture", data.m_Depth);
                    context.cmd.SetGlobalBuffer("_LightVisibilityIndexBuffer", data.m_LightVisibilityIndexBuffer);
                    context.cmd.SetRenderTarget(data.m_Dest);
                    
                    //context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    //context.cmd.ClearRenderTarget(true, renderingData.cameraData.camera.cameraType != CameraType.SceneView, renderingData.cameraData.camera.backgroundColor);
                    context.cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.m_finalBlitMaterial, 0, MeshTopology.Triangles, 3);
                    //CoreUtils.DrawFullScreen(context.cmd, data.m_finalBlitMaterial);

                });
            }
        }
    }
}
