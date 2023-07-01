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
    public enum ShadowCascadesOption
    {
        NoCascades,
        TwoCascades,
        FourCascades,
    }


    public enum ShadowQuality
    {
        Disabled,
        HardShadows,
        SoftShadows,
    }

    public enum ShadowType
    {
        PCF,
        PCSS,
        VSM,
        EVSM,
        MSM,
    }

    public enum ShadowPCFFilter
    {
        PCF_None,
        PCF_3x3,
        PCF_5x5,
        PCF_7x7,
    }

    public enum ShadowResolution
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
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

        protected override RenderPipeline CreatePipeline()
        {
            return new InsanityPipeline();
        }

        #region Shadowmap
        // Shadows Settings
        [SerializeField] float m_ShadowDistance = 50.0f;
        [SerializeField] ShadowCascadesOption m_ShadowCascades = ShadowCascadesOption.NoCascades;
        [SerializeField] float m_Cascade2Split = 0.25f;
        [SerializeField] Vector3 m_Cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
        [SerializeField] float m_ShadowDepthBias = 1.0f;
        [SerializeField] float m_ShadowNormalBias = 0.75f;
        [SerializeField] bool m_SoftShadowsSupported = false;
        [SerializeField] bool m_AdaptiveShadowBias = false;
        [SerializeField] bool m_EnableCSMBlend = false;
        [SerializeField] float m_CSMBlendDistance = 0;
        [SerializeField] ShadowType m_ShadowType = ShadowType.PCF;
        [SerializeField] ShadowPCFFilter m_ShadowPCFFilter = ShadowPCFFilter.PCF_None;
        [SerializeField] float m_PCSSSoftness = 1.0f;
        [SerializeField] float m_PCSSSoftnessFalloff = 2.0f;
        [SerializeField] bool m_VSMSATEnable = false;
        [SerializeField] eGaussianRadius m_ShadowPrefitlerGaussianRadius = eGaussianRadius.eGausian3x3;
        [SerializeField] Vector2 m_EVSMExponents = new Vector2(10, 10);
        [SerializeField] float m_LightBleedingReduction = 0.5f;

        public float shadowDistance
        {
            get { return m_ShadowDistance; }
            set { m_ShadowDistance = Mathf.Max(0.0f, value); }
        }

        public ShadowCascadesOption shadowCascadeOption
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

        public bool supportsSoftShadows
        {
            get { return m_SoftShadowsSupported; }
            set { m_SoftShadowsSupported = value; }
        }

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

        public ShadowType ShadowType
        {
            get { return m_ShadowType; }
            set { m_ShadowType = value; }
        }

        public ShadowPCFFilter PCFFilter
        {
            get { return m_ShadowPCFFilter; }
            set { m_ShadowPCFFilter = value; }
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
        #endregion

        #region Atmosphere scattring
        //Atmosphere Settings
        [SerializeField] float m_ScatteringScaleR = 1.0f;
        [SerializeField] float m_ScatteringScaleM = 1.0f;
        [SerializeField] float m_MieG = 0.76f;
        [SerializeField] Color m_SunLightColor = Color.white;
        [SerializeField] bool m_physicalBasedSky = false;

        private bool m_RecalculateSkyLUT = true;

        public bool RecalculateSkyLUT
        {
            get { return m_RecalculateSkyLUT; }
            set { m_RecalculateSkyLUT = value; }
        }

        public float ScatteringScaleR
        {
            get { return m_ScatteringScaleR; }
            set { m_ScatteringScaleR = value; RecalculateSkyLUT = true; }
        }

        public float ScatteringScaleM
        {
            get { return m_ScatteringScaleM; }
            set { m_ScatteringScaleM = value; RecalculateSkyLUT = true; }
        }

        public float MieG
        {
            get { return m_MieG; }
            set { m_MieG = value;}
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

        //Pipeline resources
        [SerializeField] InsanityPipelineResources m_PipelineResources;

        

        public InsanityPipelineResources InsanityPipelineResources
        {
            get { return m_PipelineResources; }
            set { m_PipelineResources = value; }
        }
    }
}
