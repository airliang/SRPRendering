using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Insanity
{
    public class CameraData
    {
        public static int s_CameraRelativeRendering = 1;
        public struct ViewConstants
        {
            /// <summary>View matrix.</summary>
            public Matrix4x4 viewMatrix;
            /// <summary>Inverse View matrix.</summary>
            public Matrix4x4 invViewMatrix;
            /// <summary>Projection matrix.</summary>
            public Matrix4x4 projMatrix;
            /// <summary>Inverse Projection matrix.</summary>
            public Matrix4x4 invProjMatrix;
            /// <summary>View Projection matrix.</summary>
            public Matrix4x4 viewProjMatrix;
            /// <summary>Inverse View Projection matrix.</summary>
            public Matrix4x4 invViewProjMatrix;
            /// <summary>Previous view matrix from previous frame.</summary>
            public Matrix4x4 prevViewMatrix;
            /// <summary>Non-jittered View Projection matrix from previous frame.</summary>
            public Matrix4x4 prevViewProjMatrix;
            /// <summary>Non-jittered Inverse View Projection matrix from previous frame.</summary>
            public Matrix4x4 prevInvViewProjMatrix;
            public Matrix4x4 prevProjMatrix;
            public Matrix4x4 prevInvProjMatrix;

            /// <summary>Utility matrix (used by sky) to map screen position to WS view direction.</summary>
            public Matrix4x4 pixelCoordToViewDirWS;

            /// <summary>World Space camera position.</summary>
            public Vector3 worldSpaceCameraPos;
            internal float pad0;
            /// <summary>Offset from the main view position for stereo view constants.</summary>
            public Vector3 worldSpaceCameraPosViewOffset;
            internal float pad1;
        };

        /// <summary>Camera name.</summary>
        public string name { get; private set; } // Needs to be cached because camera.name generates GCAllocs
        /// <summary>
        /// Screen resolution information.
        /// Width, height, inverse width, inverse height.
        /// </summary>
        public Vector4 screenSize;
        /// <summary>
        /// Screen resolution information for post processes passes.
        /// Width, height, inverse width, inverse height.
        /// </summary>
        //public Vector4 postProcessScreenSize { get { return m_PostProcessScreenSize; } }

        /// <summary>Camera component.</summary>
        public Camera camera;
        /// <summary>View constants.</summary>
        public ViewConstants mainViewConstants;
        public float time;

        internal Vector4 zBufferParams;
        internal Vector4 unity_OrthoParams;
        internal Vector4 projectionParams;
        internal Vector4 screenParams;
        internal float lastTime;
        public int actualWidth { get; private set; }
        /// <summary>Height actually used for rendering after dynamic resolution and XR is applied.</summary>
        public int actualHeight { get; private set; }

        internal int frameIndex = 0;

        internal bool isFirstFrame { get; private set; }

        internal CameraData(Camera cam)
        {
            camera = cam;

            name = cam.name;

            Reset();
        }

        void Reset()
        {
            isFirstFrame = true;
        }

        public static CameraData GetOrCreate(Camera camera)
        {
            CameraData hdCamera;

            if (!s_Cameras.TryGetValue(camera, out hdCamera))
            {
                hdCamera = new CameraData(camera);
                s_Cameras.Add(camera, hdCamera);
            }

            return hdCamera;
        }

        static Dictionary<Camera, CameraData> s_Cameras = new Dictionary<Camera, CameraData>();

        unsafe internal void UpdateShaderVariablesGlobalCB(ref ShaderVariablesGlobal cb)
        {
            cb._ViewMatrix = mainViewConstants.viewMatrix;
            cb._CameraViewMatrix = mainViewConstants.viewMatrix;
            cb._InvViewMatrix = mainViewConstants.invViewMatrix;
            cb._ProjMatrix = mainViewConstants.projMatrix;
            cb._InvProjMatrix = mainViewConstants.invProjMatrix;
            cb._ViewProjMatrix = mainViewConstants.viewProjMatrix;
            cb._CameraViewProjMatrix = mainViewConstants.viewProjMatrix;
            cb._InvViewProjMatrix = mainViewConstants.invViewProjMatrix;
            //cb._PrevViewProjMatrix = mainViewConstants.prevViewProjMatrix;
            //cb._PrevInvViewProjMatrix = mainViewConstants.prevInvViewProjMatrix;
            cb._PixelCoordToViewDirWS = mainViewConstants.pixelCoordToViewDirWS;
            cb._WorldSpaceCameraPos_Internal = mainViewConstants.worldSpaceCameraPos;
            cb._ScreenSize = screenSize;
            cb._ZBufferParams = zBufferParams;
            cb._ProjectionParams = projectionParams;
            cb.unity_OrthoParams = unity_OrthoParams;
            cb._ScreenParams = screenParams;
            cb._FrameIndex = frameIndex;

            float ct = time;
            float pt = lastTime;
#if UNITY_EDITOR
            // Apply editor mode time override if any.

            float dt = time - lastTime;
            float sdt = dt;
#else
            float dt = Time.deltaTime;
            float sdt = Time.smoothDeltaTime;
#endif

            cb._Time = new Vector4(ct * 0.05f, ct, ct * 2.0f, ct * 3.0f);
            cb._SinTime = new Vector4(Mathf.Sin(ct * 0.125f), Mathf.Sin(ct * 0.25f), Mathf.Sin(ct * 0.5f), Mathf.Sin(ct));
            cb._CosTime = new Vector4(Mathf.Cos(ct * 0.125f), Mathf.Cos(ct * 0.25f), Mathf.Cos(ct * 0.5f), Mathf.Cos(ct));
            cb.unity_DeltaTime = new Vector4(dt, 1.0f / dt, sdt, 1.0f / sdt);
        }

        void UpdateViewConstants(ref ViewConstants viewConstants, Matrix4x4 projMatrix, Matrix4x4 viewMatrix, Vector3 cameraPosition)
        {
            // If TAA is enabled projMatrix will hold a jittered projection matrix. The original,
            // non-jittered projection matrix can be accessed via nonJitteredProjMatrix.
            var nonJitteredCameraProj = projMatrix;
            var cameraProj = nonJitteredCameraProj;

            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)
            var gpuProj = GL.GetGPUProjectionMatrix(cameraProj, true); // Had to change this from 'false'
            var gpuView = viewMatrix;
            var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(nonJitteredCameraProj, true);

            if (s_CameraRelativeRendering != 0)
            {
                // Zero out the translation component.
                gpuView.SetColumn(3, new Vector4(0, 0, 0, 1));
            }

            //var gpuVP = gpuNonJitteredProj * gpuView;
            //Matrix4x4 noTransViewMatrix = gpuView;
            //if (s_CameraRelativeRendering == 0)
            //{
            //    // In case we are not camera relative, gpuView contains the camera translation component at this stage, so we need to remove it.
            //    noTransViewMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));
            //}
            //var gpuVPNoTrans = gpuNonJitteredProj * noTransViewMatrix;
            if (!isFirstFrame)
            {
                viewConstants.prevInvViewProjMatrix = viewConstants.invViewProjMatrix;
                viewConstants.prevViewMatrix = viewConstants.viewMatrix;
                viewConstants.prevViewProjMatrix = viewConstants.viewProjMatrix;
                viewConstants.prevProjMatrix = viewConstants.projMatrix;
                viewConstants.prevInvProjMatrix = viewConstants.invProjMatrix;
            }
            else
            {
                viewConstants.prevInvViewProjMatrix = (gpuProj * gpuView).inverse;
                viewConstants.prevViewMatrix = gpuView;
                viewConstants.prevViewProjMatrix = gpuProj * gpuView;
                viewConstants.prevProjMatrix = gpuProj;
                viewConstants.prevInvProjMatrix = gpuProj.inverse;
            }

            viewConstants.viewMatrix = gpuView;
            viewConstants.invViewMatrix = gpuView.inverse;
            viewConstants.projMatrix = gpuProj;
            viewConstants.invProjMatrix = gpuProj.inverse;
            viewConstants.viewProjMatrix = gpuProj * gpuView;
            viewConstants.invViewProjMatrix = viewConstants.viewProjMatrix.inverse;
            //viewConstants.nonJitteredViewProjMatrix = gpuNonJitteredProj * gpuView;
            viewConstants.worldSpaceCameraPos = cameraPosition;
            viewConstants.worldSpaceCameraPosViewOffset = Vector3.zero;
            //viewConstants.viewProjectionNoCameraTrans = gpuVPNoTrans;
            viewConstants.pixelCoordToViewDirWS = ComputePixelCoordToWorldSpaceViewDirectionMatrix(viewConstants, screenSize);
        }

        Rect GetPixelRect()
        {
            return new Rect(camera.pixelRect.x, camera.pixelRect.y, camera.pixelWidth, camera.pixelHeight);
        }

        internal void Update()
        {
            // Inherit animation settings from the parent camera.
            //Camera aniCam = (parentCamera != null) ? parentCamera : camera;

            // Different views/tabs may have different values of the "Animated Materials" setting.
            bool animateMaterials = CoreUtils.AreAnimatedMaterialsEnabled(camera);
            if (animateMaterials)
            {
                float newTime, deltaTime;
#if UNITY_EDITOR
                newTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
                deltaTime = Application.isPlaying ? Time.deltaTime : 0.033f;
#else
                newTime = Time.time;
                deltaTime = Time.deltaTime;
#endif
                time = newTime;
                lastTime = newTime - deltaTime;
            }
            else
            {
                time = 0;
                lastTime = 0;
            }

            Rect finalViewport = GetPixelRect();
            finalViewport.width *= GlobalRenderSettings.ResolutionRate;
            finalViewport.height *= GlobalRenderSettings.ResolutionRate;

            actualWidth = Math.Max((int)finalViewport.size.x, 1);
            actualHeight = Math.Max((int)finalViewport.size.y, 1);

            //DynamicResolutionHandler.instance.finalViewport = new Vector2Int((int)finalViewport.width, (int)finalViewport.height);

            Vector2Int nonScaledViewport = new Vector2Int(actualWidth, actualHeight);

            var screenWidth = actualWidth;
            var screenHeight = actualHeight;

            screenSize = new Vector4(screenWidth, screenHeight, 1.0f / screenWidth, 1.0f / screenHeight);
            screenParams = new Vector4(screenSize.x, screenSize.y, 1 + screenSize.z, 1 + screenSize.w);

            if (++frameIndex > 1024)
                frameIndex = 0;

            UpdateViewConstants();

            isFirstFrame = false;
        }

        public void UpdateViewConstants()
        {
            var proj = camera.projectionMatrix;
            var view = camera.worldToCameraMatrix;
            var cameraPosition = camera.transform.position;

            UpdateViewConstants(ref mainViewConstants, proj, view, cameraPosition);
            UpdateFrustum(mainViewConstants);
        }

        public void UpdateCustomViewConstans(Matrix4x4 viewMatrix, Matrix4x4 projMatrix, Vector3 cameraPosition)
        {
            UpdateViewConstants(ref mainViewConstants, projMatrix, viewMatrix, cameraPosition);
            UpdateFrustum(mainViewConstants);
        }

        void UpdateFrustum(in ViewConstants viewConstants)
        {
            // Update frustum and projection parameters
            var projMatrix = mainViewConstants.projMatrix;
            var invProjMatrix = mainViewConstants.invProjMatrix;
            var viewProjMatrix = mainViewConstants.viewProjMatrix;

            float n = camera.nearClipPlane;
            float f = camera.farClipPlane;

            // Analyze the projection matrix.
            // p[2][3] = (reverseZ ? 1 : -1) * (depth_0_1 ? 1 : 2) * (f * n) / (f - n)
            float scale = projMatrix[2, 3] / (f * n) * (f - n);
            bool depth_0_1 = Mathf.Abs(scale) < 1.5f;
            bool reverseZ = scale > 0;
            bool flipProj = invProjMatrix.MultiplyPoint(new Vector3(0, 1, 0)).y < 0;

            // http://www.humus.name/temp/Linearize%20depth.txt
            if (reverseZ)
            {
                zBufferParams = new Vector4(-1 + f / n, 1, -1 / f + 1 / n, 1 / f);
            }
            else
            {
                zBufferParams = new Vector4(1 - f / n, f / n, 1 / f - 1 / n, 1 / n);
            }

            projectionParams = new Vector4(flipProj ? -1 : 1, n, f, 1.0f / f);

            float orthoHeight = camera.orthographic ? 2 * camera.orthographicSize : 0;
            float orthoWidth = orthoHeight * camera.aspect;
            unity_OrthoParams = new Vector4(orthoWidth, orthoHeight, 0, camera.orthographic ? 1 : 0);
        }

        internal bool isMainGameView { get { return camera.cameraType == CameraType.Game && camera.targetTexture == null; } }

        Matrix4x4 ComputePixelCoordToWorldSpaceViewDirectionMatrix(ViewConstants viewConstants, Vector4 resolution)
        {
            var viewSpaceRasterTransform = new Matrix4x4(
                new Vector4(2.0f * resolution.z, 0.0f, 0.0f, -1.0f),
                new Vector4(0.0f, -2.0f * resolution.w, 0.0f, 1.0f),
                new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            var transformT = viewConstants.invViewProjMatrix.transpose * Matrix4x4.Scale(new Vector3(-1.0f, -1.0f, -1.0f));
            return viewSpaceRasterTransform * transformT;
        }

        void Dispose()
        {

        }

        internal static void ClearAll()
        {
            foreach (var cam in s_Cameras)
            {
                cam.Value.Dispose();
            }

            s_Cameras.Clear();
        }

        public RenderTextureDescriptor GetCameraTargetDescriptor(float renderScale, bool isHdrEnabled, int msaaSamples)
        {
            int scaledWidth = (int)((float)camera.pixelWidth * renderScale);
            int scaledHeight = (int)((float)camera.pixelHeight * renderScale);

            RenderTextureDescriptor desc;

            if (camera.targetTexture == null)
            {
                desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
                desc.width = scaledWidth;
                desc.height = scaledHeight;
                desc.graphicsFormat = isHdrEnabled ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_SRGB;
                desc.depthBufferBits = 32;
                desc.msaaSamples = msaaSamples;
                desc.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            }
            else
            {
                desc = camera.targetTexture.descriptor;
                desc.msaaSamples = msaaSamples;
                desc.width = scaledWidth;
                desc.height = scaledHeight;

                if (camera.cameraType == CameraType.SceneView && !isHdrEnabled)
                {
                    desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
                }
                // SystemInfo.SupportsRenderTextureFormat(camera.targetTexture.descriptor.colorFormat)
                // will assert on R8_SINT since it isn't a valid value of RenderTextureFormat.
                // If this is fixed then we can implement debug statement to the user explaining why some
                // RenderTextureFormats available resolves in a black render texture when no warning or error
                // is given.
            }

            // Make sure dimension is non zero
            desc.width = Mathf.Max(1, desc.width);
            desc.height = Mathf.Max(1, desc.height);

            desc.enableRandomWrite = false;
            desc.bindMS = false;
            desc.useDynamicScale = camera.allowDynamicResolution;

            // check that the requested MSAA samples count is supported by the current platform. If it's not supported,
            // replace the requested desc.msaaSamples value with the actual value the engine falls back to
            desc.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);

            // if the target platform doesn't support storing multisampled RTs and we are doing any offscreen passes, using a Load load action on the subsequent passes
            // will result in loading Resolved data, which on some platforms is discarded, resulting in losing the results of the previous passes.
            // As a workaround we disable MSAA to make sure that the results of previous passes are stored. (fix for Case 1247423).
            if (!SystemInfo.supportsStoreAndResolveAction)
                desc.msaaSamples = 1;

            return desc;
        }
    }
}

