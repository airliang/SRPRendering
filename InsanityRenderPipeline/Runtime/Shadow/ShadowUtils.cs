using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Insanity
{
    public class ShadowUtils
    {
        public static void ExtractDirectionalLightData(VisibleLight visibleLight, int shadowResolution, uint cascadeIndex, int cascadeCount, Vector3 cascadeRatios, float nearPlaneOffset, CullingResults cullResults, int lightIndex,
            out Matrix4x4 view, out Matrix4x4 invViewProjection, out Matrix4x4 projection, out Matrix4x4 deviceProjection, out Matrix4x4 deviceProjectionYFlip, out ShadowSplitData splitData)
        {
            Vector4 lightDir;

            //Debug.Assert((uint)viewportSize.x == (uint)viewportSize.y, "Currently the cascaded shadow mapping code requires square cascades.");
            splitData = new ShadowSplitData();
            splitData.cullingSphere.Set(0.0f, 0.0f, 0.0f, float.NegativeInfinity);
            splitData.cullingPlaneCount = 0;

            // This used to be fixed to .6f, but is now configureable.
            splitData.shadowCascadeBlendCullingFactor = .6f;

            // get lightDir
            lightDir = visibleLight.localToWorldMatrix.GetColumn(2);
            // TODO: At some point this logic should be moved to C#, then the parameters cullResults and lightIndex can be removed as well
            //       For directional lights shadow data is extracted from the cullResults, so that needs to be somehow provided here.
            //       Check ScriptableShadowsUtility.cpp ComputeDirectionalShadowMatricesAndCullingPrimitives(...) for details.

            cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(lightIndex, (int)cascadeIndex, cascadeCount, cascadeRatios, shadowResolution, nearPlaneOffset, out view, out projection, out splitData);
            // and the compound (deviceProjection will potentially inverse-Z)
            deviceProjection = GL.GetGPUProjectionMatrix(projection, false);
            deviceProjectionYFlip = GL.GetGPUProjectionMatrix(projection, true);
            InvertOrthographic(ref deviceProjection, ref view, out invViewProjection);
        }

        static void InvertView(ref Matrix4x4 view, out Matrix4x4 invview)
        {
            invview = Matrix4x4.zero;
            invview.m00 = view.m00; invview.m01 = view.m10; invview.m02 = view.m20;
            invview.m10 = view.m01; invview.m11 = view.m11; invview.m12 = view.m21;
            invview.m20 = view.m02; invview.m21 = view.m12; invview.m22 = view.m22;
            invview.m33 = 1.0f;
            invview.m03 = -(invview.m00 * view.m03 + invview.m01 * view.m13 + invview.m02 * view.m23);
            invview.m13 = -(invview.m10 * view.m03 + invview.m11 * view.m13 + invview.m12 * view.m23);
            invview.m23 = -(invview.m20 * view.m03 + invview.m21 * view.m13 + invview.m22 * view.m23);
        }

        static void InvertOrthographic(ref Matrix4x4 proj, ref Matrix4x4 view, out Matrix4x4 vpinv)
        {
            Matrix4x4 invview;
            InvertView(ref view, out invview);

            Matrix4x4 invproj = Matrix4x4.zero;
            invproj.m00 = 1.0f / proj.m00;
            invproj.m11 = 1.0f / proj.m11;
            invproj.m22 = 1.0f / proj.m22;
            invproj.m33 = 1.0f;
            invproj.m03 = proj.m03 * invproj.m00;
            invproj.m13 = proj.m13 * invproj.m11;
            invproj.m23 = -proj.m23 * invproj.m22;

            vpinv = invview * invproj;
        }

        public static int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount)
        {
            int resolution = Mathf.Min(atlasWidth, atlasHeight);
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            while (currentTileCount < tileCount)
            {
                resolution = resolution >> 1;
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;
        }

        public static void ApplySliceTransform(ref Matrix4x4 worldToShadow, int atlasWidth, int atlasHeight, int resolution, int offsetX, int offsetY)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = resolution * oneOverAtlasWidth;
            sliceTransform.m11 = resolution * oneOverAtlasHeight;
            sliceTransform.m03 = offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            worldToShadow = sliceTransform * worldToShadow;
        }

        public static Matrix4x4 GetWorldToShadowTransform(Matrix4x4 proj, Matrix4x4 view, int cascadeCount, int cascadeIndex, int shadowWidth, 
            int shadowHeight, int shadowResolution, int offsetX, int offsetY)
        {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer)
            {
                //proj.m20 = -proj.m20;
                //proj.m21 = -proj.m21;
                //proj.m22 = -proj.m22;
                //proj.m23 = -proj.m23;
            }

            Matrix4x4 worldToShadow = proj * view;

            var textureScaleAndBias = Matrix4x4.identity;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore)
            {
                textureScaleAndBias.m00 = 0.5f;
                textureScaleAndBias.m11 = 0.5f;
                textureScaleAndBias.m22 = 0.5f;
                textureScaleAndBias.m03 = 0.5f;
                textureScaleAndBias.m13 = 0.5f;
                textureScaleAndBias.m23 = 0.5f;
                textureScaleAndBias.m33 = 1;
            }
            else
            {
                textureScaleAndBias.m00 = 0.5f;
                textureScaleAndBias.m11 = 0.5f;
                textureScaleAndBias.m22 = 1;// 0.5f;
                textureScaleAndBias.m03 = 0.5f;
                textureScaleAndBias.m13 = 0.5f;
                textureScaleAndBias.m23 = 0;// 0.5f;
            }


            // Apply texture scale and offset to save a MAD in shader.
            worldToShadow = textureScaleAndBias * worldToShadow;
            if (cascadeCount > 1)
            {
                ApplySliceTransform(ref worldToShadow, shadowWidth, shadowHeight, shadowResolution, offsetX, offsetY);
            }
            return worldToShadow;
        }

        public static Vector4 GetShadowBias(Light shadowLight, int shadowLightIndex, ShadowSettings shadowSetting, Matrix4x4 lightProjectionMatrix, float shadowResolution)
        {
            if (shadowLightIndex < 0/* || shadowLightIndex >= shadowData.bias.Count*/)
            {
                Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
                return Vector4.zero;
            }

            float frustumSize;
            if (shadowLight.type == LightType.Directional)
            {
                // Frustum size is guaranteed to be a cube as we wrap shadow frustum around a sphere
                frustumSize = 2.0f / lightProjectionMatrix.m00;
            }
            else if (shadowLight.type == LightType.Spot)
            {
                // For perspective projections, shadow texel size varies with depth
                // It will only work well if done in receiver side in the pixel shader. Currently UniversalRP
                // do bias on caster side in vertex shader. When we add shader quality tiers we can properly
                // handle this. For now, as a poor approximation we do a constant bias and compute the size of
                // the frustum as if it was orthogonal considering the size at mid point between near and far planes.
                // Depending on how big the light range is, it will be good enough with some tweaks in bias
                frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range;
            }
            else
            {
                Debug.LogWarning("Only spot and directional shadow casters are supported in universal pipeline");
                frustumSize = 0.0f;
            }

            // depth and normal bias scale is in shadowmap texel size in world space
            float texelSize = frustumSize / shadowResolution;
            float biasScale = 10;

            float depthBias = -1;//-shadowSetting.depthBias * texelSize;
            float normalBias = -1;//-shadowSetting.normalBias * texelSize;

            if (shadowSetting.adaptiveShadowBias)
            {
                float R = 1.0f;
                //if (shadowSetting.supportSoftShadow)
                //    R = 3.0f;
                biasScale *= R * 0.5f * texelSize;
                //return new Vector4(biasScale, 0.0f, 0.0f, 0.0f);
                depthBias = shadowSetting.depthBias;
                normalBias = shadowSetting.normalBias;
            }
            else
            {
                depthBias = -shadowSetting.depthBias * texelSize;
                normalBias = -shadowSetting.normalBias * texelSize;
            }
            

            //if (shadowSetting.supportSoftShadow)
            {
                // TODO: depth and normal bias assume sample is no more than 1 texel away from shadowmap
                // This is not true with PCF. Ideally we need to do either
                // cone base bias (based on distance to center sample)
                // or receiver place bias based on derivatives.
                // For now we scale it by the PCF kernel size (5x5)
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
                //biasScale *= kernelRadius;
            }


            return new Vector4(depthBias, normalBias, biasScale, 0.0f);
        }
    }
}

