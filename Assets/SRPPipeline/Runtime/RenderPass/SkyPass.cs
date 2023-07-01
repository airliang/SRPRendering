using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static Insanity.Atmosphere;

namespace Insanity
{

    public class SkyPassData
    {
        public TextureHandle m_Albedo;
        public Material m_skybox;
    }

    public class PhysicalBaseSkyPassData
    {
        public TextureHandle m_Albedo;
        public RenderTexture m_SkyboxLUT;
        public Material m_skybox;
        public float mieG;
        public float scatteringScaleR;
        public float scatteringScaleM;
        public Color sunLightColor;
    }

    public partial class InsanityPipeline
    {
        // Start is called before the first frame update
        bool hasPrecomputedSkyLut = false;

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

        Atmosphere m_atmosphere = new Atmosphere();

        public void Render_PhysicalBaseSky(CameraData cameraData, RenderGraph graph, DepthPrepassData depthData, InsanityPipelineAsset pipelineAsset)
        {
            SkyboxLUTPassData skyboxLUTPassData = null;
            if (m_atmosphere == null)
            {
                m_atmosphere= new Atmosphere();
                
            }

            RenderTexture skyboxLUT;
            if (!m_atmosphere.IsSkyboxLUTValid() || pipelineAsset.RecalculateSkyLUT)
            {
                skyboxLUTPassData = m_atmosphere.GenerateSkyboxLUT(graph, asset, asset.InsanityPipelineResources.shaders.PrecomputeScattering);
                skyboxLUT = skyboxLUTPassData.skyboxLUT;
                hasPrecomputedSkyLut = false;
                pipelineAsset.RecalculateSkyLUT = false;
            }
            else
                skyboxLUT = m_atmosphere.SkyboxLUT;

            using (var builder = graph.AddRenderPass<PhysicalBaseSkyPassData>("Atmosphere Scattering SkyPass", 
                out var passData, new ProfilingSampler("Atmosphere Scattering SkyPass Profiler")))
            {
                //TextureHandle Depth = builder.ReadTexture(depthData.m_Depth);
                //TextureHandle Albedo = builder.ReadTexture(depthData.m_Albedo);
                passData.m_skybox = pipelineAsset.InsanityPipelineResources.materials.PhysicalBaseSky;
                builder.UseColorBuffer(depthData.m_Albedo, 0);
                builder.UseDepthBuffer(depthData.m_Depth, DepthAccess.Read);
                passData.m_SkyboxLUT = skyboxLUT;
                passData.mieG = pipelineAsset.MieG;
                passData.scatteringScaleR = pipelineAsset.ScatteringScaleR;
                passData.scatteringScaleM = pipelineAsset.ScatteringScaleM;
                passData.sunLightColor = asset.SunLightColor;

                builder.SetRenderFunc((PhysicalBaseSkyPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
                    context.cmd.SetGlobalVector(AtmosphereShaderParameters._BetaRayleigh, Atmosphere.kRayleighScatteringCoef * data.scatteringScaleR);
                    context.cmd.SetGlobalVector(AtmosphereShaderParameters._BetaMie, Atmosphere.kMieScatteringCoef * data.scatteringScaleM);
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._MieG, data.mieG);
                    context.cmd.SetGlobalColor(AtmosphereShaderParameters._SunLightColor, data.sunLightColor);
                    context.cmd.SetViewport(cameraData.camera.pixelRect);
                    context.cmd.SetGlobalTexture(Atmosphere.AtmosphereShaderParameters._SkyboxLUT, data.m_SkyboxLUT);
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
