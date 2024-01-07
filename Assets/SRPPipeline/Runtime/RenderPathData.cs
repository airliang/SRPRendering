using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Insanity
{
    public abstract class RenderPathData : ScriptableObject
    {
        public abstract RenderPath Create(InsanityPipeline pipeline);
    }
}

