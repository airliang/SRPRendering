using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public class SSAOSettings
    {
        public float radius;
        public float horizonBias = 0;
        public bool halfResolution = true;
        public float intensity = 1;
        public float aoFadeStart = 0;
        public float aoFadeEnd = 100.0f;
        public SSAOBlurType blurMethod = SSAOBlurType.eGaussian;
        public ComputeShader ssao;
        public ComputeShader blur;
        public ComputeShader duarBlur;
        public Texture2D blueNoiseTexture;
    }

    public class HBAOShaderParams
    {
        public static int _DepthTexture;
        public static int _NormalTexture;
        public static int _AOMask;
        public static int _NoiseTexture;
        public static int _HBAOParams;
        public static int _HBAOParams2;
        public static int _NoiseParam;
        public static int _AOMaskSize;
        public static int _ScreenSize;
        public static int _ProjInverse;
        public static int _ViewMatrix;
        public static int _HalfResolution;
        public static int _ProjectionParams;
        public static int _AOInput;
        public static int _AOOutput;
        public static int _OutputTexSize;
        public static int _HBAOKernel = -1;
        public static int _VerticalBlurKernel = -1;
        public static int _HorizontalBlurKernel = -1;
        public static int _DuarBlurDownSampleKernel = -1;
        public static int _DuarBlurUpSampleKernel = -1;
    }

    public partial class RenderPasses
    {
        class HBAOPassData
        {
            public TextureHandle depth;
            public TextureHandle normal;
            public TextureHandle ao;
            public Texture noise;
            public Vector4 HBAOParams;
            public Vector4 HBAOParams2;
            public Vector4 NoiseParams;
            public Vector4 AOMaskSize;
            public Vector4 ScreenSize;
            public Matrix4x4 projInverse;
            public Matrix4x4 view;
            public float HalfResolution;
            public Vector4 projectionParams;
            public ComputeShader cs;
            public int kernel;
        }

        class BlurAOPassData
        {
            public TextureHandle aoInput;
            public TextureHandle aoOutput;
            public Vector4 AOMaskSize;
            public ComputeShader cs;
            public int kVerticalBlur;
            public int kHorizontalBlur;
        }

        class DualBlurPassData
        {
            public TextureHandle src;
            public TextureHandle dst;
            public Vector4 sizeDstTexture;
            public ComputeShader cs;
            public int kernel;
        }

        static ProfilingSampler s_HBAOPassProfiler = new ProfilingSampler("HBAO Pass Profiler");
        static ProfilingSampler s_HBAOVerticalBlurProfiler = new ProfilingSampler("Vertical Blur Pass Profiler");
        static ProfilingSampler s_HBAOHorizontalBlurProfiler = new ProfilingSampler("Horizontal Blur Pass Profiler");
        static ProfilingSampler s_DualBlurDownProfiler = new ProfilingSampler("Duar Down Sample Pass Profiler");
        static ProfilingSampler s_DualBlurUpProfiler = new ProfilingSampler("Duar Up Sample Pass Profiler");

        public static void InitializeSSAOShaderParameters()
        {
            HBAOShaderParams._DepthTexture = Shader.PropertyToID("_DepthTexture");
            HBAOShaderParams._NormalTexture = Shader.PropertyToID("_NormalTexture");
            HBAOShaderParams._AOMask = Shader.PropertyToID("_AOMask");
            HBAOShaderParams._NoiseTexture = Shader.PropertyToID("_NoiseTexture");
            HBAOShaderParams._HBAOParams = Shader.PropertyToID("_HBAOParams");
            HBAOShaderParams._HBAOParams2 = Shader.PropertyToID("_HBAOParams2");
            HBAOShaderParams._NoiseParam = Shader.PropertyToID("_NoiseParam");
            HBAOShaderParams._AOMaskSize = Shader.PropertyToID("_AOMaskSize");
            HBAOShaderParams._ScreenSize = Shader.PropertyToID("_ScreenSize");
            HBAOShaderParams._ProjInverse = Shader.PropertyToID("_ProjInverse");
            HBAOShaderParams._ViewMatrix = Shader.PropertyToID("_ViewMatrix");
            HBAOShaderParams._HalfResolution = Shader.PropertyToID("_HalfResolution");
            HBAOShaderParams._ProjectionParams = Shader.PropertyToID("_ProjectionParams");
            HBAOShaderParams._AOInput = Shader.PropertyToID("_AOInput");
            HBAOShaderParams._AOOutput = Shader.PropertyToID("_AOOutput");
            HBAOShaderParams._OutputTexSize = Shader.PropertyToID("_OutputTexSize");
        }

        private static TextureHandle CreateAOMaskTexture(RenderGraph graph, int width, int height, string name)
        {
            //Texture description
            TextureDesc textureRTDesc = new TextureDesc(width, height);
            textureRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.R8, false);
            textureRTDesc.depthBufferBits = 0;
            textureRTDesc.msaaSamples = MSAASamples.None;
            textureRTDesc.enableRandomWrite = true;
            //textureRTDesc.clearBuffer = true;
            //textureRTDesc.clearColor = Color.black;
            textureRTDesc.name = name;
            textureRTDesc.filterMode = FilterMode.Point;

            return graph.CreateTexture(textureRTDesc);
        }

        public static void Render_HBAOPass(RenderingData renderingData, TextureHandle depth, TextureHandle normal, out TextureHandle ssaoMask, SSAOSettings ssaoSettings)
        {
            float AOMaskWidth = ssaoSettings.halfResolution ? (GlobalRenderSettings.screenResolution.width * 0.5f) : GlobalRenderSettings.screenResolution.width;
            float AOMaskHeight = ssaoSettings.halfResolution ? (GlobalRenderSettings.screenResolution.height * 0.5f) : GlobalRenderSettings.screenResolution.height;
            Vector4 AOMaskSize = new Vector4(AOMaskWidth, AOMaskHeight, 1.0f / AOMaskWidth, 1.0f / AOMaskHeight);

            using (var builder = renderingData.renderGraph.AddRenderPass<HBAOPassData>("HBAO Pass", out var passData, s_HBAOPassProfiler))
            {
                if (HBAOShaderParams._HBAOKernel == -1)
                {
                    HBAOShaderParams._HBAOKernel = ssaoSettings.ssao.FindKernel("HBAO");
                }
                builder.AllowPassCulling(false);
                passData.cs = ssaoSettings.ssao;
                passData.kernel = HBAOShaderParams._HBAOKernel;
                passData.depth = builder.ReadTexture(depth);
                passData.normal = builder.ReadTexture(normal);
                passData.noise = ssaoSettings.blueNoiseTexture;

                passData.AOMaskSize = AOMaskSize;
                passData.ScreenSize.x = GlobalRenderSettings.screenResolution.width;
                passData.ScreenSize.y = GlobalRenderSettings.screenResolution.height;
                passData.ScreenSize.z = 1.0f / passData.ScreenSize.x;
                passData.ScreenSize.w = 1.0f / passData.ScreenSize.y;
                float projScale = GlobalRenderSettings.screenResolution.height / (Mathf.Tan(renderingData.cameraData.camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2.0f);
                passData.HBAOParams.x = ssaoSettings.intensity;
                passData.HBAOParams.y = ssaoSettings.horizonBias;
                passData.HBAOParams.z = -1.0f / ssaoSettings.radius * ssaoSettings.radius;
                passData.HBAOParams.w = projScale * ssaoSettings.radius * 0.5f;
                passData.HalfResolution = ssaoSettings.halfResolution ? 1.0f : 0;
                passData.NoiseParams.x = UnityEngine.Random.value;
                passData.NoiseParams.y = UnityEngine.Random.value;
                passData.NoiseParams.z = GlobalRenderSettings.screenResolution.width / ssaoSettings.blueNoiseTexture.width;
                passData.NoiseParams.w = GlobalRenderSettings.screenResolution.height / ssaoSettings.blueNoiseTexture.height;
                //make a linear equation to calculate the ao fade.
                //y = ax + b  y is ao fade, x is distance
                //a = 1.0 / (fadeStart - fadeEnd); b = fadeEnd / (fadeEnd - fadeStart)
                passData.HBAOParams2.x = 1.0f / (ssaoSettings.aoFadeStart - ssaoSettings.aoFadeEnd);
                passData.HBAOParams2.y = ssaoSettings.aoFadeEnd / (ssaoSettings.aoFadeEnd - ssaoSettings.aoFadeStart);

                Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix;
                proj = proj * Matrix4x4.Scale(new Vector3(1, 1, -1));
                passData.projInverse = proj.inverse;
                passData.view = renderingData.cameraData.camera.transform.worldToLocalMatrix;
                ssaoMask = CreateAOMaskTexture(renderingData.renderGraph, (int)AOMaskWidth, (int)AOMaskHeight, "SSAOMask");
                passData.ao = builder.WriteTexture(ssaoMask);
                passData.projectionParams = new Vector4(proj.m00, proj.m11, 1.0f / proj.m00, 1.0f / proj.m11);

                //for test
                Camera camera = renderingData.cameraData.camera;
                Vector3 viewPos = camera.ScreenToWorldPoint(Vector3.zero);
                Ray ray = camera.ScreenPointToRay(Vector3.zero);
                RaycastHit hitInfo;
                Physics.Raycast(ray, out hitInfo);
                viewPos = passData.view.MultiplyPoint(hitInfo.point);
                Vector3 normalView = passData.view.MultiplyVector(hitInfo.normal).normalized;

                Ray ray11 = camera.ScreenPointToRay(new Vector3(1, 1, 0));
                Physics.Raycast(ray11, out hitInfo);
                Vector3 viewPos11 = passData.view.MultiplyPoint(hitInfo.point);

                Vector3 horizonDir = viewPos11 - viewPos;
                float hFalloff = Vector3.Dot(horizonDir.normalized, normalView);
                //end test

                //Builder
                builder.SetRenderFunc((HBAOPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, passData.kernel, HBAOShaderParams._DepthTexture, data.depth);
                    context.cmd.SetComputeTextureParam(data.cs, passData.kernel, HBAOShaderParams._NormalTexture, data.normal);
                    context.cmd.SetComputeTextureParam(data.cs, passData.kernel, HBAOShaderParams._AOMask, data.ao);
                    context.cmd.SetComputeTextureParam(data.cs, passData.kernel, HBAOShaderParams._NoiseTexture, data.noise);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._HBAOParams, data.HBAOParams);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._HBAOParams2, data.HBAOParams2);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._AOMaskSize, data.AOMaskSize);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._ScreenSize, data.ScreenSize);
                    context.cmd.SetComputeMatrixParam(data.cs, HBAOShaderParams._ProjInverse, data.projInverse);
                    context.cmd.SetComputeMatrixParam(data.cs, HBAOShaderParams._ViewMatrix, data.view);
                    context.cmd.SetComputeFloatParam(data.cs, HBAOShaderParams._HalfResolution, data.HalfResolution);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._ProjectionParams, data.projectionParams);
                    context.cmd.SetGlobalTexture(HBAOShaderParams._AOMask, data.ao);

                    int groupX = Mathf.CeilToInt((float)data.AOMaskSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.AOMaskSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }

            if (ssaoSettings.blurMethod == SSAOBlurType.eGaussian)
                GaussianBlurAOMask(renderingData, ssaoMask, ssaoSettings, AOMaskSize);
            else
                DualBlur(renderingData, ssaoMask, ssaoSettings, AOMaskSize);
        }

        static void GaussianBlurAOMask(RenderingData renderingData, TextureHandle ssaoMask, SSAOSettings ssaoSettings, Vector4 AOMaskSize)
        {
            BlurAOPassData blurDataV;
            using (var builder = renderingData.renderGraph.AddRenderPass<BlurAOPassData>("HBAO Vertical Blur Pass", out blurDataV, s_HBAOVerticalBlurProfiler))
            {
                if (HBAOShaderParams._VerticalBlurKernel == -1)
                {
                    HBAOShaderParams._VerticalBlurKernel = ssaoSettings.blur.FindKernel("VerticleBlur");
                }

                builder.AllowPassCulling(false);
                blurDataV.cs = ssaoSettings.blur;
                blurDataV.kVerticalBlur = HBAOShaderParams._VerticalBlurKernel;
                blurDataV.aoInput = ssaoMask;
                blurDataV.aoOutput = builder.WriteTexture(CreateAOMaskTexture(renderingData.renderGraph, (int)AOMaskSize.x, (int)AOMaskSize.y, "AOTemp"));
                blurDataV.AOMaskSize = AOMaskSize;

                builder.SetRenderFunc((BlurAOPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kVerticalBlur, HBAOShaderParams._AOInput, data.aoInput);
                    context.cmd.SetComputeTextureParam(data.cs, data.kVerticalBlur, HBAOShaderParams._AOOutput, data.aoOutput);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._AOMaskSize, data.AOMaskSize);

                    int groupX = Mathf.CeilToInt(data.AOMaskSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.AOMaskSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kVerticalBlur, groupX, groupY, 1);
                });
            }
            //return;
            BlurAOPassData blurDataH;
            using (var builder = renderingData.renderGraph.AddRenderPass<BlurAOPassData>("HBAO Horizontal Blur Pass", out blurDataH, s_HBAOHorizontalBlurProfiler))
            {
                if (HBAOShaderParams._HorizontalBlurKernel == -1)
                {
                    HBAOShaderParams._HorizontalBlurKernel = ssaoSettings.blur.FindKernel("HorizontalBlur");
                }

                builder.AllowPassCulling(false);
                blurDataH.cs = ssaoSettings.blur;
                blurDataH.kHorizontalBlur = HBAOShaderParams._HorizontalBlurKernel;
                blurDataH.aoInput = builder.ReadTexture(blurDataV.aoOutput);
                blurDataH.aoOutput = builder.WriteTexture(ssaoMask);
                blurDataH.AOMaskSize = AOMaskSize;

                builder.SetRenderFunc((BlurAOPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kHorizontalBlur, HBAOShaderParams._AOInput, data.aoInput);
                    context.cmd.SetComputeTextureParam(data.cs, data.kHorizontalBlur, HBAOShaderParams._AOOutput, data.aoOutput);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._AOMaskSize, data.AOMaskSize);

                    int groupX = Mathf.CeilToInt(data.AOMaskSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.AOMaskSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kHorizontalBlur, groupX, groupY, 1);
                });
            }
        }

        static void DualBlur(RenderingData renderingData, TextureHandle ssaoMask, SSAOSettings ssaoSettings, Vector4 AOMaskSize)
        {
            float srcWidth = AOMaskSize.x;
            float srcHeight = AOMaskSize.y;
            float dstWidth = srcWidth * 0.5f;
            float dstHeight = srcHeight * 0.5f;
            DualBlurPassData downBlurPassData = null;
            using (var builder = renderingData.renderGraph.AddRenderPass<DualBlurPassData>("Duar DownSample Pass", out downBlurPassData, s_DualBlurDownProfiler))
            {
                if (HBAOShaderParams._DuarBlurDownSampleKernel == -1)
                {
                    HBAOShaderParams._DuarBlurDownSampleKernel = ssaoSettings.duarBlur.FindKernel("DownSample");
                }

                builder.AllowPassCulling(false);
                downBlurPassData.cs = ssaoSettings.duarBlur;
                downBlurPassData.kernel = HBAOShaderParams._DuarBlurDownSampleKernel;
                downBlurPassData.src = builder.ReadTexture(ssaoMask);
                downBlurPassData.dst = builder.WriteTexture(CreateAOMaskTexture(renderingData.renderGraph, (int)dstWidth, (int)dstHeight, "AOTemp"));
                downBlurPassData.sizeDstTexture = new Vector4(dstWidth, dstHeight, 1.0f / dstWidth, 1.0f / dstHeight);

                builder.SetRenderFunc((DualBlurPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, HBAOShaderParams._AOInput, data.src);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, HBAOShaderParams._AOOutput, data.dst);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._OutputTexSize, data.sizeDstTexture);

                    int groupX = Mathf.CeilToInt(data.sizeDstTexture.x / 8);
                    int groupY = Mathf.CeilToInt(data.sizeDstTexture.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }

            using (var builder = renderingData.renderGraph.AddRenderPass<DualBlurPassData>("Duar UpSample Pass", out var passData, s_DualBlurUpProfiler))
            {
                if (HBAOShaderParams._DuarBlurUpSampleKernel == -1)
                {
                    HBAOShaderParams._DuarBlurUpSampleKernel = ssaoSettings.duarBlur.FindKernel("UpSample");
                }

                srcWidth = dstWidth;
                srcHeight = dstHeight;
                dstWidth = srcWidth * 2;
                dstHeight = srcHeight * 2;

                builder.AllowPassCulling(false);
                passData.cs = ssaoSettings.duarBlur;
                passData.kernel = HBAOShaderParams._DuarBlurUpSampleKernel;
                passData.src = builder.ReadTexture(downBlurPassData.dst);
                passData.dst = builder.WriteTexture(ssaoMask);
                passData.sizeDstTexture = new Vector4(dstWidth, dstHeight, 1.0f / dstWidth, 1.0f / dstHeight);

                builder.SetRenderFunc((DualBlurPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, HBAOShaderParams._AOInput, data.src);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, HBAOShaderParams._AOOutput, data.dst);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._OutputTexSize, data.sizeDstTexture);

                    int groupX = Mathf.CeilToInt(data.sizeDstTexture.x / 8);
                    int groupY = Mathf.CeilToInt(data.sizeDstTexture.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }
        }

        public static void CreateNoiseTexture(out Texture2D noiseTexture)
        {
            noiseTexture = new Texture2D(4,4, TextureFormat.RGBA32, false, true);

            int size = 4;


            float[,] radArray = new float[4, 4];

            // Init first field with base seed
            radArray[0, 0] = 0.0f;

            // Init frac levels
            int lastFracLevel = 0;
            int fracLevel = 1;

            // Sampling pattern is a uniformly distributed spiral on 2D projected disk. Opposite direction samples are guaranteed by spiral pattern in rotation
            // We want to maximize variance of full sampling pattern, while keeping local quad to quad variance low
            while (fracLevel < size)
            {
                // Create a fractal level of swizzled rotation texture
                int nextFracLevel = fracLevel << 1;
                int levelSize = 1 << lastFracLevel;

                // Angular step for full circle on current frac level
                int totalRotationsLevel = nextFracLevel * nextFracLevel;
                float levelAngleStep = 360.0f / totalRotationsLevel * Mathf.Deg2Rad;

                int stepSize;

                // Use dynamically created TOP LEFT quarter, rotate by 2 steps and copy into TOP RIGHT
                stepSize = 2;
                for (int i = 0; i < levelSize; i++)
                {
                    for (int j = 0; j < levelSize; j++)
                    {
                        radArray[fracLevel + i, j] = radArray[i, j] + (float)stepSize * levelAngleStep;
                    }
                }

                // Use dynamically created TOP LEFT quarter, rotate by 1 step and copy into BOTTOM LEFT
                stepSize = 1;
                for (int i = 0; i < levelSize; i++)
                {
                    for (int j = 0; j < levelSize; j++)
                    {
                        radArray[i, fracLevel + j] = radArray[i, j] + (float)stepSize * levelAngleStep;
                    }
                }

                // Use dynamically created TOP LEFT quarter, rotate by 3 steps and copy into BOTTOM RIGHT
                stepSize = 3;
                for (int i = 0; i < levelSize; i++)
                {
                    for (int j = 0; j < levelSize; j++)
                    {
                        radArray[fracLevel + i, fracLevel + j] = radArray[i, j] + stepSize * levelAngleStep;
                    }
                }

                lastFracLevel = fracLevel;
                fracLevel = nextFracLevel;
            }

            // fill texture with random normals
            Vector4 normal;
            Vector4 normalCompressed;
            Vector4 bias = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            Vector4 scale = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
            Color[] colors = new Color[size * size];

            for (int y = 0; y < size; ++y)
            {
                //curPix = curLine;

                for (int x = 0; x < size; ++x)
                {
                    // create a rotation matrix, xy first row zw second row
                    
                    float sin = Mathf.Sin(radArray[x, y]);
                    float cos = Mathf.Cos(radArray[x, y]);
                    Vector4 rotationMatrix = new Vector4(cos, sin,
                                                         -sin, cos);

                    // rotate the normal, normalize, then scale and bias
                    //normal = new Vector4(rotationMatrix.Get(0, 0), rotationMatrix.Get(1, 0),
                    //    rotationMatrix.Get(0, 1), rotationMatrix.Get(1, 1));
                    normalCompressed = (rotationMatrix + bias).Mul(scale);

                    colors[y * size + x] = new Color(normalCompressed.x, normalCompressed.y, normalCompressed.z, normalCompressed.w);

                    // copy normal inside the texture (format ARGB)
                    //*curPix = color.GetAsARGB();
                    //curPix++;
                }

                //curLine = (ubiU32*)(((ubiU8*)curLine) + lockRect.Pitch);
            }

            noiseTexture.SetPixels(colors);
            noiseTexture.Apply(false, true);
        }
    }
}

