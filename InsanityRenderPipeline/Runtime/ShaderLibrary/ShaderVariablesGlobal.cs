using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Insanity
{
    unsafe public struct ShaderVariablesGlobal
    {
        public Matrix4x4 _ViewMatrix;
        public Matrix4x4 _CameraViewMatrix;
        public Matrix4x4 _InvViewMatrix;
        public Matrix4x4 _ProjMatrix;
        public Matrix4x4 _InvProjMatrix;
        public Matrix4x4 _ViewProjMatrix;
        public Matrix4x4 _CameraViewProjMatrix;
        public Matrix4x4 _InvViewProjMatrix;
        public Matrix4x4 _PixelCoordToViewDirWS;
        public Vector4 _WorldSpaceCameraPos_Internal;
        public Vector4 _ScreenSize;
        // Values used to linearize the Z buffer (http://www.humus.name/temp/Linearize%20depth.txt)
        // x = 1-far/near
        // y = far/near
        // z = x/far
        // w = y/far
        // or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
        // x = -1+far/near
        // y = 1
        // z = x/far
        // w = 1/far
        public Vector4 _ZBufferParams;

        // x = 1 or -1 (-1 if projection is flipped)
        // y = near plane
        // z = far plane
        // w = 1/far plane
        public Vector4 _ProjectionParams;

        // x = orthographic camera's width
        // y = orthographic camera's height
        // z = unused
        // w = 1.0 if camera is ortho, 0.0 if perspective
        public Vector4 unity_OrthoParams;

        // x = width
        // y = height
        // z = 1 + 1.0/width
        // w = 1 + 1.0/height
        public Vector4 _ScreenParams;
        public Vector4 _Time;
        public Vector4 _SinTime;
        public Vector4 _CosTime;
        public Vector4 unity_DeltaTime;

        public int _FrameIndex;
        public Vector3 _Pad0;
    }
}


