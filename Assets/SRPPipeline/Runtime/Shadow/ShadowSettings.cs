using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Insanity
{
    public class ShadowSettings// : ScriptableObject
    {
        public bool supportsMainLightShadows;
        public bool requiresScreenSpaceShadowResolve;
        public float screenSpaceShadowScale = 1.0f;
        //public int mainLightShadowmapWidth = 512;
        //public int mainLightShadowmapHeight = 512;
        public int mainLightResolution = 512;
        public int mainLightShadowCascadesCount = 1;
        float m_Cascade2Split = 0.25f;
        Vector3 m_Cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
        public float depthBias = 1.0f;
        public float normalBias = 0.8f;
        //public bool supportSoftShadow = false;
        public eShadowType shadowType = eShadowType.PCF;
        public eShadowPCFFilter shadowPCFFilter = eShadowPCFFilter.PCF_None;
        public bool adaptiveShadowBias = true;
        public float maxShadowDistance = 0;
        public bool csmBlendEnable = false;
        public float csmBlendDistance = 0;
        public float pcssSoftness = 1.0f;
        public float pcssSoftnessFalloff = 2.0f;
        public bool vsmSatEnable = false;
        public Vector2 exponentialConstants = new Vector2(10.0f, 10.0f);
        public float lightBleedingReduction = 0.5f;
        public eGaussianRadius prefilterGaussianRadius = eGaussianRadius.eGausian3x3;

        public float cascade2Split
        {
            get { return m_Cascade2Split; }
            set { m_Cascade2Split = value; }
        }

        public Vector3 cascade4Split
        {
            get { return m_Cascade4Split; }
            set { m_Cascade4Split = value; }
        }

        public static GraphicsFormat GetShadowmapFormat(eShadowType shadowType)
        {
            switch (shadowType)
            {
                case eShadowType.VSM:
                    return GraphicsFormat.R16G16_UNorm;
                case eShadowType.EVSM:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                default:
                    return GraphicsFormat.R16_UNorm;
            }
        }
    }
}

