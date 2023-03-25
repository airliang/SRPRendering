//copy from hdrp shadervariablematrixdefshdcamera.hlsl

#ifndef UNITY_SHADER_VARIABLES_MATRIX_CAMERA_INCLUDED
#define UNITY_SHADER_VARIABLES_MATRIX_CAMERA_INCLUDED

#define UNITY_MATRIX_V     _ViewMatrix
#define UNITY_MATRIX_I_V   _InvViewMatrix
#define UNITY_MATRIX_P     OptimizeProjectionMatrix(_ProjMatrix)
#define UNITY_MATRIX_I_P   _InvProjMatrix
#define UNITY_MATRIX_VP    _ViewProjMatrix
#define UNITY_MATRIX_I_VP  _InvViewProjMatrix
#define UNITY_MATRIX_UNJITTERED_VP _NonJitteredViewProjMatrix
#define UNITY_MATRIX_PREV_VP _PrevViewProjMatrix
#define UNITY_MATRIX_PREV_I_VP _PrevInvViewProjMatrix


#endif // UNITY_SHADER_VARIABLES_MATRIX_DEFS_HDCAMERA_INCLUDED
