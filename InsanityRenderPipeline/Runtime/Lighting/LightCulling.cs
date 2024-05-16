using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Insanity
{
    public struct LightTile
    {
        public Vector2Int position;

    }

    unsafe public class LightCulling : IDisposable
    {
        public enum CullingType
        {
            TileBased,
            ClusterBased,
        }

        public struct GPULightData
        {
            public Vector4 position;  //xyz position in view space w - range
            public Vector4 direction; //xyz direction in view space w spotlight angle 0 is point light
            public Vector4 color;     //w - intensity
        }

        public class LightCullingShaderParams
        {
            public static int _TileNumber;
            public static int _TileFrustums;
            public static int _ViewToScreenTranspose;
            public static int _LightVisibilityIndexBuffer;
            public static int _LightBuffer;
            public static int _DepthTexture;
            public static int _ProjInverse;
            public static int _ScreenSize;
            public static int _TotalLightNum;
            public static int _TileVisibleLightCounts;
            public static int _ViewMatrix;
            public static int _TileAABBsBuffer;
        }


        const int MAX_LIGHTS_NUM = 1024;
        const int MAX_VISIBLE_LIGHTS_PER_TILE = 256;

        ComputeBuffer m_AdditionalLightsBuffer = null;
        ComputeBuffer m_LightsVisibilityIndexBuffer = null;
        
        ComputeBuffer m_TileFrustumBuffer = null;
        //for debugging
        bool m_DebugMode = false;
        RenderTexture m_TileVisibleLightCounts;
        ComputeBuffer m_TileAABBsBuffer = null;
        //----------
        GPULightData[] m_AdditionalLights = new GPULightData[MAX_LIGHTS_NUM];
        int m_ValidLightsCount = 0;

        Vector2Int m_CurrentTileNumbers;
        int m_kernelTileFrustumCompute = -1;
        int m_kernelLightCulling = -1;
        int m_tileSize = 0;

        static LightCulling s_instance = null;
        public static LightCulling Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new LightCulling();
                }
                return s_instance;
            }
        }

        static void InitializeShaderParams()
        {
            LightCullingShaderParams._TileNumber = Shader.PropertyToID("_TileNumber");
            LightCullingShaderParams._ViewToScreenTranspose = Shader.PropertyToID("_ViewToScreenTranspose");
            LightCullingShaderParams._TileFrustums = Shader.PropertyToID("_TileFrustums");
            LightCullingShaderParams._LightVisibilityIndexBuffer = Shader.PropertyToID("_LightVisibilityIndexBuffer");
            LightCullingShaderParams._LightBuffer = Shader.PropertyToID("_LightBuffer");
            LightCullingShaderParams._DepthTexture = Shader.PropertyToID("_DepthTexture");
            LightCullingShaderParams._ProjInverse = Shader.PropertyToID("_ProjInverse");
            LightCullingShaderParams._ScreenSize = Shader.PropertyToID("_ScreenSize");
            LightCullingShaderParams._TotalLightNum = Shader.PropertyToID("_TotalLightNum");
            LightCullingShaderParams._TileVisibleLightCounts = Shader.PropertyToID("_TileVisibleLightCounts");
            LightCullingShaderParams._ViewMatrix = Shader.PropertyToID("_ViewMatrix");
            LightCullingShaderParams._TileAABBsBuffer = Shader.PropertyToID("_TileAABBs");
        }

        private LightCulling()
        {
            InitializeShaderParams();

        }

        public void SetupAdditionalLights(NativeArray<VisibleLight> visibleLights, CameraData cameraData)
        {
            if (m_AdditionalLightsBuffer == null)
            {
                m_AdditionalLightsBuffer = new ComputeBuffer(MAX_LIGHTS_NUM, Marshal.SizeOf(typeof(GPULightData)), ComputeBufferType.Default);
            }

            m_ValidLightsCount = 0;
            for (int i = 0; i < visibleLights.Length; ++i)
            {
                if (visibleLights[i].light.type == LightType.Directional)
                {
                    continue;
                }
                if (visibleLights[i].lightType == LightType.Spot && visibleLights[i].light.spotAngle == 0)
                {
                    continue;
                }
                if (visibleLights[i].light.intensity > 0)
                {
                    GPULightData additionalLight = new GPULightData();
                    additionalLight.position = visibleLights[i].light.transform.position;// - cameraData.camera.transform.position;
                    additionalLight.position.w = visibleLights[i].light.range;//1.0f / (visibleLights[i].light.range * visibleLights[i].light.range);
                    additionalLight.direction = visibleLights[i].light.transform.forward;//cameraData.camera.transform.TransformDirection(visibleLights[i].light.transform.forward);
                    additionalLight.direction.w = visibleLights[i].light.type == LightType.Spot ? Mathf.Deg2Rad * visibleLights[i].light.spotAngle : 0;
                    additionalLight.color = visibleLights[i].light.color.linear;
                    additionalLight.color.w = visibleLights[i].light.intensity;
                    m_AdditionalLights[m_ValidLightsCount++] = additionalLight;
                }
            }

            m_AdditionalLightsBuffer.SetData(m_AdditionalLights);
        }

        public RenderTexture TileVisibleLightCounts
        {
            get { return m_TileVisibleLightCounts; }
        }

        public Vector2Int CurrentTileNumber
        {
            get { return m_CurrentTileNumbers; }
        }

        private static TextureHandle CreateLightVisibleCountTexture(RenderGraph graph, int width, int height)
        {
            //Texture description
            TextureDesc textureRTDesc = new TextureDesc(width, height);
            textureRTDesc.colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RInt, false);
            textureRTDesc.depthBufferBits = 0;
            textureRTDesc.msaaSamples = MSAASamples.None;
            textureRTDesc.enableRandomWrite = true;
            textureRTDesc.clearBuffer = true;
            textureRTDesc.clearColor = Color.black;
            textureRTDesc.name = "TileLightVisibleCount";
            textureRTDesc.filterMode = FilterMode.Point;

            return graph.CreateTexture(textureRTDesc);
        }

        public void SetupTiles(int screenWidth, int screenHeight, int tileSize)
        {
            m_tileSize = tileSize;
            Vector2Int tileNumber = new Vector2Int(Mathf.CeilToInt((float)screenWidth / tileSize), Mathf.CeilToInt((float)screenHeight / tileSize));

            if (tileNumber != m_CurrentTileNumbers)
            {
                m_CurrentTileNumbers = tileNumber;

                if (m_TileFrustumBuffer != null)
                {
                    CoreUtils.SafeRelease(m_TileFrustumBuffer);
                    m_TileFrustumBuffer = null;
                }

                if (m_TileVisibleLightCounts != null)
                {
                    m_TileVisibleLightCounts.Release();
                    m_TileVisibleLightCounts = null;
                }
                if (m_DebugMode)
                {
                    if (m_TileAABBsBuffer != null)
                    {
                        m_TileAABBsBuffer.Release();
                        m_TileAABBsBuffer = null;
                    }
                }
                

                if (m_LightsVisibilityIndexBuffer == null)
                {
                    CoreUtils.SafeRelease(m_LightsVisibilityIndexBuffer);
                    m_LightsVisibilityIndexBuffer = null;
                }

                m_TileFrustumBuffer = new ComputeBuffer(m_CurrentTileNumbers.x * m_CurrentTileNumbers.y, Marshal.SizeOf<Vector4>() * 4, ComputeBufferType.Default);
                
                m_LightsVisibilityIndexBuffer = new ComputeBuffer(m_CurrentTileNumbers.x * m_CurrentTileNumbers.y * MAX_VISIBLE_LIGHTS_PER_TILE, 
                    sizeof(int), ComputeBufferType.Default);

                m_TileVisibleLightCounts = new RenderTexture(m_CurrentTileNumbers.x, m_CurrentTileNumbers.y, 0, RenderTextureFormat.RInt);
                m_TileVisibleLightCounts.enableRandomWrite = true;
                m_TileVisibleLightCounts.Create();
                if (m_DebugMode)
                {
                    m_TileAABBsBuffer = new ComputeBuffer(m_CurrentTileNumbers.x * m_CurrentTileNumbers.y, Marshal.SizeOf<Vector3>() * 2, ComputeBufferType.Default);
                }
                
            }  

            if (ValidAdditionalLightsCount == 0)
            {
                NativeArray<int> ints = new NativeArray<int>(m_CurrentTileNumbers.x * m_CurrentTileNumbers.y * MAX_VISIBLE_LIGHTS_PER_TILE, Allocator.Temp);
                UnsafeUtility.MemSet(ints.GetUnsafePtr(), 255, ints.Length * sizeof(int));
                m_LightsVisibilityIndexBuffer.SetData(ints);
            }
        }

        public ComputeBuffer AdditionalLightsBuffer
        { get { return m_AdditionalLightsBuffer; } }

        public int ValidAdditionalLightsCount
        { get { return m_ValidLightsCount; } }
        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LightCulling()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public ComputeBuffer LightsVisibilityIndexBuffer
        {
            get { return m_LightsVisibilityIndexBuffer; }
        }

        class TileFrustumComputePassData
        {
            public ComputeBuffer tileFrustumBuffer;
            public Vector2 tileNumbers;
            public int kernelId;
            public ComputeShader computeShader;
            public Matrix4x4 viewToScreenTranspose;
            public float tileSize;
        }

        public void ExecuteTileFrustumCompute(RenderingData renderingData, ComputeShader tileFrustumCompute)
        {
            if (m_kernelTileFrustumCompute == -1)
            {
                m_kernelTileFrustumCompute = tileFrustumCompute.FindKernel("TileFrustumCompute");
            }
            using (var builder = renderingData.renderGraph.AddRenderPass<TileFrustumComputePassData>("Compute Tile Frustum", out var passData,
                new ProfilingSampler("Compute Tile Frustum Profiler")))
            {
                passData.tileFrustumBuffer = m_TileFrustumBuffer;
                passData.computeShader = tileFrustumCompute;
                passData.tileNumbers = m_CurrentTileNumbers;
                passData.kernelId = m_kernelTileFrustumCompute;
                passData.tileSize = m_tileSize;
                Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix; //GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
                proj = proj * Matrix4x4.Scale(new Vector3(1, 1, -1));
                //we can not use renderingData.cameraData.mainViewConstants because it is the camera relative space.
                //The light data is in the world space. So we have to use the original view matrix
                //Matrix4x4 viewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;   
                Vector3 point1 = new Vector3(0, -0.5f, 1.0f);
                Vector3 point2 = new Vector3(0, 0, 0.98701f);
                point1 = proj.inverse.MultiplyPoint(point1);
                point2 = proj.inverse.MultiplyPoint(point2);

                passData.viewToScreenTranspose = proj.transpose;//renderingData.cameraData.mainViewConstants.invProjMatrix.transpose;
                Vector4 plane = new Vector4(1.0f, 0, 0, 1.0f);
                Plane plane1 = new Plane(Vector3.right, 1.0f);
                plane1 = proj.inverse.TransformPlane(plane1);
                plane = passData.viewToScreenTranspose * plane;
                float normalLength = new Vector3(plane.x, plane.y, plane.z).magnitude;
                plane.x /= normalLength;
                plane.y /= normalLength;
                plane.z /= normalLength;
                plane.w /= normalLength;

                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera);
                Plane left = renderingData.cameraData.camera.transform.worldToLocalMatrix.TransformPlane(frustumPlanes[0]);


                builder.AllowPassCulling(false);
                builder.SetRenderFunc((TileFrustumComputePassData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeBufferParam(data.computeShader, data.kernelId, LightCullingShaderParams._TileFrustums, data.tileFrustumBuffer);
                    context.cmd.SetComputeMatrixParam(data.computeShader, LightCullingShaderParams._ViewToScreenTranspose, data.viewToScreenTranspose);
                    context.cmd.SetComputeVectorParam(data.computeShader, LightCullingShaderParams._TileNumber, data.tileNumbers);
                    int threadGroupX = Mathf.CeilToInt(data.tileNumbers.x / data.tileSize);
                    int threadGroupY = Mathf.CeilToInt(data.tileNumbers.y / data.tileSize);
                    context.cmd.DispatchCompute(data.computeShader, data.kernelId, threadGroupX, threadGroupY, 1);
                });
            }
        }

        public class TileBasedLightCullingData
        {
            public ComputeShader computeShader;
            public ComputeBuffer additionalLightsBuffer;
            public ComputeBuffer tileFrustumBuffer;
            public ComputeBuffer lightVisibilityIndexBuffer;
            public TextureHandle tileVisibleLightCounts;
            public TextureHandle depthTexture;
            public Vector2 tileNumbers;
            public int kernelId;
            public float tileSize;
            public Matrix4x4 projInverse;
            public Vector2 screenSize;
            public int totalLightsNum;
            public Matrix4x4 viewMatrix;
            public ComputeBuffer tileAABBsBuffer;
        }


        public TileBasedLightCullingData ExecuteTilebasedLightCulling(RenderingData renderingData, TextureHandle depthTexture, ComputeShader tileBasedLightCulling)
        {
            if (m_kernelLightCulling == -1)
            {
                m_kernelLightCulling = tileBasedLightCulling.FindKernel("LightCulling");
            }

            //TestLightCulling(new Vector2Int(0, 1), Vector2Int.zero, renderingData.cameraData.camera);

            using (var builder = renderingData.renderGraph.AddRenderPass<TileBasedLightCullingData>("Tile based light culling", out var passData,
                new ProfilingSampler("Tile based light culling Profiler")))
            {
                passData.computeShader = tileBasedLightCulling;
                passData.additionalLightsBuffer = m_AdditionalLightsBuffer;
                passData.tileFrustumBuffer = m_TileFrustumBuffer;
                passData.lightVisibilityIndexBuffer = m_LightsVisibilityIndexBuffer;
                passData.kernelId = m_kernelLightCulling;
                TextureHandle tileLightVisibleCounts = CreateLightVisibleCountTexture(renderingData.renderGraph, m_CurrentTileNumbers.x, m_CurrentTileNumbers.y);

                passData.tileVisibleLightCounts = builder.WriteTexture(tileLightVisibleCounts); //m_TileVisibleLightCounts;
                passData.depthTexture = builder.ReadTexture(depthTexture);
                passData.tileNumbers = m_CurrentTileNumbers;
                passData.tileSize = m_tileSize;
                //camera.projectMatrix transform the view space position to the NDC position and flip the z direction
                //NDC position means all the values are between [-1, 1]. Notice that the z is also transform to [-1, 1]
                Matrix4x4 proj = renderingData.cameraData.camera.projectionMatrix * Matrix4x4.Scale(new Vector3(1, 1, -1)); //GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix, false);
                passData.projInverse = proj.inverse;//renderingData.cameraData.camera.projectionMatrix.inverse;
                passData.screenSize = new Vector2(Screen.width, Screen.height);
                passData.totalLightsNum = ValidAdditionalLightsCount;
                passData.viewMatrix = renderingData.cameraData.camera.transform.worldToLocalMatrix;
                Vector3 cameraPos = renderingData.cameraData.camera.transform.position;
                Vector3 cameraPosInView = passData.viewMatrix.MultiplyPoint(cameraPos);

                passData.tileAABBsBuffer = m_TileAABBsBuffer;

                builder.AllowPassCulling(false);
                builder.SetRenderFunc((TileBasedLightCullingData data, RenderGraphContext context) =>
                {
                    context.cmd.SetComputeBufferParam(data.computeShader, data.kernelId, LightCullingShaderParams._TileFrustums, data.tileFrustumBuffer);
                    context.cmd.SetComputeBufferParam(data.computeShader, data.kernelId, LightCullingShaderParams._LightBuffer, data.additionalLightsBuffer);
                    context.cmd.SetComputeBufferParam(data.computeShader, data.kernelId, LightCullingShaderParams._LightVisibilityIndexBuffer, data.lightVisibilityIndexBuffer);
                    context.cmd.SetComputeTextureParam(data.computeShader, data.kernelId, LightCullingShaderParams._DepthTexture, data.depthTexture);
                    context.cmd.SetComputeTextureParam(data.computeShader, data.kernelId, LightCullingShaderParams._TileVisibleLightCounts, data.tileVisibleLightCounts);
                    context.cmd.SetComputeVectorParam(data.computeShader, LightCullingShaderParams._ScreenSize, data.screenSize);
                    context.cmd.SetComputeVectorParam(data.computeShader, LightCullingShaderParams._TileNumber, data.tileNumbers);
                    context.cmd.SetComputeMatrixParam(data.computeShader, LightCullingShaderParams._ProjInverse, data.projInverse);
                    context.cmd.SetComputeIntParam(data.computeShader, LightCullingShaderParams._TotalLightNum, data.totalLightsNum);
                    context.cmd.SetComputeMatrixParam(data.computeShader, LightCullingShaderParams._ViewMatrix, data.viewMatrix);
                    if (m_DebugMode)
                    {
                        context.cmd.SetComputeBufferParam(data.computeShader, data.kernelId, LightCullingShaderParams._TileAABBsBuffer, data.tileAABBsBuffer);
                    }
                    
                    int threadGroupX = (int)data.tileNumbers.x;
                    int threadGroupY = (int)data.tileNumbers.y;
                    context.cmd.DispatchCompute(data.computeShader, data.kernelId, threadGroupX, threadGroupY, 1);
                });
                return passData;
            }
            
        }

        public void Dispose()
        {
            CoreUtils.SafeRelease(m_LightsVisibilityIndexBuffer);
            CoreUtils.SafeRelease(m_AdditionalLightsBuffer);
            CoreUtils.SafeRelease(m_TileFrustumBuffer);
            m_LightsVisibilityIndexBuffer = null;
            m_AdditionalLightsBuffer = null;
            m_TileFrustumBuffer = null;
            if (m_TileVisibleLightCounts != null)
            {
                m_TileVisibleLightCounts.Release();
                m_TileVisibleLightCounts = null;
            }
            CoreUtils.SafeRelease(m_TileAABBsBuffer);
            m_kernelTileFrustumCompute = -1;
            m_kernelLightCulling = -1;
            m_CurrentTileNumbers = Vector2Int.zero;
        }
    }
}


