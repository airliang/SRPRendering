using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Insanity;
using Insanity;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using UnityEditorInternal;
using UnityEngine.Rendering;

namespace UnityEditor.Insanity
{
    [CustomEditor(typeof(InsanityPipelineAsset))]
    [CanEditMultipleObjects]
    public class InsanityPipelineAssetEditor : Editor
    {
        internal class Styles
        {
            public static GUIContent shadowSettingsText = EditorGUIUtility.TrTextContent("Shadows");

            public static GUIContent shadowDistanceText = EditorGUIUtility.TrTextContent("Distance", "Maximum shadow rendering distance.");
            public static GUIContent shadowCascadesText = EditorGUIUtility.TrTextContent("Cascades", "Number of cascade splits used in for directional shadows");
            public static GUIContent shadowDepthBias = EditorGUIUtility.TrTextContent("Depth Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent shadowNormalBias = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
            public static GUIContent supportsSoftShadows = EditorGUIUtility.TrTextContent("Soft Shadows", "If enabled pipeline will perform shadow filtering. Otherwise all lights that cast shadows will fallback to perform a single shadow sample.");
            public static GUIContent adaptiveShadowBias = EditorGUIUtility.TrTextContent("Adaptive Shadow Bias", "If enabled pipeline will enable adaptive shadow bias.");
            public static GUIContent enableCSMBlend = EditorGUIUtility.TrTextContent("Enable CSM Blend", "Blend the shaodow between 2 cascades.");
            public static GUIContent csmBlendDistance = EditorGUIUtility.TrTextContent("CSM Blend Distance", "Blend distance between 2 cascades.");
            public static GUIContent shadowType = EditorGUIUtility.TrTextContent("Shadow Type", "Shadow Mapping algothrithm options.");
            public static GUIContent shadowPCFFilter = EditorGUIUtility.TrTextContent("Shadow PCF Filter", "Shadow Mapping PCF Filter options.");
            public static GUIContent pcssSoftness = EditorGUIUtility.TrTextContent("PCSS Softness", "Simulate the direction light size in PCSS algorithrm");
            public static GUIContent pcssSoftnessFalloff = EditorGUIUtility.TrTextContent("PCSS Softness Falloff", "Softness falloff parameter use by a pow formular.");
            public static GUIContent gaussianFilterRadius = EditorGUIUtility.TrTextContent("Gaussian Prefilter Radius", "The gaussian kernel size to filtering.");
            public static GUIContent exponentialConstants = EditorGUIUtility.TrTextContent("Exponential Variance Shadow Constants", "Setting the exponential constants of EVSM");
            public static GUIContent lightBleedingReduction = EditorGUIUtility.TrTextContent("LightBleeding Reduction Value", "Clamp the [pMax, 1] to the [lightBleeding, 1]");
            public static string[] shadowCascadeOptions = { "No Cascades", "Two Cascades", "Four Cascades" };
            public static string[] shadowTypeOptions = { "PCF", "PCSS", "VSM", "EVSM", "MSM" };
            public static string[] shadowPCFFilterOptions = { "Hard", "Low", "Medium", "High" };
            public static string[] gaussianFilterRadiusOptions = { "3x3", "5x5", "9x9", "13x13" };
        }


        SavedBool m_ShadowSettingsFoldout;

        SerializedProperty m_ShadowDistanceProp;
        SerializedProperty m_ShadowCascadesProp;
        SerializedProperty m_ShadowCascade2SplitProp;
        SerializedProperty m_ShadowCascade4SplitProp;
        SerializedProperty m_ShadowDepthBiasProp;
        SerializedProperty m_ShadowNormalBiasProp;

        SerializedProperty m_SoftShadowsSupportedProp;
        SerializedProperty m_AdaptiveShadowBias;
        SerializedProperty m_EnableCSMBlend;
        SerializedProperty m_CSMBlendDistance;

        SerializedProperty m_ShadowType;
        SerializedProperty m_ShadowPCFFilter;

        SerializedProperty m_PCSSSoftnessProp;
        SerializedProperty m_PCSSSoftnessFalloff;
        SerializedProperty m_ShadowPrefilterGaussians;
        SerializedProperty m_ExponentialConstants;
        SerializedProperty m_LightBleedingReduction;
             
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawShadowSettings();


            serializedObject.ApplyModifiedProperties();
        }

        void OnEnable()
        {
            m_ShadowSettingsFoldout = new SavedBool($"{target.GetType()}.ShadowSettingsFoldout", false);

            m_ShadowDistanceProp = serializedObject.FindProperty("m_ShadowDistance");
            m_ShadowCascadesProp = serializedObject.FindProperty("m_ShadowCascades");
            m_ShadowCascade2SplitProp = serializedObject.FindProperty("m_Cascade2Split");
            m_ShadowCascade4SplitProp = serializedObject.FindProperty("m_Cascade4Split");
            m_ShadowDepthBiasProp = serializedObject.FindProperty("m_ShadowDepthBias");
            m_ShadowNormalBiasProp = serializedObject.FindProperty("m_ShadowNormalBias");
            m_SoftShadowsSupportedProp = serializedObject.FindProperty("m_SoftShadowsSupported");
            m_AdaptiveShadowBias = serializedObject.FindProperty("m_AdaptiveShadowBias");
            m_EnableCSMBlend = serializedObject.FindProperty("m_EnableCSMBlend");
            m_CSMBlendDistance = serializedObject.FindProperty("m_CSMBlendDistance");
            m_ShadowType = serializedObject.FindProperty("m_ShadowType");
            m_ShadowPCFFilter = serializedObject.FindProperty("m_ShadowPCFFilter");
            m_PCSSSoftnessProp = serializedObject.FindProperty("m_PCSSSoftness");
            m_PCSSSoftnessFalloff = serializedObject.FindProperty("m_PCSSSoftnessFalloff");
            m_ShadowPrefilterGaussians = serializedObject.FindProperty("m_ShadowPrefitlerGaussianRadius");
            m_ExponentialConstants = serializedObject.FindProperty("m_EVSMExponents");
            m_LightBleedingReduction = serializedObject.FindProperty("m_LightBleedingReduction");
        }

        void DrawShadowSettings()
        {
            m_ShadowSettingsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_ShadowSettingsFoldout.value, Styles.shadowSettingsText);
            if (m_ShadowSettingsFoldout.value)
            {
                EditorGUI.indentLevel++;
                m_ShadowDistanceProp.floatValue = Mathf.Max(0.0f, EditorGUILayout.FloatField(Styles.shadowDistanceText, m_ShadowDistanceProp.floatValue));
                CoreEditorUtils.DrawPopup(Styles.shadowCascadesText, m_ShadowCascadesProp, Styles.shadowCascadeOptions);

                ShadowCascadesOption cascades = (ShadowCascadesOption)m_ShadowCascadesProp.intValue;
                if (cascades == ShadowCascadesOption.FourCascades)
                    EditorUtils.DrawCascadeSplitGUI<Vector3>(ref m_ShadowCascade4SplitProp);
                else if (cascades == ShadowCascadesOption.TwoCascades)
                    EditorUtils.DrawCascadeSplitGUI<float>(ref m_ShadowCascade2SplitProp);
                if (m_ShadowCascadesProp.intValue > 0)
                {
                    EditorGUILayout.PropertyField(m_EnableCSMBlend, Styles.enableCSMBlend);
                    if (m_EnableCSMBlend.boolValue)
                    {
                        m_CSMBlendDistance.floatValue = EditorGUILayout.Slider(Styles.csmBlendDistance, m_CSMBlendDistance.floatValue, 0.0f, 1.0f);
                    }
                }

                m_ShadowDepthBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowDepthBias, m_ShadowDepthBiasProp.floatValue, 
                    0.0f, InsanityPipeline.maxShadowBias);
                m_ShadowNormalBiasProp.floatValue = EditorGUILayout.Slider(Styles.shadowNormalBias, m_ShadowNormalBiasProp.floatValue, 
                    0.0f, InsanityPipeline.maxShadowBias);

                CoreEditorUtils.DrawPopup(Styles.shadowType, m_ShadowType, Styles.shadowTypeOptions);
                ShadowType shadowType = (ShadowType)m_ShadowType.intValue;
                if (shadowType == ShadowType.PCF)
                {
                    CoreEditorUtils.DrawPopup(Styles.shadowPCFFilter, m_ShadowPCFFilter, Styles.shadowPCFFilterOptions);
                    EditorGUILayout.PropertyField(m_SoftShadowsSupportedProp, Styles.supportsSoftShadows);
                }
                else if (shadowType == ShadowType.PCSS)
                {
                    m_PCSSSoftnessProp.floatValue = EditorGUILayout.Slider(Styles.pcssSoftness, m_PCSSSoftnessProp.floatValue, 0.01f, 2.0f);
                    m_PCSSSoftnessFalloff.floatValue = EditorGUILayout.Slider(Styles.pcssSoftnessFalloff, m_PCSSSoftnessFalloff.floatValue, 0.0f, 8.0f);
                }
                else if (shadowType == ShadowType.VSM || shadowType == ShadowType.EVSM)
                {
                    CoreEditorUtils.DrawPopup(Styles.gaussianFilterRadius, m_ShadowPrefilterGaussians, Styles.gaussianFilterRadiusOptions);
                    if (shadowType == ShadowType.EVSM)
                    {
                        EditorGUILayout.PropertyField(m_ExponentialConstants, Styles.exponentialConstants);
                    }
                    m_LightBleedingReduction.floatValue =  EditorGUILayout.Slider(Styles.lightBleedingReduction, 
                        m_LightBleedingReduction.floatValue, 0.0f, 1.0f);
                }

                
                EditorGUILayout.PropertyField(m_AdaptiveShadowBias, Styles.adaptiveShadowBias);


                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}

