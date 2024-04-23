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
            [Reload("Runtime/ShaderLibrary/Blit.shader"), SerializeField]
            public Shader Blit;
            [Reload("Runtime/ShaderLibrary/HDRISky.shader")]
            public Shader HDRISky;
            [Reload("Runtime/ShaderLibrary/ParallelScan.compute")]
            public ComputeShader ParallelScan;
            [Reload("Runtime/ShaderLibrary/Shadow/ScreenSpaceShadow.shader")]
            public Shader ScreenSpaceShadow;
            public Shader CopyDepth;
            [Reload("Runtime/ShaderLibrary/Debug/DebugViewBlit.shader")]
            public Shader DebugViewBlit;
            [Reload("Runtime/ShaderLibrary/TileBasedLightCulling.compute")]
            public ComputeShader TileBasedLightCulling;
            //public ComputeShader TileFrustumCompute;
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

        [Serializable, ReloadGroup]
        public sealed class InternalTextures
        {
            public Texture BRDFLut;
        }

        public ShaderResources shaders;
        public MaterialResources materials;
        public InternalTextures internalTextures;
    }
}


