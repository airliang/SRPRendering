using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    public class AtmosphereResources : RenderPipelineResources
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/AtmosphereResource", priority = 1)]
        static void CreateInsanityPipelineResources()
        {
            var newAsset = CreateInstance<AtmosphereResources>();
            string pathName = AssetDatabase.GetAssetPath(Selection.activeObject) + "/AtmosphereResources.asset";
            newAsset.name = Path.GetFileName(pathName);
            UnityEditor.AssetDatabase.CreateAsset(newAsset, pathName);
        }
#endif
        [Reload("Runtime/ShaderLibrary/PrecomputeScattering.compute")]
        public ComputeShader PrecomputeScattering;
        public ComputeShader ProjAtmosphereToSH;
        public ComputeShader BakeToCubemap;
        public ComputeShader DownSampleCubemap;
        public ComputeShader PrefilterSpecularCubemap;
        public Texture SkyboxLUT;

        public Vector3 ScatteringCoefficientRayleigh = new Vector3(6.55f, 17.3f, 23.0f);
        public Vector3 ScatteringCoefficientMie = new Vector3(2.0f, 2.0f, 2.0f);
        public float ScaleRayleigh = 1.0f;
        public float ScaleMie = 1.0f;
        public float MieG = 0.6f;
        public bool RenderSun = true;
        public int MultipleScatteringOrder = 0;
    }
}

