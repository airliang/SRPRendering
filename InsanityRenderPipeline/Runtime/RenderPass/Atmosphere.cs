using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        static Vector3Int _skyboxLUTSize = new Vector3Int(32, 64, 32);
        public Vector4[] m_bakeSHSamples;
        const int SHSampleCount = 128;
        private bool m_bakeCubemap = false;
        public Queue<AsyncGPUReadbackRequest> m_SHBakeReadbacks = new Queue<AsyncGPUReadbackRequest>();
        Vector3 m_sunDirectionLastFrame;

        private static Atmosphere s_instance = null;
        public static Atmosphere Instance
        { get {
                if (s_instance == null)
                {
                    s_instance = new Atmosphere();
                }
                return s_instance; } }


        public bool BakeCubemap
        {
            get { return m_bakeCubemap; }
            set { m_bakeCubemap = value;}
        }

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
            public static int _Cubemap;
            public static int _InputCubemap;
            public static int _OutputCubemap0;
            public static int _OutputCubemap1;
            public static int _OutputCubemap2;
            public static int _OutputCubemap3;
            public static int _OutputCubemap4;
            public static int _OutputCubemap5;
            public static int _ATMOSPHERE_SPECULAR;
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
            AtmosphereShaderParameters._Cubemap = Shader.PropertyToID("_Cubemap");
            AtmosphereShaderParameters._InputCubemap = Shader.PropertyToID("_InputCubemap");
            AtmosphereShaderParameters._OutputCubemap0 = Shader.PropertyToID("_OutputCubemap0");
            AtmosphereShaderParameters._OutputCubemap1 = Shader.PropertyToID("_OutputCubemap1");
            AtmosphereShaderParameters._OutputCubemap2 = Shader.PropertyToID("_OutputCubemap2");
            AtmosphereShaderParameters._OutputCubemap3 = Shader.PropertyToID("_OutputCubemap3");
            AtmosphereShaderParameters._OutputCubemap4 = Shader.PropertyToID("_OutputCubemap4");
            AtmosphereShaderParameters._OutputCubemap5 = Shader.PropertyToID("_OutputCubemap5");
            AtmosphereShaderParameters._ATMOSPHERE_SPECULAR = Shader.PropertyToID("_ATMOSPHERE_SPECULAR");
        }

        public Atmosphere()
        {
            InitShaderParameters();
            ProjSHShaderParameters.InitShaderParameters();
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

                            ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                            ctx.cmd.Clear();
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

                           ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                           ctx.cmd.Clear();
                       }
                   });
                return passData;
            }
        }

        private static RenderTexture CreateLUTRenderTexture(string name)
        {
            RenderTexture lut = RenderTexture.GetTemporary(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            lut.dimension = TextureDimension.Tex3D;
            lut.volumeDepth = _skyboxLUTSize.z;
            lut.enableRandomWrite = true;
            lut.name = name;
            lut.wrapMode = TextureWrapMode.Clamp;
            lut.Create();
            return lut;
        }

        public static Texture PrecomputeSkyboxLUT(AtmosphereResources atmosphereResources)
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

            //RenderTexture skyboxLUT = new RenderTexture(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            RenderTexture skyboxLUT = RenderTexture.GetTemporary(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            skyboxLUT.dimension = TextureDimension.Tex3D;
            skyboxLUT.volumeDepth = _skyboxLUTSize.z;
            skyboxLUT.autoGenerateMips = false;
            skyboxLUT.enableRandomWrite = true;
            skyboxLUT.name = "SkyboxLUT";
            skyboxLUT.wrapMode = TextureWrapMode.Clamp;
            skyboxLUT.Create();
            Vector3Int skyboxLUTSize = new Vector3Int(skyboxLUT.width, skyboxLUT.height, skyboxLUT.volumeDepth);


            Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
            Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;
            precomputeScattering.SetFloat(AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
            precomputeScattering.SetFloat(AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
            precomputeScattering.SetVector(AtmosphereShaderParameters._BetaRayleigh, rayleightScatteringCoef);
            precomputeScattering.SetVector(AtmosphereShaderParameters._BetaMie, mieScatteringCoef);
            precomputeScattering.SetFloat(AtmosphereShaderParameters._MieG, atmosphereResources.MieG);
            precomputeScattering.SetTexture(kSkyboxLUT, AtmosphereShaderParameters._SkyboxLUT, skyboxLUT);
            precomputeScattering.Dispatch(kSkyboxLUT, skyboxLUTSize.x, skyboxLUTSize.y, skyboxLUTSize.z);
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

                    precomputeScattering.Dispatch(kPrecomputeMultipleLUT, skyboxLUTSize.x, skyboxLUTSize.y, skyboxLUTSize.z);
                    finalOutput = curOrderOutput;
                    RenderTexture tmp = preOrder;
                    preOrder = curOrderOutput;
                    curOrderOutput = tmp;
                }
            }

            Texture3D texture3D = new Texture3D(_skyboxLUTSize.x, _skyboxLUTSize.y, _skyboxLUTSize.z, TextureFormat.RGBAHalf, false);
            texture3D.wrapMode = TextureWrapMode.Clamp;
            texture3D.filterMode = FilterMode.Bilinear;
            RenderTexture lastRT = RenderTexture.active;
            Texture2D sliceTex = new Texture2D(_skyboxLUTSize.x, _skyboxLUTSize.y, TextureFormat.RGBAHalf, false);
            for (int i = 0; i < _skyboxLUTSize.z; ++i)
            {
                RenderTexture slice = RenderTexture.GetTemporary(_skyboxLUTSize.x, _skyboxLUTSize.y, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                Graphics.CopyTexture(finalOutput, i, 0, slice, 0, 0);
                RenderTexture.active = slice;
                
                sliceTex.ReadPixels(new Rect(0, 0, _skyboxLUTSize.x, _skyboxLUTSize.y), 0, 0);
                //sliceTex.Apply();
                Graphics.CopyTexture(sliceTex, 0, 0, texture3D, i, 0);
                RenderTexture.ReleaseTemporary(slice);
            }
            
#if UNITY_EDITOR
            Texture2D.DestroyImmediate(sliceTex);
#else   
            Texture2D.Destroy(sliceTex);
#endif

            RenderTexture.active = lastRT;
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
            //uniform sample sphere
            
            m_bakeSHSamples = new Vector4[512];
            for (int i = 0; i < 512; ++i)
            {
                float xi1 = Random.Range(0.0f, 1.0f);
                float xi2 = Random.Range(0.0f, 1.0f);
                float thetaAngle = Mathf.Acos(1.0f - xi1 * 2.0f);
                float phiAngle = Mathf.PI * 2.0f * xi2;
                float sinTheta = Mathf.Sin(thetaAngle);
                float sinPhi = Mathf.Sin(phiAngle);
                float cosPhi = Mathf.Cos(phiAngle);
                float cosTheta = Mathf.Cos(thetaAngle);
                float weight = sinTheta;
                m_bakeSHSamples[i] = new Vector4(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi, weight);
            }
            
        }

        public class AtmosphereSHSetting
        {
            public ComputeBuffer m_BakeSamples;

            public RenderTexture m_SHCoefficientsTexture;
            public RenderTexture m_SHCoefficientsGroupSumTexture;

            public ComputeBuffer m_SHCoefficientsArray;
            public ComputeBuffer m_SHCoefficientsGroupSumArray;

            public ComputeBuffer m_FinalProjSH;
            public static int THREAD_NUM_PER_GROUP = 128;
            public static bool DIRECT_BAKING = false;
            public static bool OPTIMIZE_BAKING = true;

            public void Release()
            {
                if (m_BakeSamples != null)
                {
                    m_BakeSamples.Release();
                    m_BakeSamples = null;
                }

                if (m_SHCoefficientsTexture != null)
                {
                    m_SHCoefficientsTexture.Release();
                    m_SHCoefficientsTexture = null;
                }

                if (m_SHCoefficientsGroupSumTexture != null)
                {
                    m_SHCoefficientsGroupSumTexture.Release();
                    m_SHCoefficientsGroupSumTexture = null;
                }

                if (m_SHCoefficientsArray != null)
                {
                    m_SHCoefficientsArray.Release();
                    m_SHCoefficientsArray = null;
                }

                if (m_SHCoefficientsGroupSumArray != null)
                {
                    m_SHCoefficientsGroupSumArray.Release();
                    m_SHCoefficientsGroupSumArray = null;
                }
            }
        }

        public class ProjSHShaderParameters
        {
            public static int _SHCoefficients;
            public static int _BakeSamples;
            public static int _MainLightPosition;
            public static int _MainLightIntensity;
            public static int _InputSHCoefficients;
            public static int _ArrayLengthPerThreadGroup;
            public static int _GroupsNumPowOf2;
            public static int _SHCoefficientsGroupSumArray;
            public static int _SHCoefficientsGroupSumArrayInput;
            public static int _FinalProjSH;
            //public static int _fC0to3;
            //public static int _fC4;
            public static int _SHAr;
            public static int _SHAg;
            public static int _SHAb;
            public static int _SHBr;
            public static int _SHBg;
            public static int _SHBb;
            public static int _SHC;

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
                _GroupsNumPowOf2 = Shader.PropertyToID("_GroupsNumPowOf2");
                _FinalProjSH = Shader.PropertyToID("_FinalProjSH");
                //_fC0to3 = Shader.PropertyToID("_fC0to3");
                //_fC4 = Shader.PropertyToID("_fC4");
                _SHAr = Shader.PropertyToID("_SHAr");
                _SHAg = Shader.PropertyToID("_SHAg");
                _SHAb = Shader.PropertyToID("_SHAb");
                _SHBr = Shader.PropertyToID("_SHBr");
                _SHBg = Shader.PropertyToID("_SHBg");
                _SHBb = Shader.PropertyToID("_SHBb");
                _SHC = Shader.PropertyToID("_SHC");
            }
        }

        struct GPUAmbientSHCoefL2
        {
            public Vector3 c0;
            public Vector3 c1;
            public Vector3 c2;
            public Vector3 c3;
            public Vector3 c4;
            public Vector3 c5;
            public Vector3 c6;
            public Vector3 c7;
            public Vector3 c8;
            public float pack;

            public static GPUAmbientSHCoefL2 operator *(GPUAmbientSHCoefL2 lhs, float rhs)
            {
                GPUAmbientSHCoefL2 result = default(GPUAmbientSHCoefL2);
                result.c0 = lhs.c0 * rhs;
                result.c1 = lhs.c1 * rhs;
                result.c2 = lhs.c2 * rhs;
                result.c3 = lhs.c3 * rhs;
                result.c4 = lhs.c4 * rhs;
                result.c5 = lhs.c5 * rhs;
                result.c6 = lhs.c6 * rhs;
                result.c7 = lhs.c7 * rhs;
                result.c8 = lhs.c8 * rhs;
                return result;
            }

            public static GPUAmbientSHCoefL2 operator *(float lhs, GPUAmbientSHCoefL2 rhs)
            {
                GPUAmbientSHCoefL2 result = default(GPUAmbientSHCoefL2);
                result.c0 = rhs.c0 * lhs;
                result.c1 = rhs.c1 * lhs;
                result.c2 = rhs.c2 * lhs;
                result.c3 = rhs.c3 * lhs;
                result.c4 = rhs.c4 * lhs;
                result.c5 = rhs.c5 * lhs;
                result.c6 = rhs.c6 * lhs;
                result.c7 = rhs.c7 * lhs;
                result.c8 = rhs.c8 * lhs;
                return result;
            }
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

        private GPUPolynomialSHL2 GetEnvmapSHPolyCoef(ref GPUAmbientSHCoefL2 sh)
        {
            GPUPolynomialSHL2 polyShCoef = new GPUPolynomialSHL2();
            float sqrtPI = Mathf.Sqrt(Mathf.PI);
            float fC0 = (1.0f / (2.0f * sqrtPI));
            float fC1 = (Mathf.Sqrt(3.0f) / (3.0f * sqrtPI));
            float fC2 = (Mathf.Sqrt(15.0f) / (8.0f * sqrtPI));
            float fC3 = (Mathf.Sqrt(5.0f) / (16.0f * sqrtPI));
            float fC4 = (0.5f * fC2);
            polyShCoef.SHAr = new Vector4(-fC1 * sh.c3.x, -fC1 * sh.c1.x, fC1 * sh.c2.x, fC0 * sh.c0.x - fC3 * sh.c6.x);
            polyShCoef.SHAg = new Vector4(-fC1 * sh.c3.y, -fC1 * sh.c1.y, fC1 * sh.c2.y, fC0 * sh.c0.y - fC3 * sh.c6.y);
            polyShCoef.SHAb = new Vector4(-fC1 * sh.c3.z, -fC1 * sh.c1.z, fC1 * sh.c2.z, fC0 * sh.c0.z - fC3 * sh.c6.z);

            polyShCoef.SHBr = new Vector4(fC2 * sh.c4.x, -fC2 * sh.c5.x, 3.0f * fC2 * sh.c6.x, -fC2 * sh.c7.x);
            polyShCoef.SHBg = new Vector4(fC2 * sh.c4.y, -fC2 * sh.c5.y, 3.0f * fC2 * sh.c6.y, -fC2 * sh.c7.y);
            polyShCoef.SHBb = new Vector4(fC2 * sh.c4.z, -fC2 * sh.c5.z, 3.0f * fC2 * sh.c6.z, -fC2 * sh.c7.z);

            polyShCoef.SHC = new Vector4(fC4 * sh.c8.x, fC4 * sh.c8.y, fC4 * sh.c8.z, 1.0f);

            return polyShCoef;
        }

        AtmosphereSHSetting m_skySHSetting = null;

        GPUAmbientSHCoefL2[] m_finalPolySHL2 = new GPUAmbientSHCoefL2[1];

        public void ClearSamples()
        {
            m_bakeSHSamples = null;
        }

        void GetAmbientSHData(ComputeBuffer ambientSHBuffer)
        {
            AsyncGPUReadbackRequest singleReadBack = AsyncGPUReadback.Request(ambientSHBuffer);
            m_SHBakeReadbacks.Enqueue(singleReadBack);
        }

        const string m_BakeSHProfilerTag = "Baking Atmophere Scattering to SHs";
        ProfilingSampler m_BakeSHProfilingSampler = new ProfilingSampler(m_BakeSHProfilerTag);

        class BakeAtmosphereToSHPassData
        {
            public ComputeShader csProjAtmosphereToSH;
            public int kDirectBake = -1;
            public int kPreSumSH = -1;
            public int kPreSumSHGroup = -1;
            public int kBakeSHToTexture = -1;
            public Texture SkyLUT;
            public RenderTexture m_SHCoefficientsTexture;
            public RenderTexture m_SHCoefficientsGroupSumTexture;
            public ComputeBuffer m_FinalProjSH;
            public ComputeBuffer m_BakeSamples;
            //public ComputeBuffer m_SHCoefficientsGroupSumArray;
            public Vector4 rayleightScatteringCoef;
            public Vector4 mieScatteringCoef;
            public float atmosphereHeight;
            public float earthRadius;
            public float mieG;
            public Vector4 mainLightPosition;
            public float mainLightIntensity;
            public int bakeSamplesCount;
        }

        public void BakeSkyToSHAmbient(RenderGraph renderGraph, ref ScriptableRenderContext context, AtmosphereResources atmosphereResources, Light sunLight)
        {
            if (atmosphereResources.ProjAtmosphereToSH == null)
                return;

            if (atmosphereResources.SkyboxLUT == null)
                return;

            if (m_bakeSHSamples == null)
            {
                InitSHBakeSamples();
            }

            if (m_skySHSetting == null)
            {
                m_skySHSetting = new AtmosphereSHSetting();
            }

            if (m_skySHSetting.m_BakeSamples == null)
            {
                m_skySHSetting.m_BakeSamples = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Structured);
                m_skySHSetting.m_BakeSamples.SetData(m_bakeSHSamples);
            }

            if (AtmosphereSHSetting.OPTIMIZE_BAKING)
            {
                if (m_skySHSetting.m_SHCoefficientsTexture == null)
                {
                    m_skySHSetting.m_SHCoefficientsTexture = new RenderTexture(m_bakeSHSamples.Length, 9, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    m_skySHSetting.m_SHCoefficientsTexture.enableRandomWrite = true;
                    m_skySHSetting.m_SHCoefficientsTexture.Create();
                }

                if (m_skySHSetting.m_SHCoefficientsGroupSumTexture == null)
                {
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture = new RenderTexture(m_bakeSHSamples.Length / AtmosphereSHSetting.THREAD_NUM_PER_GROUP,
                        9, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture.enableRandomWrite = true;
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture.Create();
                }
            }
            else
            {
                if (m_skySHSetting.m_SHCoefficientsArray == null)
                    m_skySHSetting.m_SHCoefficientsArray = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf<GPUAmbientSHCoefL2>());
                if (m_skySHSetting.m_SHCoefficientsGroupSumArray == null)
                    m_skySHSetting.m_SHCoefficientsGroupSumArray = new ComputeBuffer(m_bakeSHSamples.Length / AtmosphereSHSetting.THREAD_NUM_PER_GROUP,
                        Marshal.SizeOf<GPUAmbientSHCoefL2>());
            }

            if (m_skySHSetting.m_FinalProjSH == null)
                m_skySHSetting.m_FinalProjSH = new ComputeBuffer(1, Marshal.SizeOf<GPUAmbientSHCoefL2>(), ComputeBufferType.Structured);


            /*
            ComputeShader csProjAtmosphereToSH = atmosphereResources.ProjAtmosphereToSH;
            CommandBuffer cmd = CommandBufferPool.Get(m_BakeSHProfilerTag);
            
            using (new ProfilingScope(cmd, m_BakeSHProfilingSampler))
            {
                LocalKeyword bakeCubemap = new LocalKeyword(csProjAtmosphereToSH, "_BAKE_CUBEMAP");
                cmd.DisableKeyword(csProjAtmosphereToSH, bakeCubemap);
                //set the cs parameters
                Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
                Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;

                cmd.SetComputeFloatParam(csProjAtmosphereToSH, AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
                cmd.SetComputeFloatParam(csProjAtmosphereToSH, AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
                cmd.SetComputeVectorParam(csProjAtmosphereToSH, AtmosphereShaderParameters._BetaRayleigh, rayleightScatteringCoef);
                cmd.SetComputeVectorParam(csProjAtmosphereToSH, AtmosphereShaderParameters._BetaMie, mieScatteringCoef);
                cmd.SetComputeFloatParam(csProjAtmosphereToSH, AtmosphereShaderParameters._MieG, atmosphereResources.MieG);
                Vector4 mainLightPosition = -sunLight.transform.localToWorldMatrix.GetColumn(2);
                mainLightPosition.w = 0;

                cmd.SetComputeVectorParam(csProjAtmosphereToSH, ProjSHShaderParameters._MainLightPosition, mainLightPosition);
                cmd.SetComputeFloatParam(csProjAtmosphereToSH, ProjSHShaderParameters._MainLightIntensity, sunLight.intensity);

                float sqrtPI = Mathf.Sqrt(Mathf.PI);
                float fC0 = (1.0f / (2.0f * sqrtPI));
                float fC1 = (Mathf.Sqrt(3.0f) / (3.0f * sqrtPI));
                float fC2 = (Mathf.Sqrt(15.0f) / (8.0f * sqrtPI));
                float fC3 = (Mathf.Sqrt(5.0f) / (16.0f * sqrtPI));
                float fC4 = (0.5f * fC2);


                if (AtmosphereSHSetting.DIRECT_BAKING)
                {
                    int kBakeDirect = csProjAtmosphereToSH.FindKernel("BakeSHDirect");
                    if (kBakeDirect == -1)
                        return;


                    cmd.SetComputeTextureParam(csProjAtmosphereToSH, kBakeDirect, AtmosphereShaderParameters._SkyboxLUT, atmosphereResources.SkyboxLUT);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeDirect, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeDirect, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);
                    cmd.DispatchCompute(csProjAtmosphereToSH, kBakeDirect, 1, 1, 1);
                    GetAmbientSHData(m_skySHSetting.m_FinalProjSH);
                }
                else
                {
                    int kPreSumSH = csProjAtmosphereToSH.FindKernel("PresumSHCoefficient");
                    if (kPreSumSH == -1)
                        return;

                    //pass 1 parallen sum the sh coefficients into array.
                    cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSH, AtmosphereShaderParameters._SkyboxLUT, atmosphereResources.SkyboxLUT);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                    int groupsNumX = m_bakeSHSamples.Length / 128;
                    if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                    {

                        cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._SHCoefficients, m_skySHSetting.m_SHCoefficientsTexture);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSH, groupsNumX, 9, 1);
                    }
                    else
                    {

                        cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._SHCoefficients, m_skySHSetting.m_SHCoefficientsArray);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSH, groupsNumX, 1, 1);
                    }

                    //pass 2, sum the last element of sh coefficients to the group array.
                    if (groupsNumX > 1)
                    {
                        int kPreSumSHGroup = csProjAtmosphereToSH.FindKernel("PreSumGroupSH");
                        if (kPreSumSHGroup >= 0)
                        {
                            //csProjAtmosphereToSH.SetInt(ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNumX);
                            cmd.SetComputeIntParam(csProjAtmosphereToSH, ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNumX);
                            cmd.SetComputeIntParam(csProjAtmosphereToSH, ProjSHShaderParameters._GroupsNumPowOf2, Mathf.NextPowerOfTwo(groupsNumX));
                            if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                            {

                                cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, m_skySHSetting.m_SHCoefficientsTexture);
                                cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSHGroup,
                                    ProjSHShaderParameters._SHCoefficientsGroupSumArray, m_skySHSetting.m_SHCoefficientsGroupSumTexture);
                                cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSHGroup, 1, 9, 1);
                            }
                            else
                            {

                                cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, m_skySHSetting.m_SHCoefficientsArray);
                                cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSHGroup,
                                    ProjSHShaderParameters._SHCoefficientsGroupSumArray, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                                cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSHGroup, 1, 1, 1);
                            }


                        }
                    }

                    //pass 3
                    int kBakeSHToTexture = csProjAtmosphereToSH.FindKernel("BakeSHToTexture");
                    if (kBakeSHToTexture >= 0)
                    {
                        if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                        {
                            cmd.SetComputeTextureParam(csProjAtmosphereToSH, kBakeSHToTexture,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, m_skySHSetting.m_SHCoefficientsGroupSumTexture);
                        }
                        else
                        {

                            cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeSHToTexture,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                        }

                        cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeSHToTexture, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kBakeSHToTexture, 1, 1, 1);

                        GetAmbientSHData(m_skySHSetting.m_FinalProjSH);
                    }
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            */

            using (var builder = renderGraph.AddRenderPass<BakeAtmosphereToSHPassData>("Bake Atmosphere Scattering to SH Pass",
                out var passData, new ProfilingSampler("Bake Atmosphere Scattering to SH Pass Profiler")))
            {
                passData.csProjAtmosphereToSH = atmosphereResources.ProjAtmosphereToSH;
                passData.kPreSumSH = atmosphereResources.ProjAtmosphereToSH.FindKernel("PresumSHCoefficient");
                passData.kPreSumSHGroup = atmosphereResources.ProjAtmosphereToSH.FindKernel("PreSumGroupSH");
                passData.kBakeSHToTexture = atmosphereResources.ProjAtmosphereToSH.FindKernel("BakeSHToTexture");
                passData.SkyLUT = atmosphereResources.SkyboxLUT;
                passData.m_SHCoefficientsTexture = m_skySHSetting.m_SHCoefficientsTexture;
                passData.m_SHCoefficientsGroupSumTexture = m_skySHSetting.m_SHCoefficientsGroupSumTexture;
                passData.m_FinalProjSH = m_skySHSetting.m_FinalProjSH;
                passData.m_BakeSamples = m_skySHSetting.m_BakeSamples;
                passData.rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
                passData.mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;
                passData.mieG = atmosphereResources.MieG;
                passData.earthRadius = Atmosphere.kAtmosphereHeight;
                passData.atmosphereHeight = Atmosphere.kAtmosphereHeight;
                passData.mainLightPosition = -sunLight.transform.localToWorldMatrix.GetColumn(2);
                passData.mainLightPosition.w = 0;
                passData.mainLightIntensity = sunLight.intensity;
                passData.bakeSamplesCount = m_bakeSHSamples.Length;

                builder.SetRenderFunc((BakeAtmosphereToSHPassData data, RenderGraphContext context) =>
                {
                    LocalKeyword bakeCubemap = new LocalKeyword(data.csProjAtmosphereToSH, "_BAKE_CUBEMAP");
                    context.cmd.DisableKeyword(data.csProjAtmosphereToSH, bakeCubemap);

                    context.cmd.SetComputeFloatParam(data.csProjAtmosphereToSH, AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
                    context.cmd.SetComputeFloatParam(data.csProjAtmosphereToSH, AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
                    context.cmd.SetComputeVectorParam(data.csProjAtmosphereToSH, AtmosphereShaderParameters._BetaRayleigh, data.rayleightScatteringCoef);
                    context.cmd.SetComputeVectorParam(data.csProjAtmosphereToSH, AtmosphereShaderParameters._BetaMie, data.mieScatteringCoef);
                    context.cmd.SetComputeFloatParam(data.csProjAtmosphereToSH, AtmosphereShaderParameters._MieG, data.mieG);

                    context.cmd.SetComputeVectorParam(data.csProjAtmosphereToSH, ProjSHShaderParameters._MainLightPosition, data.mainLightPosition);
                    context.cmd.SetComputeFloatParam(data.csProjAtmosphereToSH, ProjSHShaderParameters._MainLightIntensity, data.mainLightIntensity);

                    //pass 1 parallen sum the sh coefficients into array.
                    context.cmd.SetComputeTextureParam(data.csProjAtmosphereToSH, data.kPreSumSH, AtmosphereShaderParameters._SkyboxLUT, data.SkyLUT);
                    context.cmd.SetComputeBufferParam(data.csProjAtmosphereToSH, data.kPreSumSH, ProjSHShaderParameters._BakeSamples, data.m_BakeSamples);
                    int groupsNumX = data.bakeSamplesCount / 128;

                    context.cmd.SetComputeTextureParam(data.csProjAtmosphereToSH, data.kPreSumSH, ProjSHShaderParameters._SHCoefficients, data.m_SHCoefficientsTexture);
                    context.cmd.DispatchCompute(data.csProjAtmosphereToSH, data.kPreSumSH, groupsNumX, 9, 1);


                    //pass 2, sum the last element of sh coefficients to the group array.
                    if (groupsNumX > 1)
                    {
                        if (data.kPreSumSHGroup >= 0)
                        {
                            context.cmd.SetComputeIntParam(data.csProjAtmosphereToSH, ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNumX);
                            context.cmd.SetComputeIntParam(data.csProjAtmosphereToSH, ProjSHShaderParameters._GroupsNumPowOf2, Mathf.NextPowerOfTwo(groupsNumX));

                            context.cmd.SetComputeTextureParam(data.csProjAtmosphereToSH, data.kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, data.m_SHCoefficientsTexture);
                            context.cmd.SetComputeTextureParam(data.csProjAtmosphereToSH, data.kPreSumSHGroup,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArray, data.m_SHCoefficientsGroupSumTexture);
                            context.cmd.DispatchCompute(data.csProjAtmosphereToSH, data.kPreSumSHGroup, 1, 9, 1);
                        }
                    }

                    //pass 3
                    if (data.kBakeSHToTexture >= 0)
                    {
                        context.cmd.SetComputeTextureParam(data.csProjAtmosphereToSH, data.kBakeSHToTexture,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, data.m_SHCoefficientsGroupSumTexture);


                        context.cmd.SetComputeBufferParam(data.csProjAtmosphereToSH, data.kBakeSHToTexture, ProjSHShaderParameters._FinalProjSH, data.m_FinalProjSH);
                        context.cmd.DispatchCompute(data.csProjAtmosphereToSH, data.kBakeSHToTexture, 1, 1, 1);

                        
                    }

                    context.renderContext.ExecuteCommandBuffer(context.cmd);
                    context.cmd.Clear();
                });

                GetAmbientSHData(m_skySHSetting.m_FinalProjSH);
            }

            BakeAtmosphereToCubemap(renderGraph, ref context, atmosphereResources, sunLight);
        }

        const int m_CubemapSize = 32;
        Cubemap m_AtmosphereCubemap;   //irradiance
        Cubemap m_AtmospherePrefilterSpecular;   //prefilter specular cubemap
        TextureHandle[] m_AtmosphereCubeArrays = new TextureHandle[(int)Mathf.Log(m_CubemapSize, 2) + 1];
        

        TextureHandle CreateAtmosphereFaceTextureArray(RenderGraph renderGraph, int size)
        {
            TextureDesc textureDesc = new TextureDesc(size, size);
            textureDesc.slices = 6;
            textureDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            textureDesc.useMipMap = false;
            textureDesc.enableRandomWrite = true;
            textureDesc.autoGenerateMips = false;
            textureDesc.wrapMode = TextureWrapMode.Clamp;
            textureDesc.filterMode = FilterMode.Bilinear;
            textureDesc.dimension = TextureDimension.Tex2DArray;


            int textureIndex = (int)Mathf.Log((float)m_CubemapSize / size, 2.0f);
            renderGraph.CreateTextureIfInvalid(textureDesc, ref m_AtmosphereCubeArrays[textureIndex]);
            return m_AtmosphereCubeArrays[textureIndex];
        }

        public class BakeAtmosphereToCubeData
        {
            public ComputeShader m_ComputeShader;
            public int kBakeAtmosphereToCube = -1;
            public Texture SkyLUT;
            public TextureHandle m_FaceTextureArray;
            public Vector4 mainLightPosition;
            public float mainLightIntensity;
            public int cubeMapWidth;
            public int cubeMapHeight;
            public Cubemap m_Cubemap;
        }

        public class DownSampleCubeData
        {
            public ComputeShader m_ComputeShader;
            public int kDownSampleCube = -1;
            public Cubemap m_InputCubemap;
            public TextureHandle m_OutputFaceTextureArray;
            public int m_OutputTextureSize;
            public int m_MipLevel;
        }

        public class PrefilterSpecularPassData
        {
            public ComputeShader m_ComputeShader;
            public int kPrefilterSpecular = -1;
            public Cubemap m_InputCubemap;
            public TextureHandle m_OutputFaceTextureArray0;
            public TextureHandle m_OutputFaceTextureArray1;
            public TextureHandle m_OutputFaceTextureArray2;
            public TextureHandle m_OutputFaceTextureArray3;
            public TextureHandle m_OutputFaceTextureArray4;
            public TextureHandle m_OutputFaceTextureArray5;
            public Cubemap m_OutputCubemap;
        }

        public void BakeAtmosphereToCubemap(RenderGraph renderGraph, ref ScriptableRenderContext context, AtmosphereResources atmosphereResources, Light sunLight)
        {
            if (atmosphereResources.BakeToCubemap == null)
                return;

            if (m_AtmosphereCubemap == null)
            {
                m_AtmosphereCubemap = new Cubemap(32, TextureFormat.RGBAHalf, true, true);
            }

            if (m_AtmospherePrefilterSpecular == null)
            {
                m_AtmospherePrefilterSpecular = new Cubemap(32, TextureFormat.RGBAHalf, true, true);
            }


            using (var builder = renderGraph.AddRenderPass<BakeAtmosphereToCubeData>("Bake Atmosphere Scattering to Cubemap Pass",
                out var passData, new ProfilingSampler("Bake Atmosphere Scattering to Cubemap Pass Profiler")))
            {
                passData.m_ComputeShader = atmosphereResources.BakeToCubemap;
                passData.kBakeAtmosphereToCube = atmosphereResources.BakeToCubemap.FindKernel("CSMain");
                passData.SkyLUT = atmosphereResources.SkyboxLUT;
                passData.m_FaceTextureArray = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, m_CubemapSize));
                passData.mainLightPosition = -sunLight.transform.localToWorldMatrix.GetColumn(2);
                passData.mainLightPosition.w = 0;
                passData.mainLightIntensity = sunLight.intensity;
                passData.m_Cubemap = m_AtmosphereCubemap;
                passData.cubeMapWidth = m_AtmosphereCubemap.width;
                passData.cubeMapHeight = m_AtmosphereCubemap.height;
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((BakeAtmosphereToCubeData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeVectorParam(data.m_ComputeShader, ProjSHShaderParameters._MainLightPosition, data.mainLightPosition);
                    context.cmd.SetComputeFloatParam(data.m_ComputeShader, ProjSHShaderParameters._MainLightIntensity, data.mainLightIntensity);

                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kBakeAtmosphereToCube, AtmosphereShaderParameters._SkyboxLUT, data.SkyLUT);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kBakeAtmosphereToCube, AtmosphereShaderParameters._Cubemap, data.m_FaceTextureArray);

                    int groupX = data.cubeMapWidth / 8;
                    int groupY = data.cubeMapHeight / 8;
                    context.cmd.DispatchCompute(data.m_ComputeShader, data.kBakeAtmosphereToCube, groupX, groupY, 6);

                    for (int i = 0; i < 6; i++)
                    {
                        context.cmd.CopyTexture(data.m_FaceTextureArray, i, 0, data.m_Cubemap, i, 0);
                    }

                    context.renderContext.ExecuteCommandBuffer(context.cmd);
                    context.cmd.Clear();
                });
            }

            //Down sample cube map texture array
            int remainTextureMip = (int)Mathf.Log(m_CubemapSize, 2.0f);
            for (int mip = 0; mip < remainTextureMip; ++mip)
            {
                using (var builder = renderGraph.AddRenderPass<DownSampleCubeData>("Downsample Cubemap Pass",
                    out var passData, new ProfilingSampler("Downsample Cubemap Pass Profiler")))
                {
                    passData.kDownSampleCube = atmosphereResources.DownSampleCubemap.FindKernel("CSMain");
                    passData.m_ComputeShader = atmosphereResources.DownSampleCubemap;
                    passData.m_InputCubemap = m_AtmosphereCubemap;
                    passData.m_MipLevel = mip + 1;
                    passData.m_OutputTextureSize = m_CubemapSize >> passData.m_MipLevel;
                    passData.m_OutputFaceTextureArray = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, passData.m_OutputTextureSize));

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((DownSampleCubeData data, RenderGraphContext context) =>
                    {
                        context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kDownSampleCube, AtmosphereShaderParameters._Cubemap, data.m_OutputFaceTextureArray);
                        context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kDownSampleCube, AtmosphereShaderParameters._InputCubemap, data.m_InputCubemap);
                        int groupX = Mathf.Max(data.m_OutputTextureSize / 8, 1);
                        int groupY = groupX;
                        context.cmd.DispatchCompute(data.m_ComputeShader, data.kDownSampleCube, groupX, groupY, 6);

                        for (int i = 0; i < 6; i++)
                        {
                            context.cmd.CopyTexture(data.m_OutputFaceTextureArray, i, 0, data.m_InputCubemap, i, passData.m_MipLevel);
                        }

                        context.renderContext.ExecuteCommandBuffer(context.cmd);
                        context.cmd.Clear();
                    });
                }
            }

            //prefilter specular cubemap
            using (var builder = renderGraph.AddRenderPass<PrefilterSpecularPassData>("Prefilter Specular Cubemap Pass",
                out var passData, new ProfilingSampler("Prefilter Specular Cubemap Pass Profiler")))
            {
                passData.kPrefilterSpecular = atmosphereResources.PrefilterSpecularCubemap.FindKernel("CSMain");
                passData.m_ComputeShader = atmosphereResources.PrefilterSpecularCubemap;
                passData.m_InputCubemap = m_AtmosphereCubemap;
                passData.m_OutputFaceTextureArray0 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 32));
                passData.m_OutputFaceTextureArray1 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 16));
                passData.m_OutputFaceTextureArray2 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 8));
                passData.m_OutputFaceTextureArray3 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 4));
                passData.m_OutputFaceTextureArray4 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 2));
                passData.m_OutputFaceTextureArray5 = builder.WriteTexture(CreateAtmosphereFaceTextureArray(renderGraph, 1));
                passData.m_OutputCubemap = m_AtmospherePrefilterSpecular;

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PrefilterSpecularPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._InputCubemap, data.m_InputCubemap);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap0, data.m_OutputFaceTextureArray0);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap1, data.m_OutputFaceTextureArray1);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap2, data.m_OutputFaceTextureArray2);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap3, data.m_OutputFaceTextureArray3);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap4, data.m_OutputFaceTextureArray4);
                    context.cmd.SetComputeTextureParam(data.m_ComputeShader, data.kPrefilterSpecular, AtmosphereShaderParameters._OutputCubemap5, data.m_OutputFaceTextureArray5);

                    int threadTotal = 32 * 32 + 16 * 16 + 8 * 8 + 4 * 4 + 2 * 2 + 1;
                    int groupX = Mathf.CeilToInt((float)threadTotal / 64);
                    context.cmd.DispatchCompute(data.m_ComputeShader, data.kPrefilterSpecular, groupX, 1, 6);

                    for (int i = 0; i < 6; i++)
                    {
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray0, i, 0, data.m_OutputCubemap, i, 0);
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray1, i, 0, data.m_OutputCubemap, i, 1);
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray2, i, 0, data.m_OutputCubemap, i, 2);
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray3, i, 0, data.m_OutputCubemap, i, 3);
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray4, i, 0, data.m_OutputCubemap, i, 4);
                        context.cmd.CopyTexture(data.m_OutputFaceTextureArray5, i, 0, data.m_OutputCubemap, i, 5);
                    }
                    context.cmd.SetGlobalTexture(AtmosphereShaderParameters._ATMOSPHERE_SPECULAR, data.m_OutputCubemap);

                    context.renderContext.ExecuteCommandBuffer(context.cmd);
                    context.cmd.Clear();
                });
            }
        }
            
        public void Release()
        {
            if (m_skySHSetting != null)
            {
                m_skySHSetting.Release();
                m_skySHSetting = null;
            }
        }

        public void Update()
        {
            if (m_SHBakeReadbacks.Count == 0)
                return;
            while (m_SHBakeReadbacks.Peek().done || m_SHBakeReadbacks.Peek().hasError == true)
            {
                // If this has an error, just skip it
                if (!m_SHBakeReadbacks.Peek().hasError)
                {
                    NativeArray<GPUAmbientSHCoefL2> result = m_SHBakeReadbacks.Peek().GetData<GPUAmbientSHCoefL2>();
                    result.CopyTo(m_finalPolySHL2);
                    GPUAmbientSHCoefL2 sh = m_finalPolySHL2[0] * Mathf.GammaToLinearSpace(RenderSettings.ambientIntensity);
                    //Debug.Log("c0=" + sh.c0);
                    //Debug.Log("c1=" + sh.c1);
                    //Debug.Log("c2=" + sh.c2);
                    //Debug.Log("c3=" + sh.c3);
                    //Debug.Log("c4=" + sh.c4);
                    //Debug.Log("c5=" + sh.c5);
                    //Debug.Log("c6=" + sh.c6);
                    //Debug.Log("c7=" + sh.c7);
                    //Debug.Log("c8=" + sh.c8);
                    GPUPolynomialSHL2 polynomialSHL2 = GetEnvmapSHPolyCoef(ref sh);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHAr, polynomialSHL2.SHAr);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHAg, polynomialSHL2.SHAg);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHAb, polynomialSHL2.SHAb);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHBr, polynomialSHL2.SHBr);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHBg, polynomialSHL2.SHBg);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHBb, polynomialSHL2.SHBb);
                    Shader.SetGlobalVector(ProjSHShaderParameters._SHC, polynomialSHL2.SHC);
                }
                m_SHBakeReadbacks.Dequeue();
            }
        }

        public void BakeCubemapToSHAmbient(ref ScriptableRenderContext context, AtmosphereResources atmosphereResources, Cubemap cubemap)
        {
            if (atmosphereResources.ProjAtmosphereToSH == null)
                return;

            if (cubemap == null)
                return;

            if (m_bakeSHSamples == null)
            {
                InitSHBakeSamples();
            }

            if (m_skySHSetting == null)
            {
                m_skySHSetting = new AtmosphereSHSetting();
            }

            if (m_skySHSetting.m_BakeSamples == null)
            {
                m_skySHSetting.m_BakeSamples = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Structured);
                m_skySHSetting.m_BakeSamples.SetData(m_bakeSHSamples);
            }

            if (AtmosphereSHSetting.OPTIMIZE_BAKING)
            {
                if (m_skySHSetting.m_SHCoefficientsTexture == null)
                {
                    m_skySHSetting.m_SHCoefficientsTexture = new RenderTexture(m_bakeSHSamples.Length, 9, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    m_skySHSetting.m_SHCoefficientsTexture.enableRandomWrite = true;
                    m_skySHSetting.m_SHCoefficientsTexture.Create();
                }

                if (m_skySHSetting.m_SHCoefficientsGroupSumTexture == null)
                {
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture = new RenderTexture(m_bakeSHSamples.Length / AtmosphereSHSetting.THREAD_NUM_PER_GROUP,
                        9, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture.enableRandomWrite = true;
                    m_skySHSetting.m_SHCoefficientsGroupSumTexture.Create();
                }
            }
            else
            {
                if (m_skySHSetting.m_SHCoefficientsArray == null)
                    m_skySHSetting.m_SHCoefficientsArray = new ComputeBuffer(m_bakeSHSamples.Length, Marshal.SizeOf<GPUAmbientSHCoefL2>());
                if (m_skySHSetting.m_SHCoefficientsGroupSumArray == null)
                    m_skySHSetting.m_SHCoefficientsGroupSumArray = new ComputeBuffer(m_bakeSHSamples.Length / AtmosphereSHSetting.THREAD_NUM_PER_GROUP,
                        Marshal.SizeOf<GPUAmbientSHCoefL2>());
            }

            if (m_skySHSetting.m_FinalProjSH == null)
                m_skySHSetting.m_FinalProjSH = new ComputeBuffer(1, Marshal.SizeOf<GPUPolynomialSHL2>(), ComputeBufferType.Structured);

            ComputeShader csProjAtmosphereToSH = atmosphereResources.ProjAtmosphereToSH;
            CommandBuffer cmd = CommandBufferPool.Get(m_BakeSHProfilerTag);

            using (new ProfilingScope(cmd, m_BakeSHProfilingSampler))
            {
                LocalKeyword bakeCubemap = new LocalKeyword(csProjAtmosphereToSH, "_BAKE_CUBEMAP");
                cmd.EnableKeyword(csProjAtmosphereToSH, bakeCubemap);
                //set the cs parameters
                

                float sqrtPI = Mathf.Sqrt(Mathf.PI);
                float fC0 = (1.0f / (2.0f * sqrtPI));
                float fC1 = (Mathf.Sqrt(3.0f) / (3.0f * sqrtPI));
                float fC2 = (Mathf.Sqrt(15.0f) / (8.0f * sqrtPI));
                float fC3 = (Mathf.Sqrt(5.0f) / (16.0f * sqrtPI));
                float fC4 = (0.5f * fC2);

                //cmd.SetComputeVectorParam(csProjAtmosphereToSH, ProjSHShaderParameters._fC0to3, new Vector4(fC0, fC1, fC2, fC3));
                //cmd.SetComputeFloatParam(csProjAtmosphereToSH, ProjSHShaderParameters._fC4, fC4);

                if (AtmosphereSHSetting.DIRECT_BAKING)
                {
                    int kBakeDirect = csProjAtmosphereToSH.FindKernel("BakeSHDirect");
                    if (kBakeDirect == -1)
                        return;

                    cmd.SetComputeTextureParam(csProjAtmosphereToSH, kBakeDirect, "_Cubemap", cubemap);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeDirect, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeDirect, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);
                    cmd.DispatchCompute(csProjAtmosphereToSH, kBakeDirect, 1, 1, 1);
                    GetAmbientSHData(m_skySHSetting.m_FinalProjSH);
                }
                else
                {
                    int kPreSumSH = csProjAtmosphereToSH.FindKernel("PresumSHCoefficient");
                    if (kPreSumSH == -1)
                        return;

                    //pass 1 parallen sum the sh coefficients into array.
                    cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSH, "_Cubemap", cubemap);
                    cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._BakeSamples, m_skySHSetting.m_BakeSamples);
                    int groupsNumX = m_bakeSHSamples.Length / 128;
                    if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                    {
                        cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._SHCoefficients, m_skySHSetting.m_SHCoefficientsTexture);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSH, groupsNumX, 9, 1);
                    }
                    else
                    {
                        cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSH, ProjSHShaderParameters._SHCoefficients, m_skySHSetting.m_SHCoefficientsArray);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSH, groupsNumX, 1, 1);
                    }

                    //pass 2, sum the last element of sh coefficients to the group array.
                    if (groupsNumX > 1)
                    {
                        int kPreSumSHGroup = csProjAtmosphereToSH.FindKernel("PreSumGroupSH");
                        if (kPreSumSHGroup >= 0)
                        {
                            //csProjAtmosphereToSH.SetInt(ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNumX);
                            cmd.SetComputeIntParam(csProjAtmosphereToSH, ProjSHShaderParameters._ArrayLengthPerThreadGroup, groupsNumX);
                            cmd.SetComputeIntParam(csProjAtmosphereToSH, ProjSHShaderParameters._GroupsNumPowOf2, Mathf.NextPowerOfTwo(groupsNumX));
                            if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                            {
                                cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, m_skySHSetting.m_SHCoefficientsTexture);
                                cmd.SetComputeTextureParam(csProjAtmosphereToSH, kPreSumSHGroup,
                                    ProjSHShaderParameters._SHCoefficientsGroupSumArray, m_skySHSetting.m_SHCoefficientsGroupSumTexture);
                                cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSHGroup, 1, 9, 1);
                            }
                            else
                            {
                                cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSHGroup, ProjSHShaderParameters._InputSHCoefficients, m_skySHSetting.m_SHCoefficientsArray);
                                cmd.SetComputeBufferParam(csProjAtmosphereToSH, kPreSumSHGroup,
                                    ProjSHShaderParameters._SHCoefficientsGroupSumArray, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                                cmd.DispatchCompute(csProjAtmosphereToSH, kPreSumSHGroup, 1, 1, 1);
                            }


                        }
                    }

                    //pass 3
                    int kBakeSHToTexture = csProjAtmosphereToSH.FindKernel("BakeSHToTexture");
                    if (kBakeSHToTexture >= 0)
                    {
                        if (AtmosphereSHSetting.OPTIMIZE_BAKING)
                        {
                            
                            cmd.SetComputeTextureParam(csProjAtmosphereToSH, kBakeSHToTexture,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, m_skySHSetting.m_SHCoefficientsGroupSumTexture);
                        }
                        else
                        {
                            
                            cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeSHToTexture,
                                ProjSHShaderParameters._SHCoefficientsGroupSumArrayInput, m_skySHSetting.m_SHCoefficientsGroupSumArray);
                        }

                        cmd.SetComputeBufferParam(csProjAtmosphereToSH, kBakeSHToTexture, ProjSHShaderParameters._FinalProjSH, m_skySHSetting.m_FinalProjSH);
                        cmd.DispatchCompute(csProjAtmosphereToSH, kBakeSHToTexture, 1, 1, 1);

                        GetAmbientSHData(m_skySHSetting.m_FinalProjSH);
                    }
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    
}
