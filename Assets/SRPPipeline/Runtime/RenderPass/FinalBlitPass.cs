using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static Insanity.InsanityPipeline;

namespace Insanity
{

    public class FinalBlitPassData
    {
        public TextureHandle m_Source;
        //public TextureHandle m_Color;
        public bool flip;
        public bool tonemapping = false;
        public float exposure = 1.0f;
        public Material m_finalBlitMaterial;
    }

    public partial class RenderPasses
    {
        //public static Material m_finalBlitMaterial;

        public static void FinalBlitPass(RenderingData renderingData, TextureHandle colorTarget, Material finalBlitMaterial)
        {
            if (finalBlitMaterial == null)
                finalBlitMaterial = CoreUtils.CreateEngineMaterial("Insanity/Blit");

            using (var builder = renderingData.renderGraph.AddRenderPass<FinalBlitPassData>("FinalBlitPass", out var passData, new ProfilingSampler("FinalBlitPass Profiler")))
            {
                passData.flip = renderingData.cameraData.isMainGameView;                
                passData.m_Source = builder.ReadTexture(colorTarget);
                passData.m_finalBlitMaterial = finalBlitMaterial;
                passData.tonemapping = GlobalRenderSettings.HDREnable;
                passData.exposure = GlobalRenderSettings.HDRExposure;

                builder.AllowPassCulling(false);
                //TextureHandle dest = builder.WriteTexture(TextureHandle.nullHandle);
                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    CoreUtils.SetKeyword(context.cmd, "_TONEMAPPING", passData.tonemapping);
                    finalBlitMaterial.SetInt("_FlipY", data.flip ? 1 : 0);
                    finalBlitMaterial.SetFloat("_Exposure", data.exposure);
                    //cameraData.UpdateCustomViewConstans(Matrix4x4.identity, Matrix4x4.identity, Vector3.zero);
                    //UpdateGlobalConstantBuffers(cameraData, context.cmd);

                    context.cmd.SetGlobalTexture("_SourceTex", data.m_Source);

                    context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    context.cmd.ClearRenderTarget(true, renderingData.cameraData.camera.cameraType != CameraType.SceneView, renderingData.cameraData.camera.backgroundColor);

                    //cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    //cmd.SetGlobalMatrix("_ViewMatrix", Matrix4x4.identity);
                    //cmd.SetGlobalMatrix("_ViewProjMatrix", Matrix4x4.identity);
                    context.cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                    CoreUtils.DrawFullScreen(context.cmd, data.m_finalBlitMaterial);
                    //cmd.DrawMesh(screenTriangle, Matrix4x4.identity, m_finalBlitMaterial);
                    //cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                });
            }
        }
    }
}

