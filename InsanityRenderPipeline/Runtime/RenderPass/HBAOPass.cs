using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace Insanity
{
    public class SSAOSettings
    {
        public float radius;
        public bool halfResolution = true;
    }

    public partial class RenderPasses
    {
        class HBAOPassData
        {
            public TextureHandle depth;
            public TextureHandle normal;
            public Vector4 HBAOParams;
        }

        void Render_HBAOPass(RenderingData renderingData, TextureHandle depth, TextureHandle normal)
        {

        }
    }
}

