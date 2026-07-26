#ifndef DEBUGVIEW_COMMON
#define DEBUGVIEW_COMMON
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#define DebugTileBasedCullingResult 1
#define DebugDepth                  2
#define DebugLinearDepth 3
#define DebugNormal                 4
#define DebugSSAO 5
#define DebugAlbedo 6
#define DebugMetallic 7
#define DebugSmoothness 8
#define DebugOverdraw 9

// Must match DebugView.DebugViewVariables in C#.
CBUFFER_START(DebugViewVariables)
int _DebugViewMode;
float _ScaleDepth;
float2 _DebugViewPad;
CBUFFER_END

// Non-jittered GPU inv-proj for LinearDepth (bound on the blit material).
float4x4 _ProjInverse;

#endif
