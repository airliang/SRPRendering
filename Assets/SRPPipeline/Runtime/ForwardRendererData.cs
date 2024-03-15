using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Insanity
{
    public class ForwardRendererData : RenderPathData
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/ForwardRendererData", priority = 1)]
        static void CreateForwardRendererData()
        {
            var instance = ScriptableObject.CreateInstance<ForwardRendererData>();
            UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRPPipeline/data/ForwardRendererData.asset");
        }

        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/ForwardRenderPathResources", priority = 1)]
        static void CreateForwardRenderPathResources()
        {
            var newAsset = CreateInstance<ForwardRenderPathResources>();
            string pathName = AssetDatabase.GetAssetPath(Selection.activeObject) + "/ForwardRenderPathResources.asset";
            newAsset.name = Path.GetFileName(pathName);
            UnityEditor.AssetDatabase.CreateAsset(newAsset, pathName);
        }
#endif
        [SerializeField] ForwardRenderPathResources m_ForwardPathResources;
        [SerializeField] int m_TileSize = 16;
        //[SerializeField] bool m_AdditionalLightEnable = true;

        public ForwardRenderPathResources ForwardPathResources
        {
            get { return m_ForwardPathResources; }
            set { m_ForwardPathResources = value; }
        }

        public int TileSize
        {
            get { return m_TileSize; }
            set { m_TileSize = value; }
        }

        //public bool AdditionalLightEnable
        //{
        //    get { return m_AdditionalLightEnable; }
        //    set { m_AdditionalLightEnable= value; }
        //}

        public override RenderPath Create(InsanityPipeline pipeline)
        {
            return new ForwardRenderPath(this, pipeline);
        }
    }
}

