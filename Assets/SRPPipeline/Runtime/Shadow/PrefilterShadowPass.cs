using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public enum eGaussianRadius
    {
        eGausian3x3,
        eGausian5x5,
        eGausian9x9,
        eGausian13x13
    }

    public class PrefilterShadowPassData
    {
        public Rect shadowRect;
        public TextureHandle m_BlurShadowmap;
        //public TextureHandle m_BlurShadowmap1;
        public TextureHandle m_Shadowmap;
        public Material m_FilterMaterial;
        public eGaussianRadius m_filterRadius;
        public bool flip = false;
    }

    public class PrefilterShadowPass
    {
        private Material m_prefilterMaterial = null;
        //eGaussianRadius m_filterRadius = eGaussianRadius.eGausian3x3;

        public TextureDesc GetShadowMapTextureDesc(int shadowMapWidth, int shadowMapHeight, string textureName, ShadowType shadowType)
        {
            return new TextureDesc(shadowMapWidth, shadowMapHeight)
            {
                filterMode = FilterMode.Bilinear,
                depthBufferBits = DepthBits.None,
                autoGenerateMips = true,
                useMipMap = true,
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                colorFormat = ShadowSettings.GetShadowmapFormat(shadowType) //GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RGHalf, false)
            };
        }

        private TextureHandle CreateBlurShadowTexture(RenderGraph graph, int shadowMapWidth, int shadowMapHeight, string textureName, ShadowType shadowType)
        {

            //Texture description
            TextureDesc colorRTDesc = GetShadowMapTextureDesc(shadowMapWidth, shadowMapHeight, textureName, shadowType);

            return graph.CreateTexture(colorRTDesc);
        }

        public PrefilterShadowPassData PrefilterShadowmap(RenderGraph graph, ShadowPassData shadowData)
        {
            using (var builder = graph.AddRenderPass<PrefilterShadowPassData>("Prefilter Shadow Pass", out var passData, 
                new ProfilingSampler("Prefilter Shadow Profiler")))
            {
                if (m_prefilterMaterial == null)
                {
                    m_prefilterMaterial = CoreUtils.CreateEngineMaterial("Insanity/Shadow PreFilter");
                }
                //Textures - Multi-RenderTarget
                TextureHandle BlurShadowMap = CreateBlurShadowTexture(graph, shadowData.m_ShadowmapWidth, 
                    shadowData.m_ShadowmapHeight, "BlurShadowMap", shadowData.m_ShadowType);
                //if (m_filterRadius != eGaussianRadius.eGausian3x3)
                //{
                //    passData.m_BlurShadowmap1 = CreateBlurShadowTexture(graph, shadowData.m_ShadowmapWidth,
                //    shadowData.m_ShadowmapHeight, "BlurShadowMap1");
                //}
                passData.m_BlurShadowmap = builder.WriteTexture(BlurShadowMap);
                passData.m_Shadowmap = builder.ReadWriteTexture(shadowData.m_Shadowmap);
                //builder.UseColorBuffer(passData.m_BlurShadowmap, 0);
                passData.shadowRect = new Rect(0, 0, shadowData.m_ShadowmapWidth, shadowData.m_ShadowmapHeight);
                passData.m_FilterMaterial = m_prefilterMaterial;
                passData.m_filterRadius = shadowData.m_ShadowPrefilterGaussianRadius;
                //Builder
                builder.SetRenderFunc((PrefilterShadowPassData data, RenderGraphContext context) =>
                {
                    data.m_FilterMaterial.SetInt("_FlipY", 0);
                    context.cmd.SetViewport(data.shadowRect);
                    CoreUtils.SetKeyword(context.cmd, "GAUSSIAN3x3", data.m_filterRadius == eGaussianRadius.eGausian3x3);
                    CoreUtils.SetKeyword(context.cmd, "GAUSSIAN5x5", data.m_filterRadius == eGaussianRadius.eGausian5x5);
                    CoreUtils.SetKeyword(context.cmd, "GAUSSIAN9x9", data.m_filterRadius == eGaussianRadius.eGausian9x9);
                    CoreUtils.SetKeyword(context.cmd, "GAUSSIAN13x13", data.m_filterRadius == eGaussianRadius.eGausian13x13);
                    if (data.m_filterRadius != eGaussianRadius.eGausian3x3)
                    {
                        //context.cmd.SetGlobalTexture("_MainLightShadowmapBlur", data.m_Shadowmap);
                        //context.cmd.SetGlobalTexture("_MainLightShadowmapBlur1", data.m_BlurShadowmap);
                        data.m_FilterMaterial.SetTexture("_MainLightShadowmapBlur", data.m_BlurShadowmap);
                        //data.m_FilterMaterial.SetTexture("_MainLightShadowmapBlur1", data.m_BlurShadowmap1);
                        context.cmd.SetRenderTarget(data.m_BlurShadowmap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        CoreUtils.DrawFullScreen(context.cmd, data.m_FilterMaterial, null, 0);
                        context.cmd.SetRenderTarget(data.m_Shadowmap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        //context.cmd.SetGlobalTexture("_MainLightShadowmapBlur1", data.m_BlurShadowmap);
                        CoreUtils.DrawFullScreen(context.cmd, data.m_FilterMaterial, null, 1);
                        //context.cmd.SetGlobalTexture("_ShadowMap", data.m_Shadowmap);
                    }
                    else
                    {
                        context.cmd.SetRenderTarget(data.m_BlurShadowmap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        CoreUtils.DrawFullScreen(context.cmd, data.m_FilterMaterial, null, 2);

                        //context.cmd.SetGlobalTexture("_ShadowMap", data.m_BlurShadowmap);
                    }
                });

                

                return passData;
            }
        }
    }
}


