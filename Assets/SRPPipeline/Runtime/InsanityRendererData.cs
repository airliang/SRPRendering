using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Insanity
{
    public class RendererData : ScriptableObject
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/InsanityRendererData", priority = 1)]
        static void CreateRendererData()
        {
            var instance = ScriptableObject.CreateInstance<RendererData>();
            UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRPPipeline/data/RendererData.asset");
        }

        [UnityEditor.MenuItem("Assets/Create/Render Pipeline/RendererDataResources", priority = 1)]
        static void CreateRendererDataResources()
        {
            var newAsset = CreateInstance<RendererDataResources>();
            string pathName = AssetDatabase.GetAssetPath(Selection.activeObject) + "/RendererDataResources.asset";
            newAsset.name = Path.GetFileName(pathName);
            UnityEditor.AssetDatabase.CreateAsset(newAsset, pathName);
        }
#endif
        public enum eRenderingPath
        {
            /// <summary>Render all objects and lighting in one pass, with a hard limit on the number of lights that can be applied on an object.</summary>
            Forward = 0,
            /// <summary>Render all objects first in a g-buffer pass, then apply all lighting in a separate pass using deferred shading.</summary>
            Deferred = 1
        };
        [SerializeField] RendererDataResources m_Resources;
        [SerializeField] eRenderingPath m_RenderingPath;
        [SerializeField] int m_TileSize = 16;
        //[SerializeField] bool m_AdditionalLightEnable = true;

        public RendererDataResources DataResources
        {
            get { return m_Resources; }
            set { m_Resources = value; }
        }

        public eRenderingPath RenderingPath
        {
            get { return m_RenderingPath; }
            set { m_RenderingPath = value; }
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

        public InsanityRenderer Create(InsanityPipeline pipeline)
        {
            return new InsanityRenderer(this, pipeline);
        }
    }
}

