using Insanity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Windows;

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
        //Atmosphere atmosphere = new Atmosphere();
        Texture3D skyboxLUT = Atmosphere.PrecomputeSkyboxLUT(_AtmosphereResources) as Texture3D;
        //skyboxLUT.Apply();
        if (skyboxLUT != null)
        {
            string path = AssetDatabase.GetAssetPath(_AtmosphereResources);
            int index = path.LastIndexOf('/');
            if (index >= 0)
            {
                path = path.Substring(0, index + 1);
            }

            //if (AssetDatabase.Contains(_AtmosphereResources.SkyboxLUT))
            {
                AssetDatabase.DeleteAsset(path + "SkyboxLUT.asset");
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(skyboxLUT, path + "SkyboxLUT.asset");

            _AtmosphereResources.SkyboxLUT = AssetDatabase.LoadAssetAtPath(path + "SkyboxLUT.asset", typeof(Texture3D)) as Texture;
            //EditorUtility.SetDirty(_AtmosphereResources);
            
            //Texture3D testAsset = AssetDatabase.LoadAssetAtPath(path + "test3DTex.asset", typeof(Texture3D)) as Texture3D; 
            //if (testAsset == null) 
            //{
            //    testAsset = new Texture3D(2, 2, 2, TextureFormat.RGBAHalf, false);
            //    testAsset.SetPixels(new Color[] { Color.red, Color.green, Color.blue, Color.white, Color.black, Color.yellow, Color.cyan, Color.magenta });
            //    testAsset.Apply();
            //    AssetDatabase.CreateAsset(testAsset, path + "test3DTex.asset");
            //}
            

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private void BakeAtmosphereSHTest()
    {
        //Atmosphere atmosphere = new Atmosphere();
        //atmosphere.ClearSamples();
        //atmosphere.BakeSkyToSHAmbient(_AtmosphereResources, _SunLight);
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
