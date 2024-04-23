using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    //public class ShadowPassData
    //{
    //    public TextureHandle m_Shadowmap;
    //    public ShadowDrawingSettings shadowDrawSettings;
    //    public int cascadeCount;
    //}

    public partial class InsanityPipeline
    {
        ShaderTagId m_ShadowPass = new ShaderTagId("ShadowCaster");
        int m_ShadowRes = 512;

        private TextureHandle CreateShadowTexture(RenderGraph graph, int width, int height)
        {
            //Texture description
            TextureDesc shadowMapRTDesc = new TextureDesc(width, height);
            shadowMapRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Shadowmap, false);
            shadowMapRTDesc.depthBufferBits = DepthBits.Depth24;
            shadowMapRTDesc.msaaSamples = MSAASamples.None;
            shadowMapRTDesc.enableRandomWrite = false;
            shadowMapRTDesc.clearBuffer = true;
            shadowMapRTDesc.clearColor = Color.black;
            shadowMapRTDesc.name = "Shadowmap";
            shadowMapRTDesc.isShadowMap = true;

            return graph.CreateTexture(shadowMapRTDesc);
        }

        public void RenderShadowMaps(RenderGraph graph, CullingResults cull, in ShaderVariablesGlobal globalCBData, Light light, int lightIndex, DepthPrepassData depthData)
        {
            using (var builder = graph.AddRenderPass<ShadowPassData>("Shadow Caster Pass", out var passData, new ProfilingSampler("Shadow Caster Pass Profiler")))
            {
                Bounds bounds;
                bool doShadow = light.type == LightType.Directional && light.shadows != LightShadows.None && cull.GetShadowCasterBounds(lightIndex, out bounds);

                //************************** Shadow Mapping ************************************
                if (doShadow)
                {
                    Matrix4x4 view = Matrix4x4.identity;
                    Matrix4x4 proj = Matrix4x4.identity;
                    ShadowSplitData splitData;


                    bool successShadowMap = false;

                    successShadowMap = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives
                    (
                        lightIndex,
                        0, 1, new Vector3(1, 0, 0),
                        m_ShadowRes, light.shadowNearPlane, out view, out proj, out splitData
                    );

                    if (successShadowMap)
                    {
                        passData.m_Shadowmap = builder.WriteTexture(CreateShadowTexture(graph, m_ShadowRes, m_ShadowRes));
                        builder.UseDepthBuffer(passData.m_Shadowmap, DepthAccess.ReadWrite);
                        builder.SetRenderFunc(
                            (ShadowPassData data, RenderGraphContext ctx) =>
                            {
                                ctx.cmd.SetRenderTarget(data.m_Shadowmap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);


                                for (int i = 0; i < data.cascadeCount; ++i)
                                {
                                    ctx.cmd.SetViewport(new Rect(0, 0, m_ShadowRes, m_ShadowRes));
                                    ctx.cmd.EnableScissorRect(new Rect(4, 4, m_ShadowRes - 8, m_ShadowRes - 8));
                                    ctx.cmd.SetViewProjectionMatrices(view, proj);
                                    ctx.renderContext.DrawShadows(ref data.shadowDrawSettings);
                                }
                                ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                            }
                            );
                        
                    }
                }
            }
        }
    }
}
