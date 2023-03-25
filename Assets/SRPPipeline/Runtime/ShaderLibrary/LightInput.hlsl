#ifndef LIGHT_INPUT_INCLUDED
#define LIGHT_INPUT_INCLUDED

CBUFFER_START(LightVariablesGlobal)
float4 _MainLightPosition;
float4 _MainLightColor;
float  _MainLightIntensity;
float3 pad;
CBUFFER_END

#endif
