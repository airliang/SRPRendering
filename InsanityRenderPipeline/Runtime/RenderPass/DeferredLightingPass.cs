using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    

    public partial class RenderPasses
    {
        public class DeferredShadingParams
        {
            public static int _DepthTexture;
            public static int _FinalLighting;
            public static int _AlbedoMetallic;
            public static int _NormalSmoothness;
            public static int _ScreenSize;
            public static int _ProjInverse;
            public static int _ViewInverse;
            public static int _ZBufferParams;
            public static int _DeferredShadingKernel = -1;
        }

        public class DeferredShadingPassData
        {
            public TextureHandle albedoMetallic;
            public TextureHandle normalSmoothness;
            public TextureHandle finalLighting;
            public TextureHandle depthTexture;
            public Matrix4x4 projInverse;
            public Matrix4x4 viewInverse;
            public Vector4 zBufferParams;
            public Vector4 screenSize;
            public bool m_AdditionalLightsEnable;
            public eAdditionalLightCullingFunction m_AdditionalLightCullingFunction;
            public ComputeBuffer m_LightVisibilityIndexBuffer;
            public ComputeShader cs;
            public int kernel;
        }

        static ProfilingSampler s_DeferredShadingPassProfiler = new ProfilingSampler("Deferred Shading Pass Profiler");

        public static void InitializeDeferredShadingParameters()
        {
            DeferredShadingParams._DepthTexture = Shader.PropertyToID("_DepthTexture");
            DeferredShadingParams._FinalLighting = Shader.PropertyToID("_FinalLighting");
            DeferredShadingParams._AlbedoMetallic = Shader.PropertyToID("_AlbedoMetallic");
            DeferredShadingParams._NormalSmoothness = Shader.PropertyToID("_NormalSmoothness");
            DeferredShadingParams._ScreenSize = Shader.PropertyToID("_ScreenSize");
            DeferredShadingParams._ProjInverse = Shader.PropertyToID("_ProjInverse");
            DeferredShadingParams._ViewInverse = Shader.PropertyToID("_ViewInverse");
            DeferredShadingParams._ZBufferParams = Shader.PropertyToID("_ZBufferParams");
        }

        public static void DeferredShadingPass(RenderingData renderingData, TextureHandle albedoMetallic, TextureHandle normalSmoothness,
            TextureHandle depth, TextureHandle output, ComputeShader deferredLighting)
        {
            if (DeferredShadingParams._DeferredShadingKernel == -1)
            {
                DeferredShadingParams._DeferredShadingKernel = deferredLighting.FindKernel("CSMain");
            }

            using (var builder = renderingData.renderGraph.AddRenderPass<DeferredShadingPassData>("Deferred Pass", out var passData, s_DeferredShadingPassProfiler))
            {
                builder.AllowPassCulling(false);
                passData.albedoMetallic = builder.ReadTexture(albedoMetallic);
                passData.normalSmoothness = builder.ReadTexture(normalSmoothness);
                passData.depthTexture = builder.ReadTexture(depth);
                passData.finalLighting = builder.WriteTexture(output);
                Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix * Matrix4x4.Scale(new Vector3(1, 1, -1)); //GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
                passData.projInverse = proj.inverse;
                passData.viewInverse = renderingData.cameraData.camera.transform.localToWorldMatrix; //renderingData.cameraData.mainViewConstants.invViewMatrix;
                passData.viewInverse.SetColumn(3, new Vector4(0, 0, 0, 1));
                //passData.projInverse = renderingData.cameraData.mainViewConstants.invProjMatrix;
                passData.zBufferParams = renderingData.cameraData.zBufferParams;
                passData.screenSize = renderingData.cameraData.screenSize;
                passData.cs = deferredLighting;
                passData.kernel = DeferredShadingParams._DeferredShadingKernel;
                passData.m_AdditionalLightsEnable = renderingData.supportAdditionalLights;
                passData.m_AdditionalLightCullingFunction = InsanityPipeline.asset.AdditonalLightCullingFunction;
                if (passData.m_AdditionalLightsEnable && passData.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased)
                {
                    passData.m_LightVisibilityIndexBuffer = LightCulling.Instance.LightsVisibilityIndexBuffer;
                }

                builder.SetRenderFunc((DeferredShadingPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._AlbedoMetallic, data.albedoMetallic);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._NormalSmoothness, data.normalSmoothness);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._DepthTexture, data.depthTexture);
                    context.cmd.SetComputeTextureParam(data.cs, data.kernel, DeferredShadingParams._FinalLighting, data.finalLighting);
                    context.cmd.SetComputeMatrixParam(data.cs, DeferredShadingParams._ViewInverse, data.viewInverse);
                    context.cmd.SetComputeMatrixParam(data.cs, DeferredShadingParams._ProjInverse, data.projInverse);
                    context.cmd.SetComputeVectorParam(data.cs, DeferredShadingParams._ScreenSize, data.screenSize);
                    context.cmd.SetComputeVectorParam(data.cs, DeferredShadingParams._ZBufferParams, data.zBufferParams);
                    context.cmd.EnableShaderKeyword("_ADDITIONAL_LIGHTS");
                    if (data.m_AdditionalLightsEnable)
                    {
                        CoreUtils.SetKeyword(context.cmd, "_TILEBASED_LIGHT_CULLING", data.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased);
                        if (data.m_AdditionalLightCullingFunction == eAdditionalLightCullingFunction.TileBased)
                        {
                            context.cmd.SetGlobalBuffer(LightCulling.LightCullingShaderParams._LightVisibilityIndexBuffer, data.m_LightVisibilityIndexBuffer);
                        }
                    }

                    int groupX = Mathf.CeilToInt(data.screenSize.x / 8);
                    int groupY = Mathf.CeilToInt(data.screenSize.y / 8);
                    context.cmd.DispatchCompute(data.cs, data.kernel, groupX, groupY, 1);
                });
            }
        }
    }
}
