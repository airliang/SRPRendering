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
        public Texture m_SkyboxLUT;
        public Material m_skybox;
        public float mieG;
        public Vector4 scatteringCoefR;
        public Vector4 scatteringCoefM;
        public Color sunLightColor;
        public bool runderSun = true;
    }

    public partial class RenderPasses
    {
        // Start is called before the first frame update
        static bool hasPrecomputedSkyLut = false;

        public static void Render_SkyPass(RenderingData renderingData, TextureHandle colorTarget, TextureHandle depthTarget, Material skybox)
        {
            //if (m_skyMaterial == null)
            //    m_skyMaterial = Resources.Load<Material>("Materials/Skybox");//CoreUtils.CreateEngineMaterial("Insanity/HDRISky");

            using (var builder = renderingData.renderGraph.AddRenderPass<SkyPassData>("SkyPass", out var passData, new ProfilingSampler("SkyPass Profiler")))
            {
                //TextureHandle Depth = builder.ReadTexture(depthData.m_Depth);
                //TextureHandle Albedo = builder.ReadTexture(depthData.m_Albedo);
                passData.m_skybox = skybox;
                builder.UseColorBuffer(colorTarget, 0);
                builder.UseDepthBuffer(depthTarget, DepthAccess.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((SkyPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetViewport(GlobalRenderSettings.screenResolution);
                    //CoreUtils.DrawFullScreen(context.cmd, data.m_skybox);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.m_skybox, 0, MeshTopology.Triangles, 3);
                });
            }
        }

        //Atmosphere m_atmosphere = new Atmosphere();
        //Vector3 m_sunDirectionLastFrame;

        public static void Render_PhysicalBaseSky(RenderingData renderingData, TextureHandle colorTarget, TextureHandle depthTarget, InsanityPipelineAsset pipelineAsset)
        {
            SkyboxLUTPassData skyboxLUTPassData = null;
            //if (m_atmosphere == null)
            //{
            //    m_atmosphere = new Atmosphere();
            //}
            Atmosphere atmosphere = Atmosphere.Instance;
            AtmosphereResources atmosphereResources = pipelineAsset.AtmosphereResources;
            Texture skyboxLUT;
            if (pipelineAsset.RecalculateSkyLUT)
            {
                skyboxLUTPassData = atmosphere.GenerateSkyboxLUT(renderingData.renderGraph, pipelineAsset, atmosphereResources.PrecomputeScattering);

                if (skyboxLUTPassData.multipleScatteringOrder > 0)
                {
                    skyboxLUTPassData = atmosphere.PrecomputeMultipleScatteringLUT(renderingData.renderGraph,
                        atmosphereResources.PrecomputeScattering, skyboxLUTPassData.skyboxLUT, atmosphereResources.MultipleScatteringOrder);
                }
                skyboxLUT = skyboxLUTPassData.skyboxLUT;

                hasPrecomputedSkyLut = false;
                pipelineAsset.RecalculateSkyLUT = false;
            }
            else
                skyboxLUT = atmosphereResources.SkyboxLUT;//m_atmosphere.SkyboxLUT;
            //Texture3D skyboxLUTAsset = pipelineAsset.AtmosphereResources.SkyboxLUT;
            

            using (var builder = renderingData.renderGraph.AddRenderPass<PhysicalBaseSkyPassData>("Atmosphere Scattering SkyPass", 
                out var passData, new ProfilingSampler("Atmosphere Scattering SkyPass Profiler")))
            {
                //TextureHandle Depth = builder.ReadTexture(depthData.m_Depth);
                //TextureHandle Albedo = builder.ReadTexture(depthData.m_Albedo);
                passData.m_skybox = pipelineAsset.InsanityPipelineResources.materials.PhysicalBaseSky;
                builder.UseColorBuffer(colorTarget, 0);
                builder.UseDepthBuffer(depthTarget, DepthAccess.Read);
                passData.m_SkyboxLUT = skyboxLUT;
                passData.mieG = atmosphereResources.MieG;
                passData.runderSun = atmosphereResources.RenderSun;
                Vector4 rayleightScatteringCoef = atmosphereResources.ScatteringCoefficientRayleigh * 0.000001f * atmosphereResources.ScaleRayleigh;
                Vector4 mieScatteringCoef = atmosphereResources.ScatteringCoefficientMie * 0.00001f * atmosphereResources.ScaleMie;
                passData.scatteringCoefR = rayleightScatteringCoef;
                passData.scatteringCoefM = mieScatteringCoef;
                passData.sunLightColor = pipelineAsset.SunLightColor;
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PhysicalBaseSkyPassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._AtmosphereHeight, Atmosphere.kAtmosphereHeight);
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._EarthRadius, Atmosphere.kEarthRadius);
                    context.cmd.SetGlobalVector(AtmosphereShaderParameters._BetaRayleigh, data.scatteringCoefR);
                    context.cmd.SetGlobalVector(AtmosphereShaderParameters._BetaMie, data.scatteringCoefM);
                    context.cmd.SetGlobalFloat(AtmosphereShaderParameters._MieG, data.mieG);
                    context.cmd.SetGlobalColor(AtmosphereShaderParameters._SunLightColor, data.sunLightColor);
                    context.cmd.SetViewport(GlobalRenderSettings.screenResolution);
                    context.cmd.SetGlobalTexture(Atmosphere.AtmosphereShaderParameters._SkyboxLUT, data.m_SkyboxLUT);
                    data.m_skybox.SetFloat(AtmosphereShaderParameters._RunderSun, data.runderSun ? 1.0f : 0.0f);
                    //CoreUtils.DrawFullScreen(context.cmd, data.m_skybox);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.m_skybox, 0, MeshTopology.Triangles, 3);

                    context.renderContext.ExecuteCommandBuffer(context.cmd);
                    context.cmd.Clear();
                });
            }
        }
    }
}
