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
        public ComputeShader ssao;
        public ComputeShader blur;
    }

    public class HBAOShaderParams
    {
        public static int _DepthTexture;
        public static int _NormalTexture;
        public static int _AOMask;
        public static int _HBAOParams;
        public static int _AOMaskSize;
        public static int _ScreenSize;
        public static int _ProjInverse;
        public static int _ViewMatrix;
        public static int _HalfResolution;
        public static int _HBAOKernel = -1;
    }

    public partial class RenderPasses
    {
        class HBAOPassData
        {
            public TextureHandle depth;
            public TextureHandle normal;
            public TextureHandle ao;
            public Vector4 HBAOParams;
            public Vector2 AOMaskSize;
            public Vector4 ScreenSize;
            public Matrix4x4 projInverse;
            public Matrix4x4 view;
            public float HalfResolution;
            public ComputeShader cs;
            public int kernel;
        }
        static ProfilingSampler s_HBAOPassProfiler = new ProfilingSampler("HBAO Pass Profiler");

        public static void InitializeSSAOShaderParameters()
        {
            HBAOShaderParams._DepthTexture = Shader.PropertyToID("_DepthTexture");
            HBAOShaderParams._NormalTexture = Shader.PropertyToID("_NormalTexture");
            HBAOShaderParams._AOMask = Shader.PropertyToID("_AOMask");
            HBAOShaderParams._HBAOParams = Shader.PropertyToID("_HBAOParams");
            HBAOShaderParams._AOMaskSize = Shader.PropertyToID("_AOMaskSize");
            HBAOShaderParams._ScreenSize = Shader.PropertyToID("_ScreenSize");
            HBAOShaderParams._ProjInverse = Shader.PropertyToID("_ProjInverse");
            HBAOShaderParams._ViewMatrix = Shader.PropertyToID("_ViewMatrix");
            HBAOShaderParams._HalfResolution = Shader.PropertyToID("_HalfResolution");
        }

        private static TextureHandle CreateAOMaskTexture(RenderGraph graph, int width, int height)
        {
            //Texture description
            TextureDesc textureRTDesc = new TextureDesc(width, height);
            textureRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.R8, false);
            textureRTDesc.depthBufferBits = 0;
            textureRTDesc.msaaSamples = MSAASamples.None;
            textureRTDesc.enableRandomWrite = true;
            //textureRTDesc.clearBuffer = true;
            //textureRTDesc.clearColor = Color.black;
            textureRTDesc.name = "SSAOMask";
            textureRTDesc.filterMode = FilterMode.Point;

            return graph.CreateTexture(textureRTDesc);
        }

        public static void Render_HBAOPass(RenderingData renderingData, TextureHandle depth, TextureHandle normal, out TextureHandle ssaoMask, SSAOSettings ssaoSettings)
        {
            if (HBAOShaderParams._HBAOKernel == -1)
            {
                HBAOShaderParams._HBAOKernel = ssaoSettings.ssao.FindKernel("HBAO");
            }
            using (var builder = renderingData.renderGraph.AddRenderPass<HBAOPassData>("HBAO Pass", out var passData, s_HBAOPassProfiler))
            {
                builder.AllowPassCulling(false);
                passData.cs = ssaoSettings.ssao;
                passData.kernel = HBAOShaderParams._HBAOKernel;
                passData.depth = builder.ReadTexture(depth);
                passData.normal = builder.ReadTexture(normal);
                int AOMaskWidth = ssaoSettings.halfResolution ? (int)(GlobalRenderSettings.screenResolution.width * 0.5f) : (int)GlobalRenderSettings.screenResolution.width;
                int AOMaskHeight = ssaoSettings.halfResolution ? (int)(GlobalRenderSettings.screenResolution.height * 0.5f) : (int)GlobalRenderSettings.screenResolution.height;
                passData.AOMaskSize.x = AOMaskWidth;
                passData.AOMaskSize.y = AOMaskHeight;
                passData.ScreenSize.x = GlobalRenderSettings.screenResolution.width;
                passData.ScreenSize.y = GlobalRenderSettings.screenResolution.height;
                passData.ScreenSize.z = 1.0f / passData.ScreenSize.x;
                passData.ScreenSize.w = 1.0f / passData.ScreenSize.y;
                float projScale = (float)AOMaskHeight / (Mathf.Tan(renderingData.cameraData.camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2.0f);
                passData.HBAOParams.y = ssaoSettings.horizonBias;
                passData.HBAOParams.z = -1.0f / ssaoSettings.radius * ssaoSettings.radius;
                passData.HBAOParams.w = projScale * ssaoSettings.radius * 0.5f;
                passData.HalfResolution = ssaoSettings.halfResolution ? 1.0f : 0;

                Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix; 
                proj = proj * Matrix4x4.Scale(new Vector3(1, 1, -1));
                passData.projInverse = proj.inverse;
                passData.view = renderingData.cameraData.camera.transform.worldToLocalMatrix;
                ssaoMask = CreateAOMaskTexture(renderingData.renderGraph, AOMaskWidth, AOMaskHeight);
                passData.ao = builder.WriteTexture(ssaoMask);

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
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._HBAOParams, data.HBAOParams);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._AOMaskSize, data.AOMaskSize);
                    context.cmd.SetComputeVectorParam(data.cs, HBAOShaderParams._ScreenSize, data.ScreenSize);
                    context.cmd.SetComputeMatrixParam(data.cs, HBAOShaderParams._ProjInverse, data.projInverse);
                    context.cmd.SetComputeMatrixParam(data.cs, HBAOShaderParams._ViewMatrix, data.view);
                    context.cmd.SetComputeFloatParam(data.cs, HBAOShaderParams._HalfResolution, data.HalfResolution);

                    int groupX = Mathf.CeilToInt((float)data.AOMaskSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.AOMaskSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }
        }
    }
}

