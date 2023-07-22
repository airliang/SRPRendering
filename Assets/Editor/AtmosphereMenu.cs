using Insanity;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class AtmosphereWindow : EditorWindow
{
    static AtmosphereWindow _thisInstance;
    Editor editor;
    public AtmosphereResources _AtmosphereResources;
    public Light _SunLight;

    [MenuItem("Insanity/Atmosphere LUT Precompute", false, 200)]
    static void ShowWindow()
    {
        AtmosphereWindow window = GetWindow<AtmosphereWindow>();
        window.Show();
        InsanityPipelineAsset asset = GraphicsSettings.currentRenderPipeline as InsanityPipelineAsset;
        window._AtmosphereResources = asset.AtmosphereResources;
    }

    private void Precompute()
    {
        Atmosphere atmosphere = new Atmosphere();
        Texture3D skyboxLUT = atmosphere.PrecomputeSkyboxLUT(_AtmosphereResources);
        if (skyboxLUT != null)
        {
            string path = AssetDatabase.GetAssetPath(_AtmosphereResources);
            int index = path.LastIndexOf('/');
            if (index >= 0)
            {
                path = path.Substring(0, index + 1);
            }
            AssetDatabase.CreateAsset(skyboxLUT, path + "SkyboxLUT.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _AtmosphereResources.SkyboxLUT = AssetDatabase.LoadAssetAtPath(path + "SkyboxLUT.asset", typeof(Texture3D)) as Texture3D;
        }
    }

    private void BakeAtmosphereSHTest()
    {
        Atmosphere atmosphere = new Atmosphere();
        atmosphere.ClearSamples();
        atmosphere.BakeSkyToSHAmbient(_AtmosphereResources, _SunLight);
    }

    private void OnGUI()
    {
        if (!editor)
        { editor = Editor.CreateEditor(this); }
        if (editor) { editor.OnInspectorGUI(); }
        EditorGUILayout.Space();
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        if (GUILayout.Button("Precompute Atmosphere LUT"))
        {
            Precompute();
        }

        if (GUILayout.Button("Bake Atmosphere spherical harnomics"))
        {
            if (_SunLight != null)
            {
                BakeAtmosphereSHTest();
            }
        }
    }
}
