using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        //public float scatteringScaleR;
        //public float scatteringScaleM;
        public Vector4 RayleighScatteringCoef;
        public Vector4 MieScatteringCoef;
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
        //public static Vector4 kRayleighScatteringCoef = new Vector4(6.55f, 17.3f, 23.0f, 0) * 0.000001f;
        //public static Vector4 kMieScatteringCoef = new Vector4(2.0f, 2.0f, 2.0f, 0) * 0.00001f;
        private RenderTexture m_SkyboxLUT;
        private int kernelSkyboxLUT = -1;
        private RenderTexture m_PreOrderInputLUT;
        private RenderTexture m_CurOrderOutputLUT;
        int kernelMultipleSkyboxLUT = -1;
        private bool m_MultipleScattering = false;
        Vector3Int _skyboxLUTSize = new Vector3Int(32, 64, 32);
        public Vector4[] m_bakeSHSamples;
        const int SHSampleCount = 128;

        public class AtmosphereShaderParameters
        {
            public static int _AtmosphereHeight;
            public static int _EarthRadius;
            public static int _RayleighHeightScale;
            public static int _MieHeightScale;
            public static int _BetaRayleigh;
            public static int _BetaMie;
            public static int _MieG;
            public static int _RunderSun;
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
            AtmosphereShaderParameters._RunderSun = Shader.PropertyToID("_RunderSun");
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
                passData.RayleighScatteringCoef = asset.AtmosphereResources.ScatteringCoefficientRayleigh * asset.AtmosphereResources.ScaleRayleigh;
                passData.MieScatteringCoef = asset.AtmosphereResources.ScatteringCoefficientMie * asset.AtmosphereResources.ScaleMie;
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
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaRayleigh, data.RayleighScatteringCoef);
                            ctx.cmd.SetComputeVectorParam(data.csSkyboxLUT, AtmosphereShaderParameters._BetaMie, data.MieScatteringCoef);
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

            Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
            Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;
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

        public void InitSHBakeSamples()
        {
            int thetaCount = 16;
            int phiCount = 32;
            int totalSample = thetaCount * phiCount;
            m_bakeSHSamples = new Vector4[totalSample];
            int index = 0;
            for (int phi = 0; phi < phiCount; ++phi)
            {
                for (int theta = 0; theta < thetaCount; ++theta)
                {
                    float randomU = Random.Range(0.0f, 1.0f);
                    float randomV = Random.Range(0.0f, 1.0f);
                    float thetaAngle = ((float)theta + randomU) / thetaCount * Mathf.PI;
                    float phiAngle = ((float)phi + randomV) / phiCount * Mathf.PI * 2.0f;
                    float sinTheta = Mathf.Sin(thetaAngle);
                    float sinPhi = Mathf.Sin(phiAngle);
                    float cosPhi = Mathf.Cos(phiAngle);
                    float cosTheta = Mathf.Cos(thetaAngle);
                    m_bakeSHSamples[index++] = new Vector4(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi, 0);
                }
            }
        }

        public class AtmosphereSHSetting
        {
            public ComputeBuffer m_BakeSamples;
            public ComputeBuffer m_SHCoefficientsArray;
            public ComputeBuffer m_SHCoefficientsGroupSumArray;
            public ComputeBuffer m_FinalProjSH;
            public static int THREAD_NUM_PER_GROUP = 128;
            public static bool DIRECT_BAKING = false;

        }

        public class ProjSHShaderParameters
        {
            public static int _SHCoefficients;
            public static int _BakeSamples;
            public static int _MainLightPosition;
            public static int _MainLightIntensity;
            public static int _InputSHCoefficients;
            public static int _ArrayLengthPerThreadGroup;
            public static int _SHCoefficientsGroupSumArray;
            public static int _SHCoefficientsGroupSumArrayInput;
            public static int _FinalProjSH;
            public static int _fC0to3;
            public static int _fC4;

            public static void InitShaderParameters()
            {
                _SHCoefficients = Shader.PropertyToID("_SHCoefficients");
                _BakeSamples = Shader.PropertyToID("_BakeSamples");
                _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
                _MainLightIntensity = Shader.PropertyToID("_MainLightIntensity");
                _InputSHCoefficients = Shader.PropertyToID("_InputSHCoefficients");
                _SHCoefficientsGroupSumArray = Shader.PropertyToID("_SHCoefficientsGroupSumArray");
                _SHCoefficientsGroupSumArrayInput = Shader.PropertyToID("_SHCoefficientsGroupSumArrayInput");
                _ArrayLengthPerThreadGroup = Shader.PropertyToID("_ArrayLengthPerThreadGroup");
                _FinalProjSH = Shader.PropertyToID("_FinalProjSH");
                _fC0to3 = Shader.PropertyToID("_fC0to3");
                _fC4 = Shader.PropertyToID("_fC4");
            }
        }

        struct GPUAmbientSHCoefL2
        {
            Vector3 c0;
            Vector3 c1;
            Vector3 c2;
            Vector3 c3;
            Vector3 c4;
            Vector3 c5;
            Vector3 c6;
            Vector3 c7;
            Vector3 c8;
            float pack;
        }

        struct GPUPolynomialSHL2
        {
            public Vector4 SHAr;
            public Vector4 SHAg;
            public Vector4 SHAb;
            public Vector4 SHBr;
            public Vector4 SHBg;
            public Vector4 SHBb;
            public Vector4 SHC;
        }

        AtmosphereSHSetting m_skySHSetting = null;

        public void ClearSamples()
        {
            m_bakeSHSamples = null;
        }

        public void BakeSkyToSHAmbient(AtmosphereResources atmosphereResources, Light sunLight)
        {
            if (atmosphereResources.ProjAtmosphereToSH == null)
                return;

            if (atmosphereResources.SkyboxLUT == null)
                return;

            ProjSHShaderParameters.InitShaderParameters();

            if (m_bakeSHSamples == null)
            {
                InitSHBakeSamples();
            }

            if (m_skySHSetting == null)
            {
                m_skySHSetting = new AtmosphereSHSetting();
                m_skySHSetting.m_BakeSamples = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Structured);
                m_skySHSetting.m_BakeSamples.SetData(m_bakeSHSamples);
                m_skySHSetting.m_SHCoefficientsArray = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf<GPUAmbientSHCoefL2>());
                m_skySHSetting.m_SHCoefficientsGroupSumArray = new ComputeBuffer(m_bakeSHSamples.Length / AtmosphereSHSetting.THREAD_NUM_PER_GROUP, 
                    Marshal.SizeOf<GPUAmbientSHCoefL2>());
                m_skySHSetting.m_FinalProjSH = new ComputeBuffer(1, Marshal.SizeOf<GPUPolynomialSHL2>(), ComputeBufferType.Structured);
            }

            ComputeShader csProjAtmosphereToSH = atmosphereResources.ProjAtmosphereToSH;

            //set the cs parameters
            Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
            Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;
            csProjAtmosphereToSH.SetFloat(AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
            csProjAtmosphereToSH.SetFloat(AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
            csProjAtmosphereToSH.SetVector(AtmosphereShaderParameters._BetaRayleigh, rayleightScatteringCoef);
            csProjAtmosphereToSH.SetVector(AtmosphereShaderParameters._BetaMie, mieScatteringCoef);
            csProjAtmosphereToSH.SetFloat(AtmosphereShaderParameters._MieG, atmosphereResources.MieG);
            Vector4 mainLightPosition = -sunLight.transform.localToWorldMatrix.GetColumn(2);
            mainLightPosition.w = 0;
            csProjAtmosphereToSH.SetVector(ProjSHShaderParameters._MainLightPosition, mainLightPosition);
            csProjAtmosphereToSH.SetFloat(ProjSHShaderParameters._MainLightIntensity, sunLight.intensity);

            float sqrtPI = Mathf.Sqrt(Mathf.PI);
            float fC0 = (1.0f / (2.0f * sqrtPI));
            float fC1 = (Mathf.Sqrt(3.0f) / (3.0f * sqrtPI));
            float fC2 = (Mathf.Sqrt(15.0f) / (8.0f * sqrtPI));
            float fC3 = (Mathf.Sqrt(5.0f) / (16.0f * sqrtPI));
            float fC4 = (0.5f * fC2);

            csProjAtmosphereToSH.SetVector(ProjSHShaderParameters._fC0to3, new Vector4(fC0, fC1, fC2, fC3));
            csProjAtmosphereToSH.SetFloat(ProjSHShaderParameters._fC4, fC4);

            if (AtmosphereSHSetting.DIRECT_BAKING)
            {
                int kBakeDirect = csProjAtmosphereToSH.FindKernel("BakeSHDirect");
                if (kBakeDirect == -1)
                    return;

                csProjAtmosphereToSH.SetTexture(kBakeDirect, AtmosphereShaderParameters._SkyboxLUT, atmosphereResources.SkyboxLUT);
                csProjAtmosphereToSH.SetBuffer(kBakeDirect, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                csProjAtmosphereToSH.SetBuffer(kBakeDirect, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);

                csProjAtmosphereToSH.Dispatch(kBakeDirect, 1, 1, 1);
                GPUPolynomialSHL2[] SHResults = new GPUPolynomialSHL2[1];
                m_skySHSetting.m_FinalProjSH.GetData(SHResults);
                Shader.SetGlobalVector("_SHAr", SHResults[0].SHAr);
                Shader.SetGlobalVector("_SHAg", SHResults[0].SHAg);
                Shader.SetGlobalVector("_SHAb", SHResults[0].SHAb);
                Shader.SetGlobalVector("_SHBr", SHResults[0].SHBr);
                Shader.SetGlobalVector("_SHBg", SHResults[0].SHBg);
                Shader.SetGlobalVector("_SHBb", SHResults[0].SHBb);
                Shader.SetGlobalVector("_SHC", SHResults[0].SHC);
            }
            else
            {
                int kPreSumSH = csProjAtmosphereToSH.FindKernel("PresumSHCoefficient");
                if (kPreSumSH == -1)
                    return;

                //pass 1 parallen sum the sh coefficients into array.
                csProjAtmosphereToSH.SetTexture(kPreSumSH, AtmosphereShaderParameters._SkyboxLUT, atmosphereResources.SkyboxLUT);
                csProjAtmosphereToSH.SetBuffer(kPreSumSH, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                csProjAtmosphereToSH.SetBuffer(kPreSumSH, ProjSHShaderParameters._SHCoefficients, m_skySHSetting.m_SHCoefficientsArray);

                int groupsNum = m_bakeSHSamples.Length / 128;
                csProjAtmosphereToSH.Dispatch(kPreSumSH, groupsNum, 1, 1);

                //pass 2, sum the last element of sh coefficients to the group array.
                if (groupsNum > 1)
                {
                    int kPreSumSHGroup = csProjAtmosphereToSH.FindKernel("PreSumGroupSH");
                    if (kPreSumSHGroup >= 0)
                    {
                        csProjAtmosphereToSH.SetInt(ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNum);
                        csProjAtmosphereToSH.SetBuffer(kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, m_skySHSetting.m_SHCoefficientsArray);
                        csProjAtmosphereToSH.SetBuffer(kPreSumSHGroup, ProjSHShaderParameters._SHCoefficientsGroupSumArray, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                        csProjAtmosphereToSH.Dispatch(kPreSumSHGroup, 1, 1, 1);
                    }
                }

                //pass 3
                int kBakeSHToTexture = csProjAtmosphereToSH.FindKernel("BakeSHToTexture");
                if (kBakeSHToTexture >= 0)
                {
                    csProjAtmosphereToSH.SetBuffer(kBakeSHToTexture, ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                    csProjAtmosphereToSH.SetBuffer(kBakeSHToTexture, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);
                    csProjAtmosphereToSH.Dispatch(kBakeSHToTexture, 1, 1, 1);

                    GPUPolynomialSHL2[] SHResults = new GPUPolynomialSHL2[1];
                    m_skySHSetting.m_FinalProjSH.GetData(SHResults);
                    Shader.SetGlobalVector("_SHAr", SHResults[0].SHAr);
                    Shader.SetGlobalVector("_SHAg", SHResults[0].SHAg);
                    Shader.SetGlobalVector("_SHAb", SHResults[0].SHAb);
                    Shader.SetGlobalVector("_SHBr", SHResults[0].SHBr);
                    Shader.SetGlobalVector("_SHBg", SHResults[0].SHBg);
                    Shader.SetGlobalVector("_SHBb", SHResults[0].SHBb);
                    Shader.SetGlobalVector("_SHC", SHResults[0].SHC);
                }
            }
        }
            
    }

    
}
