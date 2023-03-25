using Insanity;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static Insanity.InsanityPipeline;

namespace Insanity
{
    public class ShadowPassData
    {
        public TextureHandle m_Shadowmap;
        public ShadowDrawingSettings shadowDrawSettings;
        public int cascadeCount;
        public Matrix4x4[] m_MainLightWorldToShadowMatrices;
        public float[] m_MainLightShadowDepthRanges;
        public Vector4[] m_CascadeSplitSpheres;
        public int m_ShadowmapWidth = 512;
        public int m_ShadowmapHeight = 512;
        public Vector4 m_ShadowParams;
        public Vector4 m_ShadowmapSize;
        public float m_ShadowDistance;
        public Vector4[] m_ShadowBias;
        public ShaderVariablesGlobal globalCB;
        public Vector3 m_ShadowLightDirection;
        public bool m_SoftShadows = false;
        public bool m_AdaptiveShadowBias = false;
        //public float m_CSMBlendDistance = 0;
        public ShadowType m_ShadowType = ShadowType.PCF;
        public ShadowPCFFilter m_ShadowPCFFilter = ShadowPCFFilter.PCF_5x5;
        public float m_PCSSSoftness = 1.0f;
        public float m_PCSSFilterSamples = 64;
        public float m_PCSSSoftnessFalloff = 2.0f;
        public eGaussianRadius m_ShadowPrefilterGaussianRadius = eGaussianRadius.eGausian3x3;
        public Vector2 m_ShadowExponents = new Vector2(10, 10);
        public float m_LightBleedingReduction = 0.5f;
    }

    public class ShadowInitPassData
    {
        public bool m_supportMainLightShadow = false;
    }

    public class ScreenSpaceShadowPassData
    {
        public TextureHandle m_SSShadowmap;
        public TextureHandle m_Shadowmap;
        public TextureHandle m_Depth;
    }

    public class ShadowRequest
    {
        public Matrix4x4 view;
        // Use the y flipped device projection matrix as light projection matrix
        public Matrix4x4 deviceProjectionYFlip = Matrix4x4.identity;
        public Matrix4x4 deviceProjection = Matrix4x4.identity;
        public Matrix4x4 projection = Matrix4x4.identity;
        public Matrix4x4 shadowToWorld = Matrix4x4.identity;
        public Matrix4x4 worldToShadow = Matrix4x4.identity;
        public Vector3 position;
        public Vector4 zBufferParam;
        // Warning: these viewport fields are updated by ProcessShadowRequests and are invalid before
        public Rect atlasViewport;
        public bool zClip;
        public Vector4[] frustumPlanes;
        public float slopeBias;

        // TODO: Remove these field once scriptable culling is here (currently required by ScriptableRenderContext.DrawShadows)
        //public int lightIndex;
        public ShadowSplitData splitData;
        // end
        // Determine in which atlas the shadow will be rendered


        // PCSS parameters
        public float shadowSoftness;
        public int blockerSampleCount;
        public int filterSampleCount;
        public float minFilterSize;

        public int offsetX;
        public int offsetY;
        public int resolution;
    }

    public class ShadowManager
    {
        // Start is called before the first frame update
        ShadowRequest[] m_ShadowRequests;
        protected const int k_MaxCascades = 4;
        protected const int k_ShadowmapBufferBits = 16;
        protected int m_ShadowmapWidth = 512;
        protected int m_ShadowmapHeight = 512;
        protected int m_ShadowCasterCascadesCount = 1;
        protected int m_ShadowResolution = 512;  //
        protected int m_mainLightShadowIndex = -1;
        Vector3 m_mainLightShadowCascadesSplit;
        FilterMode m_FilterMode = FilterMode.Bilinear;
        DepthBits m_DepthBufferBits = DepthBits.Depth16;
        RenderTextureFormat m_Format = RenderTextureFormat.Shadowmap;
        string m_Name = "Shadow Atlas Map";
        protected TextureHandle m_ShadowMap;
        protected TextureHandle m_SSShadowMap;    //screen space shadowmap
        //MainLightShadowVariablesGlobal m_MainLightShadowVariablesGlobal = new MainLightShadowVariablesGlobal();
        Light m_MainLight;
        Matrix4x4[] m_MainLightWorldToShadowMatrices;
        float[] m_MainLightShadowDepthRanges;
        Vector4[] m_CascadeSplitSpheres;
        Vector4[] m_ShadowBias;
        Material m_ScreenSpaceShadowsMaterial;
        bool m_supportSoftShadow = true;
        ShadowSettings m_shadowSetting;
        IShadowCaster m_shadowCaster;
        PrefilterShadowPass m_prefilterPass;

        private static class MainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowDepthRange;
            public static int _CascadeShadowSplitSpheres;
            //public static int _CascadeShadowSplitSpheres1;
            //public static int _CascadeShadowSplitSpheres2;
            //public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;
            //public static int _ShadowOffset0;
            //public static int _ShadowOffset1;
            //public static int _ShadowOffset2;
            //public static int _ShadowOffset3;
            public static int _ShadowParams;
            public static int _ShadowmapSize;
            public static int _ShadowBias;
            public static int _ShadowDistance;
        }

        private static class PCSSConstantBuffer
        {
            public static int _PCSSSoftness;
            public static int _PCF_Samples;
            public static int _SoftnessFalloff;
        }

        private static class VSMConstantBuffer
        {
            public static int _ExponentConstants;
            public static int _LightBleedingReduction;
        }

        public ShadowManager()
        {
            m_ShadowRequests = new ShadowRequest[k_MaxCascades];
            for (int i = 0; i < k_MaxCascades; i++)
            {
                m_ShadowRequests[i] = new ShadowRequest();
            }
            m_MainLightWorldToShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_MainLightShadowDepthRanges = new float[k_MaxCascades + 1];
            m_CascadeSplitSpheres = new Vector4[k_MaxCascades];
            m_ShadowBias = new Vector4[k_MaxCascades];

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._ShadowDepthRange = Shader.PropertyToID("_MainLightShadowDepthRange");
            
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres = Shader.PropertyToID("_CascadeShadowSplitSpheres");
            //MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            //MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            //MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            //MainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_MainLightShadowOffset0");
            //MainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_MainLightShadowOffset1");
            //MainLightShadowConstantBuffer._ShadowOffset2 = Shader.PropertyToID("_MainLightShadowOffset2");
            //MainLightShadowConstantBuffer._ShadowOffset3 = Shader.PropertyToID("_MainLightShadowOffset3");
            MainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            MainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_MainLightShadowmapSize");
            MainLightShadowConstantBuffer._ShadowBias = Shader.PropertyToID("_ShadowBias");
            MainLightShadowConstantBuffer._ShadowDistance = Shader.PropertyToID("_ShadowDistance");
            PCSSConstantBuffer._PCSSSoftness = Shader.PropertyToID("_Softness");
            PCSSConstantBuffer._PCF_Samples = Shader.PropertyToID("_PCF_Samples");
            PCSSConstantBuffer._SoftnessFalloff = Shader.PropertyToID("_SoftnessFalloff");
            VSMConstantBuffer._ExponentConstants = Shader.PropertyToID("_ShadowExponents");
            VSMConstantBuffer._LightBleedingReduction = Shader.PropertyToID("_LightBleedingReduction");
        }

        public int ShadowMapWidth
        {
            get
            {
                return m_ShadowmapWidth;
            }
        }

        public int ShadowMapHeight
        {
            get
            {
                return m_ShadowmapHeight;
            }
        }


        public ShadowRequest GetShadowRequest(int requestIndex)
        {
            return m_ShadowRequests[requestIndex];
        }

        public void UpdateDirectionalShadowRequest(ShadowRequest shadowRequest, ShadowSettings shadowSettings, VisibleLight visibleLight, ref CullingResults cullResults, 
            int requestIndex, int lightIndex, Vector3 cameraPos, out Matrix4x4 invViewProjection)
        {
            m_supportSoftShadow = shadowSettings.supportSoftShadow;
            Vector4 cullingSphere;
            
            m_mainLightShadowIndex = lightIndex;
            m_MainLight = visibleLight.light;
            float nearPlaneOffset = m_MainLight.shadowNearPlane;//QualitySettings.shadowNearPlaneOffset;
            int shadowResolution = GetShadowResolution();
            ShadowUtils.ExtractDirectionalLightData(
                visibleLight, shadowResolution, (uint)requestIndex, shadowSettings.mainLightShadowCascadesCount,
                m_mainLightShadowCascadesSplit, nearPlaneOffset, cullResults, lightIndex,
                out shadowRequest.view, out invViewProjection, out shadowRequest.projection,
                out shadowRequest.deviceProjection, out shadowRequest.deviceProjectionYFlip, out shadowRequest.splitData
            );

            cullingSphere = shadowRequest.splitData.cullingSphere;

            int offsetX = (requestIndex % 2) * shadowResolution;
            int offsetY = (requestIndex / 2) * shadowResolution;
            shadowRequest.atlasViewport = new Rect(offsetX, offsetY, shadowResolution, shadowResolution);
            shadowRequest.offsetX = offsetX;
            shadowRequest.offsetY = offsetY;
            shadowRequest.resolution = shadowResolution;

            // Camera relative for directional light culling sphere
            if (CameraData.s_CameraRelativeRendering != 0)
            {
                cullingSphere.x -= cameraPos.x;
                cullingSphere.y -= cameraPos.y;
                cullingSphere.z -= cameraPos.z;
            }
            m_CascadeSplitSpheres[requestIndex] = cullingSphere;
            //UpdateCascade(requestIndex, cullingSphere);
        }

        public int CascadeCount
        {
            get
            {
                return m_ShadowCasterCascadesCount;
            }
        }

        void UpdateCascade(int cascadeIndex, Vector4 cullingSphere)
        {
            //if (cullingSphere.w != float.NegativeInfinity)
            //{
            //    cullingSphere.w *= cullingSphere.w;
            //}
            m_CascadeSplitSpheres[cascadeIndex] = cullingSphere;
        }

        public void Setup(ShadowSettings shadowSettings)
        {
            m_ShadowCasterCascadesCount = shadowSettings.mainLightShadowCascadesCount;
            m_ShadowmapWidth = shadowSettings.mainLightResolution;
            m_ShadowmapHeight = (m_ShadowCasterCascadesCount == 2) ?
                shadowSettings.mainLightResolution >> 1 :
                shadowSettings.mainLightResolution;

            switch (m_ShadowCasterCascadesCount)
            {
                case 1:
                    m_mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;
                case 2:
                    m_mainLightShadowCascadesSplit = new Vector3(shadowSettings.cascade2Split, 1.0f, 0.0f);
                    break;

                default:
                    m_mainLightShadowCascadesSplit = shadowSettings.cascade4Split;
                    break;
            }

            m_shadowSetting = shadowSettings;
        }

        public int GetShadowResolution()
        {
            m_ShadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(m_ShadowmapWidth,
                    m_ShadowmapHeight, m_ShadowCasterCascadesCount);
            return m_ShadowResolution;
        }

        public void SetShadowRequestSetting(ShadowRequest shadowRequest, int cascadeIndex, Vector3 cameraPos, Matrix4x4 invViewProjection)
        {
            CoreMatrixUtils.MatrixTimesTranslation(ref shadowRequest.view, cameraPos);
            CoreMatrixUtils.TranslationTimesMatrix(ref invViewProjection, -cameraPos);
            shadowRequest.position = new Vector3(shadowRequest.view.m03, shadowRequest.view.m13, shadowRequest.view.m23);
            shadowRequest.shadowToWorld = invViewProjection.transpose;
            shadowRequest.worldToShadow = ShadowUtils.GetWorldToShadowTransform(shadowRequest.deviceProjection, shadowRequest.view, m_ShadowCasterCascadesCount,
                cascadeIndex, m_ShadowmapWidth, m_ShadowmapHeight, shadowRequest.resolution, shadowRequest.offsetX, shadowRequest.offsetY);//invViewProjection;
        }

        public TextureDesc GetShadowMapTextureDesc(ShadowType shadowType)
        {
            if (shadowType == ShadowType.VSM || shadowType == ShadowType.EVSM)
            {
                return new TextureDesc(m_ShadowmapWidth, m_ShadowmapHeight)
                {
                    filterMode = m_FilterMode,
                    depthBufferBits = DepthBits.None,
                    //isShadowMap = false,
                    name = m_Name,
                    wrapMode = TextureWrapMode.Clamp,
                    colorFormat = ShadowSettings.GetShadowmapFormat(shadowType),
                    clearColor = shadowType == ShadowType.VSM ? Color.white : new Color(65504.0f, 65504.0f, 0, 0),
                    autoGenerateMips = false,
                    useMipMap = false,
                    clearBuffer = true
                };
            }
            else
            {
                return new TextureDesc(m_ShadowmapWidth, m_ShadowmapHeight)
                {
                    filterMode = m_FilterMode,
                    depthBufferBits = m_DepthBufferBits,
                    isShadowMap = true,
                    name = m_Name,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
        }

        public TextureDesc GetScreenSpaceShadowMapDesc(int width, int height)
        {
            TextureDesc ssShadowmapDesc = new TextureDesc(width, height);
            ssShadowmapDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, false);
            ssShadowmapDesc.depthBufferBits = 0;
            ssShadowmapDesc.msaaSamples = MSAASamples.None;
            ssShadowmapDesc.enableRandomWrite = false;
            ssShadowmapDesc.clearBuffer = true;
            ssShadowmapDesc.clearColor = Color.black;
            return ssShadowmapDesc;
        }

        TextureHandle GetShadowMap(RenderGraph renderGraph, ShadowType shadowType)
        {
            //return renderGraph.CreateTexture(GetShadowMapTextureDesc());
            renderGraph.CreateTextureIfInvalid(GetShadowMapTextureDesc(shadowType), ref m_ShadowMap);
            //m_ShadowMap = renderGraph.CreateTexture(GetShadowMapTextureDesc(shadowType));
            return m_ShadowMap;
        }

        TextureHandle GetShadowMapDepthBuffer(RenderGraph renderGraph)
        {
            //Texture description
            TextureDesc colorRTDesc = new TextureDesc(m_ShadowmapWidth, m_ShadowmapHeight);
            colorRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Depth, false);
            colorRTDesc.depthBufferBits = DepthBits.Depth16;
            colorRTDesc.msaaSamples = MSAASamples.None;
            colorRTDesc.enableRandomWrite = false;
            colorRTDesc.clearBuffer = true;
            colorRTDesc.clearColor = Color.black;
            colorRTDesc.name = "ShadowMapDepth";

            return renderGraph.CreateTexture(colorRTDesc);
        }

        TextureHandle GetScreenSpaceShadowMap(RenderGraph renderGraph, Camera camera)
        {
            renderGraph.CreateTextureIfInvalid(GetScreenSpaceShadowMapDesc(camera.pixelWidth, camera.pixelHeight), ref m_SSShadowMap);
            return m_SSShadowMap;
        }

        public void ExecuteShadowInitPass(RenderGraph renderGraph)
        {
            if (m_shadowSetting.supportsMainLightShadows)
                return;
            using (var builder = renderGraph.AddRenderPass<ShadowInitPassData>("Init Shadow", out var passData, new ProfilingSampler("Init Shadow Profiler")))
            {
                passData.m_supportMainLightShadow = m_shadowSetting.supportsMainLightShadows;

                builder.SetRenderFunc(
                    (ShadowInitPassData data, RenderGraphContext ctx) =>
                    {
                        if (!data.m_supportMainLightShadow)
                        {
                            Shader.DisableKeyword("_MAIN_LIGHT_SHADOWS");
                            Shader.DisableKeyword("_SHADOWS_SOFT");
                        }
                    });
             }
        }

        private void SetShadowCasterPassKeywords(CommandBuffer cmd, ShadowPassData passData)
        {
            CoreUtils.SetKeyword(cmd, "_ADAPTIVE_SHADOW_BIAS", passData.m_AdaptiveShadowBias);
            CoreUtils.SetKeyword(cmd, "_SHADOW_VSM", passData.m_ShadowType == ShadowType.VSM);
            CoreUtils.SetKeyword(cmd, "_SHADOW_EVSM", passData.m_ShadowType == ShadowType.EVSM);
        }

        private void SetReceiverShadowKeywords(CommandBuffer cmd, ShadowPassData passData)
        {
            CoreUtils.SetKeyword(cmd, "_MAIN_LIGHT_SHADOWS", true);
            CoreUtils.SetKeyword(cmd, "_SHADOWS_SOFT", passData.m_SoftShadows);
            CoreUtils.SetKeyword(cmd, "_MAIN_LIGHT_SHADOWS_CASCADE", passData.cascadeCount > 1);
            CoreUtils.SetKeyword(cmd, "_SHADOW_PCSS", passData.m_ShadowType == ShadowType.PCSS);
            CoreUtils.SetKeyword(cmd, "_SHADOW_VSM", passData.m_ShadowType == ShadowType.VSM);
            CoreUtils.SetKeyword(cmd, "_SHADOW_EVSM", passData.m_ShadowType == ShadowType.EVSM);
        }

        public bool NeedPrefilterShadowmap(ShadowPassData passData)
        {
            return passData.m_ShadowType == ShadowType.VSM || 
                passData.m_ShadowType == ShadowType.EVSM || 
                passData.m_ShadowType == ShadowType.MSM;
        }

        public ShadowPassData RenderShadowMap(RenderGraph renderGraph, CullingResults cullResults, ShaderVariablesGlobal globalCB)
        {
            Bounds bounds;
            bool doShadow = m_MainLight.shadows != LightShadows.None && cullResults.GetShadowCasterBounds(m_mainLightShadowIndex, out bounds);
            if (!doShadow)
                return null;

            using (var builder = renderGraph.AddRenderPass<ShadowPassData>("Render Shadow Maps", out var passData, new ProfilingSampler("ShadowPass Profiler")))
            {
                for (int i = 0; i < m_ShadowCasterCascadesCount; ++i)
                {
                    m_MainLightWorldToShadowMatrices[i] = m_ShadowRequests[i].worldToShadow;
                    float a = m_ShadowRequests[i].projection[2, 2];
                    float b = m_ShadowRequests[i].projection[2, 3];
                    float near = (b + 1) / a;
                    float far = (b - 1) / a;
                    m_MainLightShadowDepthRanges[i] = far - near;

                    m_ShadowBias[i] = ShadowUtils.GetShadowBias(m_MainLight, m_mainLightShadowIndex, m_shadowSetting, m_ShadowRequests[i].projection, m_ShadowResolution);
                }

                TextureHandle shadowmap = GetShadowMap(renderGraph, m_shadowSetting.shadowType);
                if (m_shadowSetting.shadowType == ShadowType.VSM || m_shadowSetting.shadowType == ShadowType.EVSM)
                {
                    builder.UseDepthBuffer(GetShadowMapDepthBuffer(renderGraph), DepthAccess.ReadWrite);
                    passData.m_Shadowmap = builder.UseColorBuffer(shadowmap, 0);
                }
                else
                    passData.m_Shadowmap = builder.WriteTexture(shadowmap);
                //builder.UseDepthBuffer(passData.m_Shadowmap, DepthAccess.ReadWrite);
                passData.shadowDrawSettings = new ShadowDrawingSettings(cullResults, m_mainLightShadowIndex, BatchCullingProjectionType.Orthographic);
                passData.cascadeCount = m_ShadowCasterCascadesCount;
                passData.m_MainLightWorldToShadowMatrices = m_MainLightWorldToShadowMatrices;
                passData.m_MainLightShadowDepthRanges = m_MainLightShadowDepthRanges;
                passData.m_CascadeSplitSpheres = m_CascadeSplitSpheres;
                passData.m_ShadowmapWidth = m_ShadowmapWidth;
                passData.m_ShadowmapHeight = m_ShadowmapHeight;

                bool softShadows = m_MainLight.shadows == LightShadows.Soft && m_supportSoftShadow;
                passData.m_SoftShadows = softShadows;

                float invShadowAtlasWidth = 1.0f / m_ShadowmapWidth;
                float invShadowAtlasHeight = 1.0f / m_ShadowmapHeight;
                float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
                float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
                float softShadowsProp = softShadows ? 1.0f : 0.0f;
                passData.m_ShadowParams = new Vector4(m_MainLight.shadowStrength, softShadowsProp, m_shadowSetting.csmBlendEnable ? m_shadowSetting.csmBlendDistance : 0, (float)passData.cascadeCount);
                passData.m_ShadowmapSize = new Vector4(invShadowAtlasWidth,
                                invShadowAtlasHeight,
                                m_ShadowmapWidth, m_ShadowmapHeight);
                passData.globalCB = globalCB;
                passData.m_ShadowBias = m_ShadowBias;
                Vector3 lightDirection = -m_MainLight.transform.localToWorldMatrix.GetColumn(2);
                passData.m_ShadowLightDirection = lightDirection;
                passData.m_AdaptiveShadowBias = m_shadowSetting.adaptiveShadowBias;
                passData.m_PCSSSoftness = m_shadowSetting.pcssSoftness / 64.0f / Mathf.Sqrt(m_shadowSetting.maxShadowDistance);
                passData.m_ShadowType = m_shadowSetting.shadowType;
                passData.m_PCSSSoftnessFalloff = m_shadowSetting.pcssSoftnessFalloff;
                passData.m_ShadowDistance = m_shadowSetting.maxShadowDistance;
                passData.m_ShadowPrefilterGaussianRadius = m_shadowSetting.prefilterGaussianRadius;
                passData.m_ShadowExponents = m_shadowSetting.exponentialConstants;
                passData.m_LightBleedingReduction = m_shadowSetting.lightBleedingReduction;

                builder.SetRenderFunc(
                    (ShadowPassData data, RenderGraphContext ctx) =>
                    {
                        if (data.m_ShadowType != ShadowType.VSM && data.m_ShadowType != ShadowType.EVSM)
                        {
                            ctx.cmd.SetRenderTarget(data.m_Shadowmap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                            ctx.cmd.ClearRenderTarget(true, true, Color.black);
                        }

                        //ConstantBuffer.PushGlobal(ctx.cmd, m_MainLightShadowVariablesGlobal, ShaderIDs._MainlightShadowVariablesGlobal);
                        //CoreUtils.SetKeyword(ctx.cmd, "_ADAPTIVE_SHADOW_BIAS", data.m_AdaptiveShadowBias);
                        SetShadowCasterPassKeywords(ctx.cmd, data);
                        if (data.m_ShadowType == ShadowType.EVSM || data.m_ShadowType == ShadowType.VSM)
                        {
                            ctx.cmd.SetGlobalVector(VSMConstantBuffer._ExponentConstants, data.m_ShadowExponents);
                            ctx.cmd.SetGlobalFloat(VSMConstantBuffer._LightBleedingReduction, data.m_LightBleedingReduction);
                        }

                        for (int i = 0; i < data.cascadeCount; ++i)
                        {
                            ShadowRequest shadowRequest = m_ShadowRequests[i];
                            ctx.cmd.SetGlobalDepthBias(1.0f, shadowRequest.slopeBias);
                            ctx.cmd.SetViewport(shadowRequest.atlasViewport);
                            ctx.cmd.EnableScissorRect(new Rect((int)shadowRequest.atlasViewport.xMin + 4, (int)shadowRequest.atlasViewport.yMin + 4, m_ShadowResolution - 8, m_ShadowResolution - 8));

                            data.shadowDrawSettings.lightIndex = m_mainLightShadowIndex;//shadowRequest.lightIndex;
                            data.shadowDrawSettings.splitData = shadowRequest.splitData;
                            data.shadowDrawSettings.projectionType = BatchCullingProjectionType.Orthographic; //shadowRequest.projectionType;

                            // Setup matrices for shadow rendering:
                            Matrix4x4 view = shadowRequest.view;
                            Matrix4x4 proj = shadowRequest.deviceProjectionYFlip;//shadowRequest.deviceProjectionYFlip;
                            Matrix4x4 viewProjection = proj * view;
                            data.globalCB._ViewMatrix = view;
                            data.globalCB._InvViewMatrix = view.inverse;
                            data.globalCB._ProjMatrix = proj;
                            data.globalCB._InvProjMatrix = proj.inverse;
                            data.globalCB._ViewProjMatrix = viewProjection;
                            data.globalCB._InvViewProjMatrix = viewProjection.inverse;

                            ConstantBuffer.PushGlobal(ctx.cmd, data.globalCB, ShaderIDs._ShaderVariablesGlobal);

                            //shadowBias.Scale
                            ctx.cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowBias, data.m_ShadowBias[i]);
                            ctx.cmd.SetGlobalVector("_LightDirection", data.m_ShadowLightDirection);

                            // TODO: remove this execute when DrawShadows will use a CommandBuffer
                            ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                            ctx.cmd.Clear();

                            ctx.renderContext.DrawShadows(ref data.shadowDrawSettings);
                        }
                        //ctx.cmd.SetGlobalFloat(HDShaderIDs._ZClip, 1.0f);   // Re-enable zclip globally

                        ctx.cmd.DisableScissorRect();
                        ctx.cmd.SetGlobalDepthBias(0.0f, 0.0f);             // Reset depth bias.
                        //CoreUtils.SetKeyword(ctx.cmd, "_MAIN_LIGHT_SHADOWS", true);
                        //CoreUtils.SetKeyword(ctx.cmd, "_SHADOWS_SOFT", data.m_SoftShadows);
                        //CoreUtils.SetKeyword(ctx.cmd, "_MAIN_LIGHT_SHADOWS_CASCADE", data.cascadeCount > 1);
                        //CoreUtils.SetKeyword(ctx.cmd, "_SHADOW_PCSS", data.m_ShadowType == ShadowType.PCSS);
                        SetReceiverShadowKeywords(ctx.cmd, data);
                        //ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                        //ctx.cmd.Clear();
                        SetupMainLightShadowReceiverConstants(ctx.cmd, data);
                        
                    });

                return passData;
            }
        }

        public PrefilterShadowPassData PrefilterShadowPass(RenderGraph renderGraph, ShadowPassData passData)
        {
            if (m_prefilterPass == null)
            {
                m_prefilterPass = new PrefilterShadowPass();
            }
            return m_prefilterPass.PrefilterShadowmap(renderGraph, passData);
        }

        public ScreenSpaceShadowPassData Render_ScreenSpaceShadow(RenderGraph renderGraph, Camera camera, TextureHandle shadowMap, TextureHandle mainCameraDepth)
        {
            if (m_ScreenSpaceShadowsMaterial == null)
            {
                m_ScreenSpaceShadowsMaterial = CoreUtils.CreateEngineMaterial("Insanity/ScreenSpaceShadow");
            }

            if (m_ScreenSpaceShadowsMaterial == null)
                return null;
            using (var builder = renderGraph.AddRenderPass<ScreenSpaceShadowPassData>("Render ScreenSpace Shadow Maps", out var passData, new ProfilingSampler("ScreenSpace Shadow Pass Profiler")))
            {
                passData.m_SSShadowmap = builder.ReadWriteTexture(GetScreenSpaceShadowMap(renderGraph, camera)); //builder.UseColorBuffer(GetScreenSpaceShadowMap(renderGraph), 0);
                passData.m_Depth = builder.ReadTexture(mainCameraDepth);
                passData.m_Shadowmap = builder.ReadTexture(shadowMap);


                //builder.ReadTexture(passData.m_SSShadowmap);
                //builder.Us
                builder.SetRenderFunc(
                    (ScreenSpaceShadowPassData data, RenderGraphContext ctx) =>
                    {
                        ctx.cmd.SetRenderTarget(passData.m_SSShadowmap);
                        ctx.cmd.ClearRenderTarget(true, true, Color.black);

                        //m_ScreenSpaceShadowsMaterial.SetTexture("_CameraDepthTexture", data.m_Depth);
                        ctx.cmd.SetGlobalTexture("_CameraDepthTexture", data.m_Depth);

                        CoreUtils.DrawFullScreen(ctx.cmd, m_ScreenSpaceShadowsMaterial);

                        ctx.renderContext.ExecuteCommandBuffer(ctx.cmd);
                        ctx.cmd.Clear();
                        ctx.cmd.SetGlobalTexture("_ScreenSpaceShadowmapTexture", data.m_SSShadowmap);
                    });
                return passData;
            }
        }

        public void Clear()
        {
            CoreUtils.Destroy(m_ScreenSpaceShadowsMaterial);
        }

        public void SetupMainLightShadowReceiverConstants(CommandBuffer cmd, ShadowPassData data)
        {
            //setup receive shadow constant buffer
            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, data.m_MainLightWorldToShadowMatrices);
            cmd.SetGlobalFloatArray(MainLightShadowConstantBuffer._ShadowDepthRange, data.m_MainLightShadowDepthRanges);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams, data.m_ShadowParams);
            cmd.SetGlobalFloat(MainLightShadowConstantBuffer._ShadowDistance, data.m_ShadowDistance);
            if (data.cascadeCount > 1)
            {
                cmd.SetGlobalVectorArray(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres,
                    data.m_CascadeSplitSpheres);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                    data.m_CascadeSplitSpheres[0].w * data.m_CascadeSplitSpheres[0].w,
                    data.m_CascadeSplitSpheres[1].w * data.m_CascadeSplitSpheres[1].w,
                    data.m_CascadeSplitSpheres[2].w * data.m_CascadeSplitSpheres[2].w,
                    data.m_CascadeSplitSpheres[3].w * data.m_CascadeSplitSpheres[3].w));
            }

            if (data.m_ShadowType == ShadowType.PCSS)
            {
                cmd.SetGlobalFloat(PCSSConstantBuffer._PCSSSoftness, data.m_PCSSSoftness);
                cmd.SetGlobalFloat(PCSSConstantBuffer._PCF_Samples, data.m_PCSSFilterSamples);
                cmd.SetGlobalFloat(PCSSConstantBuffer._SoftnessFalloff, data.m_PCSSSoftnessFalloff);
            }

            // moved outside, this is needed for expotional shadow maps
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowmapSize, data.m_ShadowmapSize);
            cmd.SetGlobalTexture("_ShadowMap", data.m_Shadowmap);
        }
    }
}

