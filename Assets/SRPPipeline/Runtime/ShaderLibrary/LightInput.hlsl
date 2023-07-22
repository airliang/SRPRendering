#ifndef LIGHT_INPUT_INCLUDED
#define LIGHT_INPUT_INCLUDED

CBUFFER_START(LightVariablesGlobal)
float4 _MainLightPosition;
float4 _MainLightColor;
float  _MainLightIntensity;
float3 pad;
CBUFFER_END

CBUFFER_START(AmbientSH)
float4 _SHAr;
float4 _SHAg;
float4 _SHAb;
float4 _SHBr;
float4 _SHBg;
float4 _SHBb;
float4 _SHC;
CBUFFER_END

#endif
