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
        public TextureHandle m_Dest;
        public bool flip;
        public bool tonemapping = false;
        public float exposure = 1.0f;
        public Material m_finalBlitMaterial;
    }

    public partial class RenderPasses
    {
        //public static Material m_finalBlitMaterial;

        public static void FinalBlitPass(RenderingData renderingData, TextureHandle sourceTarget, TextureHandle destTarget, Material finalBlitMaterial)
        {
            //if (finalBlitMaterial == null)
            //    finalBlitMaterial = CoreUtils.CreateEngineMaterial("Insanity/Blit");

            using (var builder = renderingData.renderGraph.AddRenderPass<FinalBlitPassData>("FinalBlitPass", out var passData, new ProfilingSampler("FinalBlitPass Profiler")))
            {
                passData.flip = renderingData.cameraData.isMainGameView;                
                passData.m_Source = builder.ReadTexture(sourceTarget);
                passData.m_Dest = builder.UseColorBuffer(destTarget, 0);
                passData.m_finalBlitMaterial = finalBlitMaterial;
                passData.tonemapping = GlobalRenderSettings.HDREnable;
                passData.exposure = GlobalRenderSettings.HDRExposure;
                finalBlitMaterial.SetInt("_FlipY", passData.flip ? 1 : 0);
                finalBlitMaterial.SetFloat("_Exposure", passData.exposure);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    CoreUtils.SetKeyword(context.cmd, "_TONEMAPPING", passData.tonemapping);

                    
                    //context.cmd.SetGlobalTexture("_SourceTex", data.m_Source);

                    context.cmd.SetRenderTarget(data.m_Dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    context.cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                    context.cmd.ClearRenderTarget(true, renderingData.cameraData.camera.cameraType != CameraType.SceneView, renderingData.cameraData.camera.backgroundColor);

                    data.m_finalBlitMaterial.SetTexture("_SourceTex", data.m_Source);
                    //context.cmd.DrawMesh(s_FullScreenTriangleMesh, Matrix4x4.identity, data.m_finalBlitMaterial);

                    context.cmd.DrawProcedural(Matrix4x4.identity, data.m_finalBlitMaterial, 0, MeshTopology.Triangles, 3, 1);
                    
                    //CoreUtils.DrawFullScreen(context.cmd, data.m_finalBlitMaterial);

                });
            }
        }
    }
}

