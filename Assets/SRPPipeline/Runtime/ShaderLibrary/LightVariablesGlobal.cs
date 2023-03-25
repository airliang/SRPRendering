using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    unsafe public struct LightVariablesGlobal
    {
        public Vector4 _MainLightPosition;
        public Color _MainLightColor;
        public float _MainLightIntensity;
        public Vector3 pad;
    }

    unsafe public struct MainLightShadowVariablesGlobal
    {
        [HLSLArray(5, typeof(Matrix4x4))]
        public fixed float _MainLightWorldToShadow[5 * 16];
        public Vector4 _CascadeShadowSplitSpheres0;
        public Vector4 _CascadeShadowSplitSpheres1;
        public Vector4 _CascadeShadowSplitSpheres2;
        public Vector4 _CascadeShadowSplitSpheres3;
        public Vector4 _CascadeShadowSplitSphereRadii;
        public Vector4 _MainLightShadowParams;
        public Vector4 _MainLightShadowmapSize;
    }
}

