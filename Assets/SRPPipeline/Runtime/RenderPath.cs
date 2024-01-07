using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    public abstract class RenderPath : IDisposable
    {
        public abstract void RenderFrame(ScriptableRenderContext context, RenderingData renderingData, ref CullingResults cull);

        virtual public void Dispose()
        {
            
        }

        internal static RenderPath current = null;

        public InsanityPipeline currentPipeline = null;
    }
}

