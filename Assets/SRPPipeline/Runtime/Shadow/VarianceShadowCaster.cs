using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class VarianceShadowPassData : ShadowPassData
    {

    }

    public class PrefilterShadowmapPass
    {

    }

    public class VarianceShadowCaster
    {
        FilterMode m_FilterMode = FilterMode.Bilinear;
        DepthBits m_DepthBufferBits = DepthBits.Depth16;
        GraphicsFormat m_colorFormat = GraphicsFormat.R16G16_UNorm;
        TextureHandle m_Shadowmap;
        public TextureDesc GetShadowMapTextureDesc(int shadowMapWidth, int shadowMapHeight)
        {
            return new TextureDesc(shadowMapWidth, shadowMapHeight)
            { filterMode = m_FilterMode, depthBufferBits = m_DepthBufferBits, useMipMap = true, 
                name = "VarianceShadowMap", wrapMode = TextureWrapMode.Clamp, colorFormat = m_colorFormat
            };
        }

        void SetShadowCasterKeyword(CommandBuffer cmd)
        {
            CoreUtils.SetKeyword(cmd, "_SHADOW_VSM", true);
        }

        public void RenderShadowmap()
        {

        }
    }
}
