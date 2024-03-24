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

        public static DebugViewPassData DebugViewForwardPass(RenderingData renderingData, TextureHandle colorTarget, TextureHandle depthTarget)
        {
            using (var builder = renderingData.renderGraph.AddRenderPass<DebugViewPassData>("DebugView Forward Pass", out var passData,
                new ProfilingSampler("DebugView Forward Pass Profiler")))
            {
                //TextureHandle Albedo = CreateColorTexture(graph, cameraData.camera, "Albedo");
                passData.m_Albedo = builder.UseColorBuffer(colorTarget, 0);
                builder.UseDepthBuffer(depthTarget, DepthAccess.Read);


                // Renderers
                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Opaque =
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_DebugViewShaderTag, renderingData.cullingResults, renderingData.cameraData.camera);
                rendererDesc_base_Opaque.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererDesc_base_Opaque.renderQueueRange = RenderQueueRange.opaque;
                RendererListHandle rHandle_base_Opaque = renderingData.renderGraph.CreateRendererList(rendererDesc_base_Opaque);
                passData.m_renderList_opaque = builder.UseRendererList(rHandle_base_Opaque);
                passData.m_AdditionalLightsEnable = renderingData.supportAdditionalLights;
                m_DebugViewVariablesCB.debugViewType = (int)debugViewType;
                passData.m_debugViewVariable = m_DebugViewVariablesCB;

                UnityEngine.Rendering.RendererUtils.RendererListDesc rendererDesc_base_Transparent =
                    new UnityEngine.Rendering.RendererUtils.RendererListDesc(m_DebugViewShaderTag, renderingData.cullingResults, renderingData.cameraData.camera);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DebugViewPassData data, RenderGraphContext context) =>
                {
                    ConstantBuffer.PushGlobal(context.cmd, passData.m_debugViewVariable, ShaderIDs._DebugViewVariables);
                    CoreUtils.SetKeyword(context.cmd, "_ADDITIONAL_LIGHTS", data.m_AdditionalLightsEnable);
                    CoreUtils.DrawRendererList(context.renderContext, context.cmd, data.m_renderList_opaque);
                    
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
        }

        

        public static void ShowDebugPass(RenderingData renderingData, ref DebugViewGPUResources debugViewTextures, Material finalBlitMaterial)
        {
            if (finalBlitMaterial == null)
                finalBlitMaterial = CoreUtils.CreateEngineMaterial("Insanity/DebugViewBlit");

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
                passData.flip = renderingData.cameraData.isMainGameView;
                passData.m_finalBlitMaterial = finalBlitMaterial;
                passData.m_finalBlitMaterial.SetInt("_FlipY", passData.flip ? 1 : 0);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DebugViewBlitPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_DepthTexture", data.m_Depth);
                    context.cmd.SetGlobalBuffer("_LightVisibilityIndexBuffer", data.m_LightVisibilityIndexBuffer);
                    context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    //context.cmd.ClearRenderTarget(true, renderingData.cameraData.camera.cameraType != CameraType.SceneView, renderingData.cameraData.camera.backgroundColor);
                    context.cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                    CoreUtils.DrawFullScreen(context.cmd, data.m_finalBlitMaterial);

                });
            }
        }
    }
}
