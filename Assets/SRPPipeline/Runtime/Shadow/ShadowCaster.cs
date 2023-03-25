using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public interface IShadowCaster
    {
        TextureDesc GetShadowMapTextureDesc(int shadowMapWidth, int shadowMapHeight);
        void SetShadowCasterKeyword(CommandBuffer cmd);
    }
}
