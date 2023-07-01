using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class SkyboxLUTPassData
    {
        public RenderTexture skyboxLUT;
        public float mieG;
        public float scatteringScaleR;
        public float scatteringScaleM;
        public ComputeShader csSkyboxLUT;
        public int kernelSkyboxLUT = -1;
    }


    public class Atmosphere
    {
        public const float kAtmosphereHeight = 80000.0f;
        public const float kEarthRadius = 6371000.0f;
        public static Vector4 kRayleighScatteringCoef = new Vector4(6.55f, 17.3f, 23.0f, 0) * 0.000001f;
        public static Vector4 kMieScatteringCoef = new Vector4(2.0f, 2.0f, 2.0f, 0) * 0.00001f;
        protected RenderTexture m_SkyboxLUT;
        protected int kernelSkyboxLUT = -1;
        Vector3Int _skyboxLUTSize = new Vector3Int(32, 64, 32);

        public class AtmosphereShaderParameters
        {
            public static int _AtmosphereHeight;
            public static int _EarthRadius;
            public static int _RayleighHeightScale;
            public static int _MieHeightScale;
            public static int _BetaRayleigh;
            public static int _BetaMie;
            public static int _MieG;
            public static int _SkyboxLUT;
            public static int _SunLightColor;
        }

        public Atmosphere()
        {
            AtmosphereShaderParameters._AtmosphereHeight = Shader.PropertyToID("_AtmosphereHeight");
            AtmosphereShaderParameters._EarthRadius = Shader.PropertyToID("_EarthRadius");
            AtmosphereShaderParameters._RayleighHeightScale = Shader.PropertyToID("_RayleighHeightScale");
            AtmosphereShaderParameters._MieHeightScale = Shader.PropertyToID("_MieHeightScale");
            AtmosphereShaderParameters._BetaRayleigh = Shader.PropertyToID("_BetaRayleigh");
            AtmosphereShaderParameters._BetaMie = Shader.PropertyToID("_BetaMie");
            AtmosphereShaderParameters._MieG = Shader.PropertyToID("_MieG");
            AtmosphereShaderParameters._SkyboxLUT = Shader.PropertyToID("_SkyboxLUT");
            AtmosphereShaderParameters._SunLightColor = Shader.PropertyToID("_SunLightColor");
        }

        public bool IsSkyboxLUTValid()
        {
            return m_SkyboxLUT != null;
        }

        RenderTexture CreateSkyboxLUT()
        {
            //TextureDesc textureDesc = new TextureDesc()
            //{
            //    width = 64,
            //    height = 64,
            //    slices = 64,
            //    depthBufferBits = 0,
            //    dimension = TextureDimension.Tex3D,
            //    enableRandomWrite = true,
            //    colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
            //    useMipMap = false
            //};

            //renderGraph.CreateTextureIfInvalid(textureDesc, ref m_SkyboxLUT);
            if (m_SkyboxLUT == null)
            {
                m_SkyboxLUT = new RenderTexture(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                //m_SkyboxLUT.isVolume
                m_SkyboxLUT.dimension = TextureDimension.Tex3D;
                m_SkyboxLUT.volumeDepth = _skyboxLUTSize.z;
                m_SkyboxLUT.autoGenerateMips = false;
                m_SkyboxLUT.enableRandomWrite = true;
                m_SkyboxLUT.name = "SkyboxLUT";
                m_SkyboxLUT.wrapMode = TextureWrapMode.Clamp;
                m_SkyboxLUT.Create();

            }
            return m_SkyboxLUT;
        }

        public RenderTexture SkyboxLUT
        {
            get { return m_SkyboxLUT; }
        }

        public SkyboxLUTPassData GenerateSkyboxLUT(RenderGraph renderGraph, InsanityPipelineAsset asset, ComputeShader cs)
        {
            using (var builder = renderGraph.AddRenderPass<SkyboxLUTPassData>("Generate Skybox LUT", 
                out var passData, new ProfilingSampler("SkyboxLUT Profiler")))
            {
                passData.skyboxLUT = CreateSkyboxLUT();//builder.WriteTexture(CreateSkyboxLUT(renderGraph));
                passData.scatteringScaleR = asset.ScatteringScaleR;
                passData.scatteringScaleM = asset.ScatteringScaleM;
                passData.mieG = asset.MieG;
                passData.csSkyboxLUT = cs;
                if (passData.kernelSkyboxLUT == -1)
                {
                    passData.kernelSkyboxLUT = cs.FindKernel("PrecomputeScattering");
                }

                builder.SetRenderFunc(
                    (SkyboxLUTPassData data, RenderGraphContext ctx) =>
                    {
                        if (data.kernelSkyboxLUT != -1)
                        {
                            ctx.cmd.SetComputeFloatParam(data.csSkyboxLUT, AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
                            ctx.cmd.SetComputeFloatParam(data.csSkyboxLUT, AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaRayleigh, Atmosphere.kRayleighScatteringCoef);
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaMie, kMieScatteringCoef);
                            ctx.cmd.SetComputeFloatParam(data.csSkyboxLUT, AtmosphereShaderParameters._MieG, data.mieG);
                            ctx.cmd.SetComputeTextureParam(data.csSkyboxLUT, data.kernelSkyboxLUT, AtmosphereShaderParameters._SkyboxLUT, data.skyboxLUT);
                            ctx.cmd.DispatchCompute(data.csSkyboxLUT, data.kernelSkyboxLUT, _skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z);
                        }
                    }
                    );

                return passData;
            }
        }

    }

    
}
