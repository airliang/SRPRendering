using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static UnityEditor.ShaderData;

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
        public int kernelMultipleSkyboxLUT = -1;
        public int multipleScatteringOrder = 0;
        public RenderTexture preOrderScatteringLUT;
        public RenderTexture tmpMultipleScatteringOrderOutput;
    }


    public class Atmosphere
    {
        public const float kAtmosphereHeight = 80000.0f;
        public const float kEarthRadius = 6371000.0f;
        public static Vector4 kRayleighScatteringCoef = new Vector4(6.55f, 17.3f, 23.0f, 0) * 0.000001f;
        public static Vector4 kMieScatteringCoef = new Vector4(2.0f, 2.0f, 2.0f, 0) * 0.00001f;
        private RenderTexture m_SkyboxLUT;
        private int kernelSkyboxLUT = -1;
        private RenderTexture m_PreOrderInputLUT;
        private RenderTexture m_CurOrderOutputLUT;
        int kernelMultipleSkyboxLUT = -1;
        private bool m_MultipleScattering = false;
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
            public static int _PreOrderScatteringLUT;
            public static int _OutScatteringLUT;
        }

        private static void InitShaderParameters()
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
            AtmosphereShaderParameters._PreOrderScatteringLUT = Shader.PropertyToID("_PreOrderScatteringLUT");
            AtmosphereShaderParameters._OutScatteringLUT = Shader.PropertyToID("_OutScatteringLUT");
        }

        public Atmosphere()
        {
            InitShaderParameters();
        }

        public bool IsSkyboxLUTValid()
        {
            return m_SkyboxLUT != null;
        }

        RenderTexture CreateSkyboxLUT()
        {
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

        RenderTexture CreateMultipleScatteringLUT(string name)
        {
            if (m_CurOrderOutputLUT == null)
            {
                m_CurOrderOutputLUT = new RenderTexture(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                //m_SkyboxLUT.isVolume
                m_CurOrderOutputLUT.dimension = TextureDimension.Tex3D;
                m_CurOrderOutputLUT.volumeDepth = _skyboxLUTSize.z;
                m_CurOrderOutputLUT.autoGenerateMips = false;
                m_CurOrderOutputLUT.enableRandomWrite = true;
                m_CurOrderOutputLUT.name = name;
                m_CurOrderOutputLUT.wrapMode = TextureWrapMode.Clamp;
                m_CurOrderOutputLUT.Create();
            }
            return m_CurOrderOutputLUT;
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
                passData.scatteringScaleR = asset.AtmosphereResources.ScaleRayleigh;
                passData.scatteringScaleM = asset.AtmosphereResources.ScaleMie;
                passData.mieG = asset.AtmosphereResources.MieG;
                passData.csSkyboxLUT = cs;
                passData.multipleScatteringOrder = asset.AtmosphereResources.MultipleScatteringOrder;
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
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaRayleigh, Atmosphere.kRayleighScatteringCoef * data.scatteringScaleR);
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaMie, kMieScatteringCoef * data.scatteringScaleM);
                            ctx.cmd.SetComputeFloatParam(data.csSkyboxLUT, AtmosphereShaderParameters._MieG, data.mieG);
                            ctx.cmd.SetComputeTextureParam(data.csSkyboxLUT, data.kernelSkyboxLUT, AtmosphereShaderParameters._SkyboxLUT, data.skyboxLUT);
                            ctx.cmd.DispatchCompute(data.csSkyboxLUT, data.kernelSkyboxLUT, _skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z);
                        }
                    }
                    );

                return passData;
            }
        }


        public SkyboxLUTPassData PrecomputeMultipleScatteringLUT(RenderGraph renderGraph, ComputeShader cs, RenderTexture skyboxLUT, int multipleScatteringOrder)
        {
            using (var builder = renderGraph.AddRenderPass<SkyboxLUTPassData>("Precompute Multiple Scattering",
                out var passData, new ProfilingSampler("Precompute Multiple Scattering Profiler")))
            {
                passData.skyboxLUT = skyboxLUT;
                passData.csSkyboxLUT = cs;
                passData.multipleScatteringOrder = multipleScatteringOrder;

                if (passData.kernelMultipleSkyboxLUT == -1)
                {
                    passData.kernelMultipleSkyboxLUT = cs.FindKernel("PrecomputeKOrderScattering");
                    passData.tmpMultipleScatteringOrderOutput = CreateMultipleScatteringLUT("TmpMultipleScatteringLUT");
                }
                passData.preOrderScatteringLUT = skyboxLUT;
                if (passData.multipleScatteringOrder % 2 != 0)
                {
                    passData.skyboxLUT = passData.tmpMultipleScatteringOrderOutput;
                }
                m_SkyboxLUT = passData.skyboxLUT;

                builder.SetRenderFunc(
                   (SkyboxLUTPassData data, RenderGraphContext ctx) =>
                   {
                       if (data.multipleScatteringOrder > 0)
                       {
                           RenderTexture preOrder = data.preOrderScatteringLUT;
                           RenderTexture curOrderOutput = data.tmpMultipleScatteringOrderOutput;

                           for (int i = 0; i < data.multipleScatteringOrder; ++i)
                           {
                               ctx.cmd.SetComputeTextureParam(data.csSkyboxLUT, data.kernelMultipleSkyboxLUT,
                                   AtmosphereShaderParameters._PreOrderScatteringLUT, preOrder);
                               ctx.cmd.SetComputeTextureParam(data.csSkyboxLUT, data.kernelMultipleSkyboxLUT,
                                   AtmosphereShaderParameters._OutScatteringLUT, curOrderOutput);

                               ctx.cmd.DispatchCompute(data.csSkyboxLUT, data.kernelMultipleSkyboxLUT, _skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z);
                               RenderTexture tmp = preOrder;
                               preOrder = curOrderOutput;
                               curOrderOutput = tmp;
                           }

                           
                       }
                   });
                return passData;
            }
        }

        private RenderTexture CreateLUTRenderTexture(string name)
        {
            RenderTexture lut = RenderTexture.GetTemporary(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);//new RenderTexture(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            lut.dimension = TextureDimension.Tex3D;
            lut.volumeDepth = _skyboxLUTSize.z;
            lut.enableRandomWrite = true;
            lut.name = name;
            lut.wrapMode = TextureWrapMode.Clamp;
            lut.Create();
            return lut;
        }

        public Texture3D PrecomputeSkyboxLUT(AtmosphereResources atmosphereResources)
        {
            if (atmosphereResources == null)
                return null;

            if (atmosphereResources.PrecomputeScattering == null)
            {
                return null;
            }

            ComputeShader precomputeScattering = atmosphereResources.PrecomputeScattering;
            int kSkyboxLUT = precomputeScattering.FindKernel("PrecomputeScattering");
            if (kSkyboxLUT == -1)
                return null;

            InitShaderParameters();
            RenderTexture skyboxLUT = RenderTexture.GetTemporary(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);//new RenderTexture(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            //m_SkyboxLUT.isVolume
            skyboxLUT.dimension = TextureDimension.Tex3D;
            skyboxLUT.volumeDepth = _skyboxLUTSize.z;
            skyboxLUT.autoGenerateMips = false;
            skyboxLUT.enableRandomWrite = true;
            skyboxLUT.name = "SkyboxLUT";
            skyboxLUT.wrapMode = TextureWrapMode.Clamp;
            skyboxLUT.Create();

            Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f;
            Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f;
            precomputeScattering.SetFloat(AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
            precomputeScattering.SetFloat(AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
            precomputeScattering.SetVector(AtmosphereShaderParameters._BetaRayleigh, rayleightScatteringCoef);
            precomputeScattering.SetVector(AtmosphereShaderParameters._BetaMie, mieScatteringCoef);
            precomputeScattering.SetFloat(AtmosphereShaderParameters._MieG, atmosphereResources.MieG);
            precomputeScattering.SetTexture(kSkyboxLUT, AtmosphereShaderParameters._SkyboxLUT, skyboxLUT);
            precomputeScattering.Dispatch(kSkyboxLUT, _skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z);
            RenderTexture finalOutput = skyboxLUT;

            RenderTexture preOrder = null;
            RenderTexture curOrderOutput = null;
            if (atmosphereResources.MultipleScatteringOrder > 0)
            {
                int kPrecomputeMultipleLUT = precomputeScattering.FindKernel("PrecomputeKOrderScattering");
                //multiple scattering precompute
                preOrder = CreateLUTRenderTexture("PreOrderLUT");
                Graphics.CopyTexture(skyboxLUT, preOrder);
                curOrderOutput = CreateLUTRenderTexture("CurOrderLUT");

                for (int i = 0; i < atmosphereResources.MultipleScatteringOrder; ++i)
                {
                    precomputeScattering.SetTexture(kPrecomputeMultipleLUT,
                                   AtmosphereShaderParameters._PreOrderScatteringLUT, preOrder);
                    precomputeScattering.SetTexture(kPrecomputeMultipleLUT,
                        AtmosphereShaderParameters._OutScatteringLUT, curOrderOutput);

                    precomputeScattering.Dispatch(kPrecomputeMultipleLUT, _skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z);
                    finalOutput = curOrderOutput;
                    RenderTexture tmp = preOrder;
                    preOrder = curOrderOutput;
                    curOrderOutput = tmp;
                }
            }

            Texture3D texture3D = new Texture3D(_skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z, TextureFormat.RGBAHalf, false);
            texture3D.wrapMode = TextureWrapMode.Clamp;
            texture3D.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < _skyboxLUTSize.z; ++i)
            {
                Graphics.CopyTexture(finalOutput, i, 0, texture3D, i, 0);
            }

            RenderTexture.ReleaseTemporary(skyboxLUT);
            if (preOrder != null)
            {
                RenderTexture.ReleaseTemporary(preOrder);
            }
            if (curOrderOutput != null)
            {
                RenderTexture.ReleaseTemporary(curOrderOutput);
            }

            return texture3D;
        }
    }

    
}
