---
title: 'Understanding Unity Projection Matrix'
date: 2024-03-20
permalink: /posts/understanding_unity_projection_matrix.md
tags:
  - cool posts
  - unity
  - math
---
# Understanding Unity Projection Matrix
## Matrix Tricks
There are some tricks for Unity camera matrices we have to make clear here.
First of all, unity camera space in the device is based on the right-hand coordinate. So the camera matrix will reverse z.
__Camera.worldToCameraMatrix__. This matrix will reverse z to -z. And also the view matrix in shader is using this matrix. Because unity uses the right coordinate as the view space.
__Camera.transform.worldToLocalMatrix__. We can consider this matrix as the standard view space matrix in left coordinate. (don't reverse z)
__Camera.projectionMatrix__. This matrix transforms the coordinates from camera space to the clip space([-1,-1,-1], [1, 1, 1]), and reverses z to -z. Besides, it translates the z from -1 to 1.
__GL.GetGPUProjectionMatrix()__. This function returns a matrix used in shader. Because Unity supports different graphics api. In opengl, the depth is in [-1, 1], while in DX or Vulkan, the depth is in [0, 1] or [1, 0]. So this function returns a matrix according to the current graphics api.

## Coordinate Transform
Here is the case when using __Camera.worldToCameraMatrix__ as view matrix and __GL.GetGPUProjectionMatrix()__ as projection matrix.
When we have the depth buffer and uv coordinates of the screen, how to translate the screen point to the real world matrix?
First, get the depth value from the depth buffer.
Second, translate the ndc position to the view space position. See the function below from the unity core package:
```C#
//The below codes are from com.unity.render-pipelines.core@14.0.9\ShaderLibrary\Common.hlsl

float4 ComputeClipSpacePosition(float2 positionNDC, float deviceDepth)
{
    float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);

#if UNITY_UV_STARTS_AT_TOP
    // Our world space, view space, screen space and NDC space are Y-up.
    // Our clip space is flipped upside-down due to poor legacy Unity design.
    // The flip is baked into the projection matrix, so we only have to flip
    // manually when going from CS to NDC and back.
    positionCS.y = -positionCS.y;
#endif

    return positionCS;
}

float3 ComputeViewSpacePosition(float2 positionNDC, float deviceDepth, float4x4 invProjMatrix)
{
    float4 positionCS = ComputeClipSpacePosition(positionNDC, deviceDepth);
    float4 positionVS = mul(invProjMatrix, positionCS);
    // The view space uses a right-handed coordinate system.
    positionVS.z = -positionVS.z;
    return positionVS.xyz / positionVS.w;
}

//first, get the depth from depth buffer regardless it's revert depth or not.
float depth = _DepthTexture.SampleLevel(s_point_clamp_sampler, uvScreen, 0);

//second, calculate the position in camera space
float3 screenPosVS = ComputeViewSpacePosition(uvScreen, depth, _ProjInverse);
```

>Note: If we use __GL.GetGPUProjectionMatrix()__ as the projection matrix in shader, we don't have to reverse depth( depth = 1 - depth ) in DX or Vulkan platform.

***
When using __Camera.transform.worldToLocalMatrix__ and __Camera.projectionMatrix__ to translate the ndc coordinate to View Space Coordinate, we need to process by ourselves.
Here is the C# codes:
```C#
Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix;
proj = proj * Matrix4x4.Scale(new Vector3(1, 1, -1));
//set proj to shader as projection matrix
```
Get the camera space coordinate in shader:
```C
float3 UVtoViewPos(float2 uv, float depth)
{
    float2 clippos = uv * 2.0 - 1.0;
    
    float4 posView = mul(_ProjInverse, float4(clippos, 2.0 * depth - 1.0, 1));
    posView /= posView.w;
    return posView.xyz;
}

float depth = _DepthTexture.SampleLevel(s_point_clamp_sampler, uvScreen, 0);
//we need to reverse depth manually
#if REVERSE_Z
depth = 1.0 - depth;
#endif

float3 screenPosVS = UVtoViewPos(uvScreen, depth);
```
>Note: When using __Camera.transform.worldToLocalMatrix__ and __Camera.projectionMatrix__, we have to remap the depth from [0, 1] to [-1, 1]. (depth = 2.0 * depth - 1).