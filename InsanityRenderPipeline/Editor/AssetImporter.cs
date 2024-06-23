using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;

public class AssetImporter : AssetPostprocessor
{
    void OnPostprocessModel(GameObject g)
    {
        //assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g);
        //if (assetPath == string.Empty)
        //    return;
        ExtractMaterials(g);
    }

    private void OnPostprocessMaterial(Material material)
    {
        
    }

    void ExtractMaterials(GameObject g)
    {
        //Try to extract materials into a subfolder
        var assetsToReload = new HashSet<string>();
        var materials = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(x => x.GetType() == typeof(Material)).ToArray();
        string destinationPath = Directory.CreateDirectory(Path.GetDirectoryName(assetPath) + "\\Materials").FullName + "\\";
        Debug.Log(assetPath + " has " + materials.Length + " materials");
        foreach (var material in materials)
        {
            var newAssetPath = destinationPath + material.name + ".mat";
            newAssetPath = AssetDatabase.GenerateUniqueAssetPath(newAssetPath);

            var error = AssetDatabase.ExtractAsset(material, newAssetPath);
            if (String.IsNullOrEmpty(error))
            {
                assetsToReload.Add(assetPath);
            }
        }

        foreach (var path in assetsToReload)
        {
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        MeshRenderer[] meshRenderers = g.GetComponentsInChildren<MeshRenderer>();
        foreach (var meshRenderer in meshRenderers)
        {
            Material[] mats = meshRenderer.sharedMaterials;
            int materialIndex = 0;
            foreach (var material in mats)
            {
                var newAssetPath = destinationPath + material.name + ".mat";
                newAssetPath = AssetDatabase.GenerateUniqueAssetPath(newAssetPath);
                if (material.shader != null && material.shader.name == "Standard")
                {
                    Texture albedo = material.GetTexture("_MainTex");
                    Texture normal = material.GetTexture("_BumpMap");
                    float metallic = material.GetFloat("_Metallic");
                    float smooth = material.GetFloat("_Glossiness");
                    float cutoff = material.GetFloat("_Cutoff");

                    //var insanityMat = new Material(Shader.Find("Insanity/Lit"));
                    //mats[materialIndex] = insanityMat;
                    materialIndex++;
                    
                    material.shader = Shader.Find("Insanity/Lit");
                    material.SetTexture("_BaseMap", albedo);
                    material.SetTexture("_NormalMap", normal);
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Smoothness", smooth);
                    material.SetFloat("_Cutoff", cutoff);
                }
                
                var error = AssetDatabase.ExtractAsset(material, newAssetPath);
                if (String.IsNullOrEmpty(error))
                {
                    assetsToReload.Add(assetPath);
                }
            }
            
        }
    }
}
