using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    public class GlobalRenderSettings
    {
        public static bool HDREnable = false;
        public static float HDRExposure = 1.0f;
        public static float ResolutionRate = 1.0f;
        public static Rect screenResolution;
        public static DepthBits depthBits = DepthBits.Depth32;
    }
}

