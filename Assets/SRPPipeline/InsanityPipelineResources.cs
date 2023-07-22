using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    public class InsanityPipelineResources : RenderPipelineResources
    {
        [Serializable, ReloadGroup]
        public sealed class ShaderResources
        {
            [Reload("Runtime/ShaderLibrary/Blit.shader")]
            public Shader Blit;
            [Reload("Runtime/ShaderLibrary/HDRISky.shader")]
            public Shader HDRISky;
            [Reload("Runtime/ShaderLibrary/ParallelScan.compute")]
            public ComputeShader ParallelScan;
        }

        [Serializable, ReloadGroup]
        public sealed class MaterialResources
        {
            [Reload("Runtime/Materials/Blit.mat")]
            public Material Blit;
            [Reload("Runtime/Materials/Skybox.mat")]
            public Material Skybox;

            public Material PhysicalBaseSky;
        }

        public ShaderResources shaders;
        public MaterialResources materials;
    }
}


