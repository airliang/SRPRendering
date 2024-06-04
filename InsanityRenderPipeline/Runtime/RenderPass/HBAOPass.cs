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
        public ComputeShader ssao;
        public ComputeShader blur;
        public Texture blueNoiseTexture;
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
        public static int _HBAOKernel = -1;
        public static int _VerticalBlurKernel = -1;
        public static int _HorizontalBlurKernel = -1;
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

        static ProfilingSampler s_HBAOPassProfiler = new ProfilingSampler("HBAO Pass Profiler");
        static ProfilingSampler s_HBAOVerticalBlurProfiler = new ProfilingSampler("Vertical Blur Pass Profiler");
        static ProfilingSampler s_HBAOHorizontalBlurProfiler = new ProfilingSampler("Horizontal Blur Pass Profiler");

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
                blurDataV.aoOutput = builder.WriteTexture(CreateAOMaskTexture(renderingData.renderGraph, (int)AOMaskWidth, (int)AOMaskHeight, "AOTemp"));
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
    }
}

