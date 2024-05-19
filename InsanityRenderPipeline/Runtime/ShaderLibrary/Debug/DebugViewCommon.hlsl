#ifndef DEBUGVIEW_COMMON
#define DEBUGVIEW_COMMON
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#define DebugTileBasedCullingResult 1
#define DebugDepth                  2
#define DebugLinearDepth 3
#define DebugNormal                 4
#define DebugSSAO 5
#define DebugOverdraw               6

CBUFFER_START(DebugViewVariables)
int _DebugViewMode;
float _ScaleDepth;
float4x4 _ProjInverse;
float2 _DebugViewPad;
CBUFFER_END


#endif
