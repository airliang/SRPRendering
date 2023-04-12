using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{

    public class SkyPassData
    {
        public TextureHandle m_Albedo;
        public Material m_skybox;
    }

    public partial class InsanityPipeline
    {
        // Start is called before the first frame update

        public void Render_SkyPass(CameraData cameraData, RenderGraph graph, DepthPrepassData depthData, Material skybox)
        {
            //if (m_skyMaterial == null)
            //    m_skyMaterial = Resources.Load<Material>("Materials/Skybox");//CoreUtils.CreateEngineMaterial("Insanity/HDRISky");

            using (var builder = graph.AddRenderPass<SkyPassData>("SkyPass", out var passData, new ProfilingSampler("SkyPass Profiler")))
            {
                //TextureHandle Depth = builder.ReadTexture(depthData.m_Depth);
                //TextureHandle Albedo = builder.ReadTexture(depthData.m_Albedo);
                passData.m_skybox = skybox;
                builder.UseColorBuffer(depthData.m_Albedo, 0);
                builder.UseDepthBuffer(depthData.m_Depth, DepthAccess.Read);
                builder.SetRenderFunc((SkyPassData data, RenderGraphContext context) =>
                {

                    context.cmd.SetViewport(cameraData.camera.pixelRect);
                    CoreUtils.DrawFullScreen(context.cmd, data.m_skybox);

                });
            }
        }

        public void ClearSkyPass()
        {
            //CoreUtils.Destroy(m_skyMaterial);
        }
    }
}
