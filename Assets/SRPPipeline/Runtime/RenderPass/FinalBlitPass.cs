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
        public Material m_finalBlitMaterial;
    }

    public partial class InsanityPipeline
    {
        public Material m_finalBlitMaterial;

        public void FinalBlitPass(CameraData cameraData, RenderGraph graph, ForwardPassData forwardPassData)
        {
            if (m_finalBlitMaterial == null)
                m_finalBlitMaterial = CoreUtils.CreateEngineMaterial("Insanity/Blit");

            using (var builder = graph.AddRenderPass<FinalBlitPassData>("FinalBlitPass", out var passData, new ProfilingSampler("FinalBlitPass Profiler")))
            {
                passData.flip = cameraData.isMainGameView;                
                passData.m_Source = builder.ReadTexture(forwardPassData.m_Albedo);
                passData.m_finalBlitMaterial = m_finalBlitMaterial;
                //TextureHandle dest = builder.WriteTexture(TextureHandle.nullHandle);
                builder.SetRenderFunc((FinalBlitPassData data, RenderGraphContext context) =>
                {
                    m_finalBlitMaterial.SetInt("_FlipY", data.flip ? 1 : 0);
                    //cameraData.UpdateCustomViewConstans(Matrix4x4.identity, Matrix4x4.identity, Vector3.zero);
                    //UpdateGlobalConstantBuffers(cameraData, context.cmd);

                    context.cmd.SetGlobalTexture("_SourceTex", data.m_Source);

                    context.cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    context.cmd.ClearRenderTarget(true, cameraData.camera.cameraType != CameraType.SceneView, cameraData.camera.backgroundColor);

                    //cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    //cmd.SetGlobalMatrix("_ViewMatrix", Matrix4x4.identity);
                    //cmd.SetGlobalMatrix("_ViewProjMatrix", Matrix4x4.identity);
                    context.cmd.SetViewport(cameraData.camera.pixelRect);
                    CoreUtils.DrawFullScreen(context.cmd, data.m_finalBlitMaterial);
                    //cmd.DrawMesh(screenTriangle, Matrix4x4.identity, m_finalBlitMaterial);
                    //cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                });
            }
        }

        public void ClearFinalBlitPass()
        {
            CoreUtils.Destroy(m_finalBlitMaterial);
        }
    }
}

