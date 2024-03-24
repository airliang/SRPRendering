using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static UnityEditor.Insanity.BaseShaderGUI;
using System;
using static UnityEditor.Lightmapping;

namespace UnityEditor.Insanity
{
    public class InsanityLitShaderGUI : BaseShaderGUI
    {
        public enum WorkflowMode
        {
            /// <summary>
            /// Use this for specular workflow.
            /// </summary>
            Specular = 0,

            /// <summary>
            /// Use this for metallic workflow.
            /// </summary>
            Metallic
        }

        /// <summary>
        /// Options to select the texture channel where the smoothness value is stored.
        /// </summary>
        public enum SmoothnessMapChannel
        {
            /// <summary>
            /// Use this when smoothness is stored in the alpha channel of the Specular/Metallic Map.
            /// </summary>
            SpecularMetallicAlpha,

            /// <summary>
            /// Use this when smoothness is stored in the alpha channel of the Albedo Map.
            /// </summary>
            AlbedoAlpha,
        }

        public static class Styles
        {
            /// <summary>
            /// The text and tooltip for the workflow Mode GUI.
            /// </summary>
            public static GUIContent workflowModeText = EditorGUIUtility.TrTextContent("Workflow Mode",
                "Select a workflow that fits your textures. Choose between Metallic or Specular.");

            /// <summary>
            /// The text and tooltip for the specular Map GUI.
            /// </summary>
            public static GUIContent specularMapText =
                EditorGUIUtility.TrTextContent("Specular Map", "Designates a Specular Map and specular color determining the apperance of reflections on this Material's surface.");

            /// <summary>
            /// The text and tooltip for the metallic Map GUI.
            /// </summary>
            public static GUIContent metallicMapText =
                EditorGUIUtility.TrTextContent("Metallic Map", "Sets and configures the map for the Metallic workflow.");

            /// <summary>
            /// The text and tooltip for the smoothness GUI.
            /// </summary>
            public static GUIContent smoothnessText = EditorGUIUtility.TrTextContent("Smoothness",
                "Controls the spread of highlights and reflections on the surface.");

            /// <summary>
            /// The text and tooltip for the smoothness source GUI.
            /// </summary>
            public static GUIContent smoothnessMapChannelText =
                EditorGUIUtility.TrTextContent("Source",
                    "Specifies where to sample a smoothness map from. By default, uses the alpha channel for your map.");

            /// <summary>
            /// The text and tooltip for the specular Highlights GUI.
            /// </summary>
            public static GUIContent highlightsText = EditorGUIUtility.TrTextContent("Specular Highlights",
                "When enabled, the Material reflects the shine from direct lighting.");

            /// <summary>
            /// The text and tooltip for the environment Reflections GUI.
            /// </summary>
            public static GUIContent reflectionsText =
                EditorGUIUtility.TrTextContent("Environment Reflections",
                    "When enabled, the Material samples reflections from the nearest Reflection Probes or Lighting Probe.");

            /// <summary>
            /// The text and tooltip for the height map GUI.
            /// </summary>
            public static GUIContent heightMapText = EditorGUIUtility.TrTextContent("Height Map",
                "Defines a Height Map that will drive a parallax effect in the shader making the surface seem displaced.");

            /// <summary>
            /// The text and tooltip for the occlusion map GUI.
            /// </summary>
            public static GUIContent occlusionText = EditorGUIUtility.TrTextContent("Occlusion Map",
                "Sets an occlusion map to simulate shadowing from ambient lighting.");

            /// <summary>
            /// The names for smoothness alpha options available for metallic workflow.
            /// </summary>
            public static readonly string[] metallicSmoothnessChannelNames = { "Metallic Alpha", "Albedo Alpha" };

            /// <summary>
            /// The names for smoothness alpha options available for specular workflow.
            /// </summary>
            public static readonly string[] specularSmoothnessChannelNames = { "Specular Alpha", "Albedo Alpha" };

            /// <summary>
            /// The text and tooltip for the enabling/disabling clear coat GUI.
            /// </summary>
            public static GUIContent clearCoatText = EditorGUIUtility.TrTextContent("Clear Coat",
                "A multi-layer material feature which simulates a thin layer of coating on top of the surface material." +
                "\nPerformance cost is considerable as the specular component is evaluated twice, once per layer.");

            /// <summary>
            /// The text and tooltip for the clear coat Mask GUI.
            /// </summary>
            public static GUIContent clearCoatMaskText = EditorGUIUtility.TrTextContent("Mask",
                "Specifies the amount of the coat blending." +
                "\nActs as a multiplier of the clear coat map mask value or as a direct mask value if no map is specified." +
                "\nThe map specifies clear coat mask in the red channel and clear coat smoothness in the green channel.");

            /// <summary>
            /// The text and tooltip for the clear coat smoothness GUI.
            /// </summary>
            public static GUIContent clearCoatSmoothnessText = EditorGUIUtility.TrTextContent("Smoothness",
                "Specifies the smoothness of the coating." +
                "\nActs as a multiplier of the clear coat map smoothness value or as a direct smoothness value if no map is specified.");
        }

        // Properties
        //private LitGUI.LitProperties litProperties;
        public MaterialProperty workflowMode;

        // Surface Input Props

        /// <summary>
        /// The MaterialProperty for metallic value.
        /// </summary>
        public MaterialProperty metallic;

        /// <summary>
        /// The MaterialProperty for specular color.
        /// </summary>
        public MaterialProperty specColor;

        /// <summary>
        /// The MaterialProperty for metallic Smoothness map.
        /// </summary>
        public MaterialProperty metallicGlossMap;

        /// <summary>
        /// The MaterialProperty for specular smoothness map.
        /// </summary>
        public MaterialProperty specGlossMap;

        /// <summary>
        /// The MaterialProperty for smoothness value.
        /// </summary>
        public MaterialProperty smoothness;

        /// <summary>
        /// The MaterialProperty for smoothness alpha channel.
        /// </summary>
        public MaterialProperty smoothnessMapChannel;


        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            //litProperties = new LitGUI.LitProperties(properties);

            workflowMode = BaseShaderGUI.FindProperty("_WorkflowMode", properties, false);
            // Surface Input Props
            metallic = BaseShaderGUI.FindProperty("_Metallic", properties);
            specColor = BaseShaderGUI.FindProperty("_SpecColor", properties, false);
            metallicGlossMap = BaseShaderGUI.FindProperty("_MetallicGlossMap", properties);
            specGlossMap = BaseShaderGUI.FindProperty("_SpecGlossMap", properties, false);
            smoothness = BaseShaderGUI.FindProperty("_Smoothness", properties, false);
            smoothnessMapChannel = BaseShaderGUI.FindProperty("_SmoothnessTextureChannel", properties, false);
        }

        // material changed check
        public override void MaterialChanged(Material material)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            SetMaterialKeywords(material);
        }

        internal bool IsOpaque(Material material)
        {
            bool opaque = true;
            if (material.HasProperty("_Surface"))
                opaque = ((BaseShaderGUI.SurfaceType)material.GetFloat("_Surface") == BaseShaderGUI.SurfaceType.Opaque);
            return opaque;
        }

        public void DoSmoothness(MaterialEditor materialEditor, Material material, MaterialProperty smoothness, MaterialProperty smoothnessMapChannel, string[] smoothnessChannelNames)
        {
            EditorGUI.indentLevel += 2;

            materialEditor.ShaderProperty(smoothness, Styles.smoothnessText);

            if (smoothnessMapChannel != null) // smoothness channel
            {
                var opaque = IsOpaque(material);
                EditorGUI.indentLevel++;
                EditorGUI.showMixedValue = smoothnessMapChannel.hasMixedValue;
                if (opaque)
                {
                    MaterialEditor.BeginProperty(smoothnessMapChannel);
                    EditorGUI.BeginChangeCheck();
                    var smoothnessSource = (int)smoothnessMapChannel.floatValue;
                    smoothnessSource = EditorGUILayout.Popup(Styles.smoothnessMapChannelText, smoothnessSource, smoothnessChannelNames);
                    if (EditorGUI.EndChangeCheck())
                        smoothnessMapChannel.floatValue = smoothnessSource;
                    MaterialEditor.EndProperty();
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Popup(Styles.smoothnessMapChannelText, 0, smoothnessChannelNames);
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUI.showMixedValue = false;
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel -= 2;
        }

        public void DoMetallicSpecularArea(MaterialEditor materialEditor, Material material)
        {
            string[] smoothnessChannelNames;
            bool hasGlossMap = false;
            if (workflowMode == null ||
                (WorkflowMode)workflowMode.floatValue == WorkflowMode.Metallic)
            {
                hasGlossMap = metallicGlossMap.textureValue != null;
                smoothnessChannelNames = Styles.metallicSmoothnessChannelNames;
                materialEditor.TexturePropertySingleLine(Styles.metallicMapText, metallicGlossMap,
                    hasGlossMap ? null : metallic);
            }
            else
            {
                hasGlossMap = specGlossMap.textureValue != null;
                smoothnessChannelNames = Styles.specularSmoothnessChannelNames;
                BaseShaderGUI.TextureColorProps(materialEditor, Styles.specularMapText, specGlossMap,
                    hasGlossMap ? null : specColor);
            }
            DoSmoothness(materialEditor, material, smoothness, smoothnessMapChannel, smoothnessChannelNames);
        }

        // material main surface options
        public override void DrawSurfaceOptions(Material material)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            base.DrawSurfaceOptions(material);
        }

        // material main surface inputs
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            //LitGUI.Inputs(litProperties, materialEditor, material);
            //DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);

            BaseShaderGUI.DrawNormalArea(materialEditor, normalMapProp);

            DoMetallicSpecularArea(materialEditor, material);
        }

        // material main advanced options
        public override void DrawAdvancedOptions(Material material)
        {
            //if (litProperties.reflections != null && litProperties.highlights != null)
            //{
            //    EditorGUI.BeginChangeCheck();
            //    materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
            //    materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
            //    materialEditor.ShaderProperty(litProperties.vertexColor, LitGUI.Styles.vertexColor);
            //    if (EditorGUI.EndChangeCheck())
            //    {
            //        MaterialChanged(material);
            //    }
            //}

            base.DrawAdvancedOptions(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasProperty("_Emission"))
            {
                //material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            /*
            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                // NOTE: legacy shaders did not provide physically based transparency
                // therefore Fade mode
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Surface", (float)surfaceType);
            material.SetFloat("_Blend", (float)blendMode);

            if (oldShader.name.Equals("Standard (Specular setup)"))
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
                Texture texture = material.GetTexture("_SpecGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
                Texture texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            */

            MaterialChanged(material);
        }
    }
}

