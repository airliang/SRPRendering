using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Insanity;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
namespace UnityEditor.Insanity
{
    [CustomEditor(typeof(InsanityPipelineAsset))]
    [CanEditMultipleObjects]
    public class InsanityPipelineAssetEditor : Editor
    {
        internal class Styles
        {
            public static GUIContent renderSettingText = EditorGUIUtility.TrTextContent("Render Settings");
            public static GUIContent hdrEnableText = EditorGUIUtility.TrTextContent("HDR Enable", "Controls the global HDR settings.");
            public static GUIContent hdrExposureText = EditorGUIUtility.TrTextContent("Exposure", "Controls the global HDR exposure settings.");
            public static GUIContent screenPercentageText = EditorGUIUtility.TrTextContent("Screen Resolution Percentage", "Controls the global screen resolution settings.");
            public static GUIContent srpBatcherText = EditorGUIUtility.TrTextContent("SRP Batcher", "Controls the global SRP Batcher settings.");
            public static GUIContent msaaText = EditorGUIUtility.TrTextContent("MSAA", "Controls the global anti aliasing settings");
            
            public static string[] msaaOptions = { "Disabled", "2x", "4x", "8x" };

            public static GUIContent shadowSettingsText = EditorGUIUtility.TrTextContent("Shadows");

            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Distance", "Maximum shadow rendering distance.");
            public static GUIContent shadowCascadesText = EditorGUIUtility.TrTextContent("Cascades", "Number of cascade splits used in for directional shadows");
            public static GUIContent shadowDepthBias = EditorGUIUtility.TrTextContent("Depth Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowNormalBias = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
            //public static GUIContent supportsSoftShadows = EditorGUIUtility.TrTextContent("Soft Shadows", "If enabled pipeline will perform shadow filtering. Otherwise all lights that cast shadows will fallback to perform a single shadow sample.");
            public static GUIContent adaptiveShadowBias = EditorGUIUtility.TrTextContent("Adaptive Shadow Bias", "If enabled pipeline will enable adaptive shadow bias.");
            public static GUIContent enableCSMBlend = EditorGUIUtility.TrTextContent("Enable CSM Blend", "Blend the shadow between 2 cascades.");
            public static GUIContent csmBlendDistance = EditorGUIUtility.TrTextContent("CSM Blend Distance", "Blend distance between 2 cascades.");
            public static GUIContent shadowType = EditorGUIUtility.TrTextContent("Shadow Type", "Shadow Mapping algorithm options.");
            public static GUIContent shadowResolution = EditorGUIUtility.TrTextContent("Shadow Resolution", "Shadow Resolution options.");
            public static GUIContent shadowPCFFilter = EditorGUIUtility.TrTextContent("Shadow PCF Filter", "Shadow Mapping PCF Filter options.");
            public static GUIContent pcssSoftness = EditorGUIUtility.TrTextContent("PCSS Softness", "Simulate the direction light size in PCSS algorithm");
            public static GUIContent pcssSoftnessFalloff = EditorGUIUtility.TrTextContent("PCSS Softness Falloff", "Softness falloff parameter use by a pow formula.");
            public static GUIContent vsmSATEnable = EditorGUIUtility.TrTextContent("VSM SAT Enable", "Enable the SAT algorithm to calculate the vsm filter.");
            public static GUIContent gaussianFilterRadius = EditorGUIUtility.TrTextContent("Gaussian Prefilter Radius", "The Gaussian kernel size to filtering.");
            public static GUIContent exponentialConstants = EditorGUIUtility.TrTextContent("Exponential Variance Shadow Constants", "Setting the exponential constants of EVSM");
            public static GUIContent lightBleedingReduction = EditorGUIUtility.TrTextContent("LightBleeding Reduction Value", "Clamp the [pMax, 1] to the [lightBleeding, 1]");
            public static GUIContent screenSpaceShadow = EditorGUIUtility.TrTextContent("Screen Space Shadow Map", "Screen Space Shadow Map Enable");
            public static GUIContent screenSpaceShadowScale = EditorGUIUtility.TrTextContent("Screen Space Shadow Map Scale", "Shadow Map Scale[0, 1] of the screen");
            public static string[] shadowCascadeOptions = { "No Cascades", "Two Cascades", "Four Cascades" };
            public static string[] shadowResolutionOptions = { "256", "512", "1024", "2048" };
            public static string[] shadowTypeOptions = { "PCF", "PCSS", "VSM", "EVSM", "MSM" };
            public static string[] shadowPCFFilterOptions = { "Hard", "Low", "Medium", "High" };
            public static string[] gaussianFilterRadiusOptions = { "3x3", "5x5", "9x9", "13x13" };

            public static GUIContent lightingSettingsText = EditorGUIUtility.TrTextContent("Lighting");
            public static GUIContent supportAdditionalLightsText = EditorGUIUtility.TrTextContent("Support additional lights");
            public static GUIContent additionalLightCullingText = EditorGUIUtility.TrTextContent("Additional Light Culling Function");
            public static string[] additonalLightCullingOptions = { "Default", "Tile Based", "Cluster Based" };
            public static GUIContent lightTileSizeText = EditorGUIUtility.TrTextContent("Tile Size");

            public static GUIContent atmosphereSettingsText = EditorGUIUtility.TrTextContent("Atmosphere");
            public static GUIContent atmosphereResourcesText = EditorGUIUtility.TrTextContent("Atmosphere setting resources", "Atmosphere setting resources");
            public static GUIContent physicalBasedSkyEnable = EditorGUIUtility.TrTextContent("Physical based sky Enable", "Enable Atmosphere scattering rendering in sky.");
            public static GUIContent scatteringScaleRayleigh = EditorGUIUtility.TrTextContent("Scattering Scale Rayleigh", "The Rayleigh scattering scale.");
            public static GUIContent scatteringScaleMie = EditorGUIUtility.TrTextContent("Scattering Scale Mie", "The Mie scattering scale.");
            public static GUIContent mieG = EditorGUIUtility.TrTextContent("Mie G", "The Mie G value.");
            public static GUIContent sunLightColor = EditorGUIUtility.TrTextContent("Sun Light Color", "The sun light color.");
            public static GUIContent multipleScatteringEnableText = EditorGUIUtility.TrTextContent("Multiple Scattering Enable", "Enable multiple scattering in atmosphere.");
            public static GUIContent multipleScatteringOrderText = EditorGUIUtility.TrTextContent("Multiple Scattering Order", "The multiple scattering order.");

            public static GUIContent resourcesSettingsText = EditorGUIUtility.TrTextContent("Resources");
            public static GUIContent pipelineResourcesText = EditorGUIUtility.TrTextContent("Pipeline Resources", 
                    "The pipeline resources asset that contains all the shaders and other resources used by the pipeline.");
            public static GUIContent renderDataText = EditorGUIUtility.TrTextContent("Renderer Data",
                    "Renderer Data defines which render path should be used in the render pipeline.");

            public static GUIContent ssaoSettingsText = EditorGUIUtility.TrTextContent("SSAO", "SSAO");
            public static GUIContent ssaoText = EditorGUIUtility.TrTextContent("SSAO Enable", "Enable Screen Space Ambient Occlusion.");
            public static GUIContent ssaoRadiusText = EditorGUIUtility.TrTextContent("SSAO Radius", "SSAO Radius.");
            public static GUIContent ssaoMaxRadiusInPixelText = EditorGUIUtility.TrTextContent("Max SSAO Radius(pixel)");
            public static GUIContent hbaoHorizonBiasText = EditorGUIUtility.TrTextContent("Horizontal Bias");
            public static GUIContent ssaoHalfResolutionText = EditorGUIUtility.TrTextContent("Half Resolution");
            public static GUIContent ssaoIntensityText = EditorGUIUtility.TrTextContent("AO Intensity");
            public static GUIContent ssaoFadeDistanceText = EditorGUIUtility.TrTextContent("AO Fade Distance Range");
            public static GUIContent ssaoBlurMethodText = EditorGUIUtility.TrTextContent("Blur Method");
            public static string[] ssaoBlurMethodOptions = { "Gaussian", "Dual" };
            public static GUIContent ssaoEnableTemperalFilterText = EditorGUIUtility.TrTextContent("Temperal Filter");

            public static GUIContent debugViewSettingsText = EditorGUIUtility.TrTextContent("DebugView", "DebugView to display the rendering results in the pipeline");
            public static string[] debugViewTypeOptions = { "None", "TileBasedLights", "Depth", "LinearDepth", "Normal", "SSAO", "TriangleOverdraw" };

        }
        SavedBool m_RenderSettingsFoldout;
        SerializedProperty m_HDRSupportProp;
        SerializedProperty m_HDRExposureProp;
        SerializedProperty m_ScreenResolutionProp;
        SerializedProperty m_SRPBatcherProp;
        SerializedProperty m_MSAAProp;
        

        SavedBool m_ShadowSettingsFoldout;

        SerializedProperty m_ShadowDistanceProp;
        SerializedProperty m_ShadowCascadesProp;
        SerializedProperty m_ShadowCascade2SplitProp;
        SerializedProperty m_ShadowCascade4SplitProp;
        SerializedProperty m_ShadowDepthBiasProp;
        SerializedProperty m_ShadowNormalBiasProp;

        //SerializedProperty m_SoftShadowsSupportedProp;
        SerializedProperty m_AdaptiveShadowBias;
        SerializedProperty m_EnableCSMBlend;
        SerializedProperty m_CSMBlendDistance;

        SerializedProperty m_ShadowType;
        SerializedProperty m_ShadowResolution;
        SerializedProperty m_ShadowPCFFilter;

        SerializedProperty m_PCSSSoftnessProp;
        SerializedProperty m_PCSSSoftnessFalloff;
        SerializedProperty m_VSMSATEnable;
        SerializedProperty m_ShadowPrefilterGaussians;
        SerializedProperty m_ExponentialConstants;
        SerializedProperty m_LightBleedingReduction;
        SerializedProperty m_ScreenSpaceShadow;
        SerializedProperty m_ScreenSpaceShadowScale;

        SavedBool m_LightingSettingsFoldout;
        SerializedProperty m_AdditionalLightEnable;
        SerializedProperty m_AdditionalLightCullingFunction;
        SerializedProperty m_LightTileSizeProp;

        SavedBool m_ResourcesSettingsFoldout;
        SerializedProperty m_PipelineResources;
        SerializedProperty m_RendererData;

        SavedBool m_AtmosphereSettingsFoldout;
        SerializedProperty m_AtmosphereResources;
        SerializedProperty m_PhysicalBaseSky;
        //SerializedProperty m_ScatteringScaleRayleigh;
        //SerializedProperty m_ScatteringScaleMie;
        //SerializedProperty m_MieG;
        SerializedProperty m_SunLightColor;
        //SerializedProperty m_MultipleScatteringOrder;

        
        SavedBool m_SSAOSettingFoldout;
        SerializedProperty m_SSAOProp;
        SerializedProperty m_SSAORadiusProp;
        SerializedProperty m_MaxRadiusInPixelProp;
        SerializedProperty m_HBAOHorizonBiasProp;
        SerializedProperty m_SSAOHalfResolutionProp;
        SerializedProperty m_SSAOIntensityProp;
        SerializedProperty m_SSAOFadeDistanceStartProp;
        SerializedProperty m_SSAOFadeDistanceEndProp;
        SerializedProperty m_SSAOBlurMethodProp;
        SerializedProperty m_SSAOTemperalFilterProp;

        SavedBool m_DebugViewSettingFoldout;
        SerializedProperty m_DebugViewSettingsMode;
             
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPipelineResources();
            DrawRenderSettings();
            DrawShadowSettings();
            DrawLightingSettings();
            DrawAtmosphereScatteringSettings();
            DrawSSAOSetting();
            DrawDebugViewSetting();
            serializedObject.ApplyModifiedProperties();
        }

        void OnEnable()
        {
            m_RenderSettingsFoldout = new SavedBool($"{target.GetType()}.RenderSettingsFoldout", true);
            m_HDRSupportProp = serializedObject.FindProperty("m_HDREnable");
            m_HDRExposureProp = serializedObject.FindProperty("m_Exposure");
            m_ScreenResolutionProp = serializedObject.FindProperty("m_ResolutionRate");
            m_SRPBatcherProp = serializedObject.FindProperty("m_UseSRPBatcher");
            m_MSAAProp = serializedObject.FindProperty("m_MSAASamples");
            

            m_ShadowSettingsFoldout = new SavedBool($"{target.GetType()}.ShadowSettingsFoldout", false);

            m_ShadowDistanceProp = serializedObject.FindProperty("m_ShadowDistance");
            m_ShadowCascadesProp = serializedObject.FindProperty("m_ShadowCascades");
            m_ShadowCascade2SplitProp = serializedObject.FindProperty("m_Cascade2Split");
            m_ShadowCascade4SplitProp = serializedObject.FindProperty("m_Cascade4Split");
            m_ShadowDepthBiasProp = serializedObject.FindProperty("m_ShadowDepthBias");
            m_ShadowNormalBiasProp = serializedObject.FindProperty("m_ShadowNormalBias");
            //m_SoftShadowsSupportedProp = serializedObject.FindProperty("m_SoftShadowsSupported");
            m_AdaptiveShadowBias = serializedObject.FindProperty("m_AdaptiveShadowBias");
            m_EnableCSMBlend = serializedObject.FindProperty("m_EnableCSMBlend");
            m_CSMBlendDistance = serializedObject.FindProperty("m_CSMBlendDistance");
            m_ShadowType = serializedObject.FindProperty("m_ShadowType");
            m_ShadowResolution = serializedObject.FindProperty("m_ShadowResolution");
            m_ShadowPCFFilter = serializedObject.FindProperty("m_ShadowPCFFilter");
            m_PCSSSoftnessProp = serializedObject.FindProperty("m_PCSSSoftness");
            m_PCSSSoftnessFalloff = serializedObject.FindProperty("m_PCSSSoftnessFalloff");
            m_VSMSATEnable = serializedObject.FindProperty("m_VSMSATEnable");
            m_ShadowPrefilterGaussians = serializedObject.FindProperty("m_ShadowPrefitlerGaussianRadius");
            m_ScreenSpaceShadow = serializedObject.FindProperty("m_ScreenSpaceShadow");
            m_ScreenSpaceShadowScale = serializedObject.FindProperty("m_ScreenSpaceShadowScale");
            m_ExponentialConstants = serializedObject.FindProperty("m_EVSMExponents");
            m_LightBleedingReduction = serializedObject.FindProperty("m_LightBleedingReduction");
            m_PipelineResources = serializedObject.FindProperty("m_PipelineResources");
            m_RendererData = serializedObject.FindProperty("m_RendererData");

            m_LightingSettingsFoldout = new SavedBool($"{target.GetType()}.LightingSettingsFoldout", false);
            m_AdditionalLightEnable = serializedObject.FindProperty("m_AdditionalLightEnable");
            m_AdditionalLightCullingFunction = serializedObject.FindProperty("m_AdditionalLightCulling");
            m_LightTileSizeProp = serializedObject.FindProperty("m_TileSize");

            m_AtmosphereSettingsFoldout = new SavedBool($"{target.GetType()}.AtmosphereSettingsFoldout", false);
            m_AtmosphereResources = serializedObject.FindProperty("m_AtmosphereResources");
            m_PhysicalBaseSky = serializedObject.FindProperty("m_physicalBasedSky");
            //m_ScatteringScaleRayleigh = serializedObject.FindProperty("m_ScatteringScaleR");
            //m_ScatteringScaleMie = serializedObject.FindProperty("m_ScatteringScaleM");
            //m_MieG = serializedObject.FindProperty("m_MieG");
            m_SunLightColor = serializedObject.FindProperty("m_SunLightColor");
            //m_MultipleScatteringOrder = serializedObject.FindProperty("m_multipleScatteringOrder");

            m_ResourcesSettingsFoldout = new SavedBool($"{target.GetType()}.ResourcesSettingsFoldout", false);

            m_SSAOSettingFoldout = new SavedBool($"{target.GetType()}.SSAOSettingFoldout", false);
            m_SSAOProp = serializedObject.FindProperty("m_SSAOEnable");
            m_SSAORadiusProp = serializedObject.FindProperty("m_SSAORadius");
            m_MaxRadiusInPixelProp = serializedObject.FindProperty("m_MaxRadiusInPixel");
            m_HBAOHorizonBiasProp = serializedObject.FindProperty("m_HBAOHorizonBias");
            m_SSAOHalfResolutionProp = serializedObject.FindProperty("m_AOHalfResolution");
            m_SSAOIntensityProp = serializedObject.FindProperty("m_AOIntensity");
            m_SSAOFadeDistanceStartProp = serializedObject.FindProperty("m_AOFadeDistanceStart");
            m_SSAOFadeDistanceEndProp = serializedObject.FindProperty("m_AOFadeDistanceEnd");
            m_SSAOBlurMethodProp = serializedObject.FindProperty("m_SSAOBlurType");
            m_SSAOTemperalFilterProp = serializedObject.FindProperty("m_EnableTemperalFilter");

            m_DebugViewSettingFoldout = new SavedBool($"{target.GetType()}.DebugViewSettingFoldout", false);
            m_DebugViewSettingsMode = serializedObject.FindProperty("m_DebugViewMode");
        }

        void DrawRenderSettings()
        {
            m_RenderSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_RenderSettingsFoldout.value, Styles.renderSettingText);
            if (m_RenderSettingsFoldout.value)
            {
                EditorGUILayout.PropertyField(m_HDRSupportProp, Styles.hdrEnableText);

                if (m_HDRSupportProp.boolValue)
                {
                    m_HDRExposureProp.floatValue = EditorGUILayout.Slider(Styles.hdrExposureText, m_HDRExposureProp.floatValue,
                    0.0f, 2.0f);
                }

                m_ScreenResolutionProp.floatValue = EditorGUILayout.Slider(Styles.screenPercentageText, m_ScreenResolutionProp.floatValue, 0.1f, 1.0f);

                m_SRPBatcherProp.boolValue = EditorGUILayout.Toggle(Styles.srpBatcherText, m_SRPBatcherProp.boolValue);
                EditorGUILayout.PropertyField(m_MSAAProp, Styles.msaaText);
                
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawShadowSettings()
        {
            m_ShadowSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_ShadowSettingsFoldout.value, Styles.shadowSettingsText);
            if (m_ShadowSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                m_ShadowDistanceProp.floatValue = Mathf.Max(0.0f, EditorGUILayout.FloatField(Styles.shadowDistanceText, m_ShadowDistanceProp.floatValue));
                CoreEditorUtils.DrawPopup(Styles.shadowCascadesText, m_ShadowCascadesProp, Styles.shadowCascadeOptions);

                eShadowCascadesOption cascades = (eShadowCascadesOption)m_ShadowCascadesProp.intValue;
                if (cascades == eShadowCascadesOption.FourCascades)
                    EditorUtils.DrawCascadeSplitGUI<Vector3>(ref m_ShadowCascade4SplitProp);
                else if (cascades == eShadowCascadesOption.TwoCascades)
                    EditorUtils.DrawCascadeSplitGUI<float>(ref m_ShadowCascade2SplitProp);
                if (m_ShadowCascadesProp.intValue > 0)
                {
                    EditorGUILayout.PropertyField(m_EnableCSMBlend, Styles.enableCSMBlend);
                    if (m_EnableCSMBlend.boolValue)
                    {
                        m_CSMBlendDistance.floatValue = EditorGUILayout.Slider(Styles.csmBlendDistance, m_CSMBlendDistance.floatValue, 0.0f, 1.0f);
                    }
                }

                CoreEditorUtils.DrawPopup(Styles.shadowResolution, m_ShadowResolution, Styles.shadowResolutionOptions);

                m_ShadowDepthBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowDepthBias, m_ShadowDepthBiasProp.floatValue, 
                    0.0f, InsanityPipeline.maxShadowBias);
                m_ShadowNormalBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowNormalBias, m_ShadowNormalBiasProp.floatValue, 
                    0.0f, InsanityPipeline.maxShadowBias);

                CoreEditorUtils.DrawPopup(Styles.shadowType, m_ShadowType, Styles.shadowTypeOptions);
                eShadowType shadowType = (eShadowType)m_ShadowType.intValue;
                if (shadowType == eShadowType.PCF)
                {
                    CoreEditorUtils.DrawPopup(Styles.shadowPCFFilter, m_ShadowPCFFilter, Styles.shadowPCFFilterOptions);
                    //EditorGUILayout.PropertyField(m_SoftShadowsSupportedProp, Styles.supportsSoftShadows);
                }
                else if (shadowType == eShadowType.PCSS)
                {
                    m_PCSSSoftnessProp.floatValue = EditorGUILayout.Slider(Styles.pcssSoftness, m_PCSSSoftnessProp.floatValue, 0.01f, 2.0f);
                    m_PCSSSoftnessFalloff.floatValue = EditorGUILayout.Slider(Styles.pcssSoftnessFalloff, m_PCSSSoftnessFalloff.floatValue, 0.0f, 8.0f);
                    
                }
                else if (shadowType == eShadowType.VSM || shadowType == eShadowType.EVSM)
                {
                    CoreEditorUtils.DrawPopup(Styles.gaussianFilterRadius, m_ShadowPrefilterGaussians, Styles.gaussianFilterRadiusOptions);
                    if (shadowType == eShadowType.EVSM)
                    {
                        EditorGUILayout.PropertyField(m_ExponentialConstants, Styles.exponentialConstants);
                    }
                    else
                    {
                        m_VSMSATEnable.boolValue = EditorGUILayout.Toggle(Styles.vsmSATEnable, m_VSMSATEnable.boolValue);
                    }
                    m_LightBleedingReduction.floatValue =  EditorGUILayout.Slider(Styles.lightBleedingReduction, 
                        m_LightBleedingReduction.floatValue, 0.0f, 1.0f);
                }

                
                EditorGUILayout.PropertyField(m_AdaptiveShadowBias, Styles.adaptiveShadowBias);
                m_ScreenSpaceShadow.boolValue = EditorGUILayout.Toggle(Styles.screenSpaceShadow, m_ScreenSpaceShadow.boolValue);
                if (m_ScreenSpaceShadow.boolValue)
                {
                    m_ScreenSpaceShadowScale.floatValue = EditorGUILayout.Slider(Styles.screenSpaceShadowScale, m_ScreenSpaceShadowScale.floatValue, 0.25f, 1.0f);
                }


                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawLightingSettings()
        {
            m_LightingSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_LightingSettingsFoldout.value, Styles.lightingSettingsText);
            if (m_LightingSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                m_AdditionalLightEnable.boolValue = EditorGUILayout.Toggle(Styles.supportAdditionalLightsText, m_AdditionalLightEnable.boolValue);
                if (m_AdditionalLightEnable.boolValue)
                {
                    CoreEditorUtils.DrawPopup(Styles.additionalLightCullingText, m_AdditionalLightCullingFunction, Styles.additonalLightCullingOptions);
                    if (m_AdditionalLightCullingFunction.intValue == (int)eAdditionalLightCullingFunction.TileBased)
                    {
                        m_LightTileSizeProp.intValue = EditorGUILayout.IntField(Styles.lightTileSizeText, m_LightTileSizeProp.intValue);
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawAtmosphereScatteringSettings()
        {
            m_AtmosphereSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_AtmosphereSettingsFoldout.value, Styles.atmosphereSettingsText);
            if (m_AtmosphereSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                m_AtmosphereResources.objectReferenceValue = EditorGUILayout.ObjectField(Styles.atmosphereResourcesText,
                m_AtmosphereResources.objectReferenceValue, typeof(AtmosphereResources), false);
                m_PhysicalBaseSky.boolValue = EditorGUILayout.Toggle(Styles.physicalBasedSkyEnable, m_PhysicalBaseSky.boolValue);
                if (m_PhysicalBaseSky.boolValue)
                {
                    //m_ScatteringScaleRayleigh.floatValue = EditorGUILayout.Slider(Styles.scatteringScaleRayleigh,
                    //    m_ScatteringScaleRayleigh.floatValue, 0.0f, 5.0f);
                    //m_ScatteringScaleMie.floatValue = EditorGUILayout.Slider(Styles.scatteringScaleMie, m_ScatteringScaleMie.floatValue, 0.0f, 5.0f);
                    //m_MieG.floatValue = EditorGUILayout.Slider(Styles.mieG, m_MieG.floatValue, 0.0f, 1.0f);
                    m_SunLightColor.colorValue = EditorGUILayout.ColorField(Styles.sunLightColor, m_SunLightColor.colorValue, true, false, true);
                    //m_MultipleScatteringOrder.intValue = EditorGUILayout.IntSlider(Styles.multipleScatteringOrderText, m_MultipleScatteringOrder.intValue, 0, 50);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawSSAOSetting()
        {
            m_SSAOSettingFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_SSAOSettingFoldout.value, Styles.ssaoSettingsText);
            if (m_SSAOSettingFoldout.value)
            {
                EditorGUI.indentLevel++;
                EditorGUI.indentLevel--;
                m_SSAOProp.boolValue = EditorGUILayout.Toggle(Styles.ssaoText, m_SSAOProp.boolValue);
                if (m_SSAOProp.boolValue)
                {
                    m_SSAORadiusProp.floatValue = EditorGUILayout.Slider(Styles.ssaoRadiusText, m_SSAORadiusProp.floatValue, 0, 10.0f);
                    m_MaxRadiusInPixelProp.floatValue = EditorGUILayout.Slider(Styles.ssaoMaxRadiusInPixelText, m_MaxRadiusInPixelProp.floatValue, 10.0f, 100.0f);
                    m_HBAOHorizonBiasProp.floatValue = EditorGUILayout.Slider(Styles.hbaoHorizonBiasText, m_HBAOHorizonBiasProp.floatValue, 0, 1.0f);
                    m_SSAOHalfResolutionProp.boolValue = EditorGUILayout.Toggle(Styles.ssaoHalfResolutionText, m_SSAOHalfResolutionProp.boolValue);
                    m_SSAOIntensityProp.floatValue = EditorGUILayout.Slider(Styles.ssaoIntensityText, m_SSAOIntensityProp.floatValue, 0, 10.0f);
                    float fadeStart = m_SSAOFadeDistanceStartProp.floatValue;
                    float fadeEnd = m_SSAOFadeDistanceEndProp.floatValue;
                    EditorGUILayout.MinMaxSlider(Styles.ssaoFadeDistanceText, ref fadeStart, ref fadeEnd, 0, 100.0f);
                    m_SSAOFadeDistanceStartProp.floatValue = fadeStart;
                    m_SSAOFadeDistanceEndProp.floatValue = fadeEnd;

                    CoreEditorUtils.DrawPopup(Styles.ssaoBlurMethodText, m_SSAOBlurMethodProp, Styles.ssaoBlurMethodOptions);
                    m_SSAOTemperalFilterProp.boolValue = EditorGUILayout.Toggle(Styles.ssaoEnableTemperalFilterText, m_SSAOTemperalFilterProp.boolValue);
                }
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawDebugViewSetting()
        {
            m_DebugViewSettingFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_DebugViewSettingFoldout.value, Styles.debugViewSettingsText);
            if (m_DebugViewSettingFoldout.value)
            {
                EditorGUI.indentLevel++;
                EditorGUI.indentLevel--;
                CoreEditorUtils.DrawPopup(Styles.debugViewSettingsText, m_DebugViewSettingsMode, Styles.debugViewTypeOptions);
                DebugViewMode debugMode = (DebugViewMode)m_DebugViewSettingsMode.intValue;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawPipelineResources()
        {
            m_ResourcesSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_ResourcesSettingsFoldout.value, Styles.resourcesSettingsText);
            if (m_ResourcesSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                m_PipelineResources.objectReferenceValue = EditorGUILayout.ObjectField(Styles.pipelineResourcesText,
                m_PipelineResources.objectReferenceValue, typeof(InsanityPipelineResources), false);

                m_RendererData.objectReferenceValue = EditorGUILayout.ObjectField(Styles.renderDataText,
                m_RendererData.objectReferenceValue, typeof(RendererData), false);

                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
#endif
