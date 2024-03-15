using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class RenderingData
    {
        public CameraData cameraData;
        public RenderGraph renderGraph;
        public CullingResults cullingResults;
        public PerObjectData perObjectData;
        public bool supportAdditionalLights;
    }
}

