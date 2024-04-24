using Insanity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;


namespace Insanity
{
    public enum eShadowCascadesOption
    {
        NoCascades,
        TwoCascades,
        FourCascades,
    }


    public enum eShadowQuality
    {
        Disabled,
        HardShadows,
        SoftShadows,
    }

    public enum eShadowType
    {
        PCF,
        PCSS,
        VSM,
        EVSM,
        MSM,
    }

    public enum eShadowPCFFilter
    {
        PCF_None,
        PCF_3x3,
        PCF_5x5,
        PCF_7x7,
    }

    public enum eShadowResolution
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048
    }

    public enum eAdditionalLightCullingFunction
    {
        Default,
        TileBased,
        ClusterBased
    }

    public enum DebugViewMode
    {
        None,
        TileBasedVisibleCount,
        Depth,
        LinearDepth,
        Normal,
        TriangleOverdraw
    }

    public enum MsaaQuality
    {
        /// <summary>
        /// Disables MSAA.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// Use this for 2 samples per pixel.
        /// </summary>
        _2x = 2,

        /// <summary>
        /// Use this for 4 samples per pixel.
        /// </summary>
        _4x = 4,

        /// <summary>
        /// Use this for 8 samples per pixel.
        /// </summary>
        _8x = 8
    }

    [ExecuteInEditMode]
    public class InsanityPipelineAsset : RenderPipelineAsset
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/InsanityPipeline", priority = 1)]
        static void CreateInsanityPipelineAsset()
        {
            var instance = ScriptableObject.CreateInstance<InsanityPipelineAsset>();
            UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRPPipeline/InsanityPipeline.asset");
        }

        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/InsanityPipelineResource", priority = 1)]
        static void CreateInsanityPipelineResources()
        {
            var newAsset = CreateInstance<InsanityPipelineResources>();
            string pathName = AssetDatabase.GetAssetPath(Selection.activeObject) + "/InsanityPipelineResources.asset";
            newAsset.name = Path.GetFileName(pathName);
            UnityEditor.AssetDatabase.CreateAsset(newAsset, pathName);
        }
#endif
        InsanityRenderer m_Renderer = null;

        //use for shader preprocess filter the shaders that is not tagged as this name.
        public override string renderPipelineShaderTag => InsanityPipeline.k_ShaderTagName;

        public InsanityRenderer Renderer
        {
            get { return m_Renderer; }
        }

        protected override RenderPipeline CreatePipeline()
        {
            DestroyRenderers();
            var pipeline = new InsanityPipeline();
            CreateRenderers(pipeline);
            return pipeline;
        }

        #region Rendering
        [SerializeField] bool m_HDREnable = false;
        [SerializeField] float m_Exposure = 1.0f;
        [SerializeField] float m_ResolutionRate = 1.0f;
        [SerializeField] bool m_UseSRPBatcher = true;
        [SerializeField] MsaaQuality m_MSAASamples = MsaaQuality.Disabled;
        [SerializeField] bool m_SSAOEnable = false;

        public bool HDREnable
        {
            get { return m_HDREnable; }
            set { m_HDREnable = value; }
        }

        public float Exposure
        {
            get { return m_Exposure; }
            set { m_Exposure = value; }
        }

        public float ResolutionRate
        {
            get { return m_ResolutionRate; }
            set { m_ResolutionRate = value; }
        }

        public bool UseSRPBatcher
        {
            get { return m_UseSRPBatcher; }
            set { m_UseSRPBatcher = value; }
        }

        public MsaaQuality MSAASamples
        {
            get { return m_MSAASamples; }
            set { m_MSAASamples = value; }
        }

        public bool SSAOEnable
        {
            get { return m_SSAOEnable; }
            set { m_SSAOEnable = value;}
        }
        #endregion

        #region Shadowmap
        // Shadows Settings
        [SerializeField] float m_ShadowDistance = 50.0f;
        [SerializeField] eShadowCascadesOption m_ShadowCascades = eShadowCascadesOption.NoCascades;
        [SerializeField] float m_Cascade2Split = 0.25f;
        [SerializeField] Vector3 m_Cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
        [SerializeField] float m_ShadowDepthBias = 1.0f;
        [SerializeField] float m_ShadowNormalBias = 0.75f;
        //[SerializeField] bool m_SoftShadowsSupported = false;
        [SerializeField] bool m_AdaptiveShadowBias = false;
        [SerializeField] bool m_EnableCSMBlend = false;
        [SerializeField] float m_CSMBlendDistance = 0;
        [SerializeField] eShadowType m_ShadowType = eShadowType.PCF;
        [SerializeField] eShadowPCFFilter m_ShadowPCFFilter = eShadowPCFFilter.PCF_None;
        [SerializeField] eShadowResolution m_ShadowResolution = eShadowResolution._512;
        [SerializeField] float m_PCSSSoftness = 1.0f;
        [SerializeField] float m_PCSSSoftnessFalloff = 2.0f;
        [SerializeField] bool m_VSMSATEnable = false;
        [SerializeField] eGaussianRadius m_ShadowPrefitlerGaussianRadius = eGaussianRadius.eGausian3x3;
        [SerializeField] bool m_ScreenSpaceShadow = false;
        [SerializeField] float m_ScreenSpaceShadowScale = 1.0f;
        [SerializeField] Vector2 m_EVSMExponents = new Vector2(10, 10);
        [SerializeField] float m_LightBleedingReduction = 0.5f;
        

        public float shadowDistance
        {
            get { return m_ShadowDistance; }
            set { m_ShadowDistance = Mathf.Max(0.0f, value); }
        }

        public eShadowCascadesOption shadowCascadeOption
        {
            get { return m_ShadowCascades; }
            set { m_ShadowCascades = value; }
        }

        public float cascade2Split
        {
            get { return m_Cascade2Split; }
        }

        public Vector3 cascade4Split
        {
            get { return m_Cascade4Split; }
            set { m_Cascade4Split = value; }
        }

        public float shadowDepthBias
        {
            get { return m_ShadowDepthBias; }
            set { m_ShadowDepthBias = ValidateShadowBias(value); }
        }

        public float shadowNormalBias
        {
            get { return m_ShadowNormalBias; }
            set { m_ShadowNormalBias = ValidateShadowBias(value); }
        }

        //public bool supportsSoftShadows
        //{
        //    get { return m_SoftShadowsSupported; }
        //    set { m_SoftShadowsSupported = value; }
        //}

        float ValidateShadowBias(float value)
        {
            return Mathf.Max(0.0f, Mathf.Min(value, InsanityPipeline.maxShadowBias));
        }

        public bool adaptiveShadowBias
        {
            get { return m_AdaptiveShadowBias; }
            set { m_AdaptiveShadowBias = value; }
        }

        public bool enableCSMBlend
        {
            get { return m_EnableCSMBlend; }
            set { m_EnableCSMBlend = value; }
        }

        public float CSMBlendDistance
        {
            get { return m_CSMBlendDistance; }
            set { m_CSMBlendDistance = value; }
        }

        public eShadowType ShadowType
        {
            get { return m_ShadowType; }
            set { m_ShadowType = value; }
        }

        public eShadowPCFFilter PCFFilter
        {
            get { return m_ShadowPCFFilter; }
            set { m_ShadowPCFFilter = value; }
        }

        public eShadowResolution ShadowResolution
        {
            get { return m_ShadowResolution; }
            set { m_ShadowResolution = value; }
        }

        public float PCSSSoftness
        {
            get { return m_PCSSSoftness; }
            set { m_PCSSSoftness = value; }
        }

        public float PCSSSoftnessFalloff
        {
            get { return m_PCSSSoftnessFalloff; }
            set { m_PCSSSoftnessFalloff = value; }
        }

        public bool VSMSATEnable
        {
            get { return m_VSMSATEnable; }
            set { m_VSMSATEnable = value; }
        }

        public eGaussianRadius ShadowPrefilterGaussian
        {
            get { return m_ShadowPrefitlerGaussianRadius; }
            set { m_ShadowPrefitlerGaussianRadius = value; }
        }

        public Vector2 EVSMExponentConstants
        {
            get { return m_EVSMExponents; }
            set { m_EVSMExponents = value; }
        }

        public float LightBleedingReduction
        {
            get { return m_LightBleedingReduction; }
            set { m_LightBleedingReduction = value; }
        }

        public bool ScreenSpaceShadow
        {
            get { return m_ScreenSpaceShadow; }
            set { m_ScreenSpaceShadow = value;}
        }

        public float ScreenSpaceShadowScale
        {
            get { return m_ScreenSpaceShadowScale; }
            set { m_ScreenSpaceShadowScale = value;}
        }
        #endregion

        #region Lighting
        [SerializeField] bool m_AdditionalLightEnable = true;
        [SerializeField] eAdditionalLightCullingFunction m_AdditionalLightCulling = eAdditionalLightCullingFunction.Default;
        public bool AdditionalLightEnable
        {
            get { return m_AdditionalLightEnable; }
            set { m_AdditionalLightEnable = value; }
        }

        public eAdditionalLightCullingFunction AdditonalLightCullingFunction
        {
            set { m_AdditionalLightCulling = value; }
            get { return m_AdditionalLightCulling; }
        }
        #endregion

        #region Atmosphere scattring
        [SerializeField] AtmosphereResources m_AtmosphereResources;

        public AtmosphereResources AtmosphereResources
        {
            get { return m_AtmosphereResources; }
            set { m_AtmosphereResources = value; }
        }

        //Atmosphere Settings
        [SerializeField] Color m_SunLightColor = Color.white;
        [SerializeField] bool m_physicalBasedSky = false;

        private bool m_RecalculateSkyLUT = true;

        public bool RecalculateSkyLUT
        {
            get { return m_RecalculateSkyLUT; }
            set { m_RecalculateSkyLUT = value; }
        }

        public Color SunLightColor
        {
            get { return m_SunLightColor; }
            set { m_SunLightColor = value; }
        }

        public bool PhysicalBasedSky
        {
            get { return m_physicalBasedSky; }
            set
            {
                m_physicalBasedSky = value;
            }
        }

        #endregion

        #region Debug View
        [SerializeField] DebugViewMode m_DebugViewMode = DebugViewMode.None;
        public DebugViewMode CurrentDebugMode
        {
            get { return m_DebugViewMode; }
            set { m_DebugViewMode = value; }
        }
        #endregion

        //Pipeline resources
        [SerializeField] InsanityPipelineResources m_PipelineResources;
        [SerializeField] RendererData m_RendererData;
        

        public InsanityPipelineResources InsanityPipelineResources
        {
            get { return m_PipelineResources; }
            set { m_PipelineResources = value; }
        }

        public RendererData RendererData 
        { 
            get { return m_RendererData; }
            set { m_RendererData = value; }
        }

        protected override void OnValidate()
        {
            //DestroyRenderers();

            // This will call RenderPipelineManager.CleanupRenderPipeline that in turn disposes the render pipeline instance and
            // assign pipeline asset reference to null
            base.OnValidate();
        }

        private void CreateRenderers(InsanityPipeline pipeline)
        {
            if (m_Renderer != null)
            {
                Debug.LogError($"Creating renderers but previous instance wasn't properly destroyed");
            }

            m_Renderer = m_RendererData.Create(pipeline);
        }

        private void DestroyRenderers()
        {
            Debug.Log("Destroy Renderers");
            if (m_Renderer != null)
            {
                m_Renderer.Dispose();
                m_Renderer = null;
            }
        }
    }
}
