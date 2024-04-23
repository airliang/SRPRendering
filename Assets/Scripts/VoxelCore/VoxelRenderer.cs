using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
//using Insanity;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Profiling;
using static Unity.Burst.Intrinsics.X86.Avx;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;

//[ExecuteInEditMode]
public class VoxelRenderer : MonoBehaviour
{
    /*
    public static int MAX_INSTANCE_NUM = 128 * 128;//VoxelChunk.VOXELS_NUMBER.x * VoxelChunk.VOXELS_NUMBER.y * 6;  //6 face x 32 voxel x 32 voxel 
    public ComputeBuffer m_visibilityBuffer;
    public ComputeBuffer m_shadowVisibilityBuffer;
    public ComputeBuffer m_transformBuffer;
    public ComputeBuffer m_visibleInstanceSumBuffer;
    private ComputeBuffer m_shadowVisibleInstanceSumBuffer;
    private ComputeBuffer m_inputColorsBuffer;
    public ComputeBuffer m_groupSumBuffer;
    public ComputeBuffer m_groupSumScanBuffer;
    public ComputeBuffer m_argBuffer;
    public ComputeBuffer m_argShadowBuffer;
    public ComputeBuffer m_counterBuffer;
    public ComputeBuffer m_counterShadowBuffer;
    public ComputeBuffer m_scanInstancePredicateBuffer;
    public ComputeBuffer m_scanGroupSumArrayBuffer;

    public Shader m_voxelInstanceShader;
    public ComputeShader m_CopyTransform;
    public ComputeShader m_CullingInstances;
    public ComputeShader m_ScanInstances;
    //public ComputeShader m_CopyVisibleInstances;

    public bool m_enablePerInstanceCulling = false;
    public bool m_casterShadow = false;
    int m_kernelCopyTransform = -1;
    int m_kernelCullingInstance = -1;
    int m_kernelCullingShadowInstance = -1;
    int m_kernelParallelPreSum = -1;
    int m_kernelGroupSumArray = -1;
    int m_kernelCopyVisibleInstances = -1;
    RenderTexture m_finalTransforms;
    RenderTexture m_voxelColors;
    VoxelWorld m_voxelWorld = new VoxelWorld();
    bool m_cullingFinished = false;
    uint[] m_args = new uint[5];
    Mesh m_mesh;
    Material m_material;
    ParallelCullingJobData parallelCullingJobData = new ParallelCullingJobData();
    private List<VoxelChunk> m_visibleChunks = new List<VoxelChunk>();
    const string m_DrawIndirectProfilerTag = "Draw Indirect Instances Profiler";
    ProfilingSampler m_DrawIndirectProfilingSampler = new ProfilingSampler(m_DrawIndirectProfilerTag);
    const string m_DrawShadowIndirectProfilerTag = "Draw Indirect Instances ShadowCaster Profiler";
    ProfilingSampler m_DrawShadowIndirectProfilingSampler = new ProfilingSampler(m_DrawShadowIndirectProfilerTag);
    private Vector3 m_cameraPos;
    private Camera m_camera;

    List<VoxelRenderBatch> m_FullyInFrustumBatch = new List<VoxelRenderBatch>();
    List<VoxelRenderBatch> m_PartInFrustumBatch = new List<VoxelRenderBatch>();

    private MaterialPropertyBlock m_materialProperty = null;

    public void SafeRelease<T>(T obj) where T : Object
    {
        if (obj != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(obj);
#else
            Destroy(obj);
#endif
            obj = null;
        }
    }

    public void SafeReleaseBuffer(ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }

    public void Initialize()
    {
        if (m_CopyTransform == null)
            return;

        if (m_voxelInstanceShader == null)
            return;

        m_voxelWorld.GenerateRandomChunks();

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        if (m_mesh == null)
        {
            m_mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            cubeMesh.GetVertices(vertices);
            m_mesh.SetVertices(vertices);
            List<Vector3> normals = new List<Vector3>();
            cubeMesh.GetNormals(normals);
            m_mesh.SetNormals(normals);

            List<int> indices = new List<int>();
            cubeMesh.GetIndices(indices, 0);
            m_mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
            m_mesh.RecalculateBounds();

            m_args[0] = (uint)m_mesh.GetIndexCount(0);
            m_args[1] = 0;
            m_args[2] = (uint)m_mesh.GetIndexStart(0);
            m_args[3] = (uint)m_mesh.GetBaseVertex(0);

            m_mesh.UploadMeshData(true);

        }
#if UNITY_EDITOR
        DestroyImmediate(cube);
#else
        Destroy(cube); 
#endif
        cube = null;

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        //RenderingEventManager.AddEvent(RenderingEvents.ShadowCasterPassEvent, RenderShadowCasterPass);

        RenderingEventManager.AddShadowCasterEvent(RenderShadowCasterPass);
        RenderingEventManager.AddEvent(RenderingEvents.DepthPassEvent, RenderCullingChunksDepthPass);
        RenderingEventManager.AddEvent(RenderingEvents.OpaqueForwardPassEvent, RenderCullingChunks);
        //RenderingEventManager.BeforeExecuteRenderGraphDelegate += OnBeforeExecuteRenderGraph;

        InitializeRenderingData();
    }

    private void InitializeRenderingData()
    {
        if (m_materialProperty == null)
        {
            m_materialProperty = new MaterialPropertyBlock();
        }
        if (m_finalTransforms == null)
        {
            m_finalTransforms = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            m_finalTransforms.filterMode = FilterMode.Point;
            m_finalTransforms.enableRandomWrite = true;
            m_finalTransforms.useMipMap = false;
            m_finalTransforms.Create();
            m_finalTransforms.name = "PositionTexture";
        }

        if (m_voxelColors == null)
        {
            m_voxelColors = new RenderTexture(128, 128, 0, GraphicsFormat.R32_UInt, 1);
            m_voxelColors.filterMode = FilterMode.Point;
            m_voxelColors.enableRandomWrite = true;
            m_voxelColors.Create();
            m_voxelColors.name = "ColorTexture";
        }

        if (m_material == null)
        {
            m_material = new Material(m_voxelInstanceShader);
            //m_material.SetTexture("_Positions", m_finalTransforms);
            m_material.enableInstancing = true;
        }

        if (m_transformBuffer == null)
        {
            m_transformBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Default);
        }

        if (m_inputColorsBuffer == null)
        {
            m_inputColorsBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, sizeof(uint), ComputeBufferType.Default);
        }

        if (m_argBuffer == null)
        {
            m_argBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        if (m_argShadowBuffer == null)
        {
            m_argShadowBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        if (m_kernelCopyTransform == -1)
        {
            m_kernelCopyTransform = m_CopyTransform.FindKernel("CSMain");
        }

        if (m_enablePerInstanceCulling)
        {
            if (m_visibilityBuffer == null)
            {
                m_visibilityBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, sizeof(int), ComputeBufferType.Default);
                m_visibilityBuffer.name = "visibilityBuffer";
            }

            if (m_shadowVisibilityBuffer == null)
            {
                m_shadowVisibilityBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, sizeof(int), ComputeBufferType.Default);
                m_shadowVisibilityBuffer.name = "shadowVisibilityBuffer";
            }

            if (m_counterBuffer == null)
            {
                m_counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Counter);
                m_counterBuffer.name = "counterBuffer";
            }

            if (m_counterShadowBuffer == null)
            {
                m_counterShadowBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Counter);
                m_counterShadowBuffer.name = "counterShadowBuffer";
            }

            if (m_visibleInstanceSumBuffer == null)
            {
                m_visibleInstanceSumBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, sizeof(int), ComputeBufferType.Default);
                m_visibleInstanceSumBuffer.name = "visibleInstanceSumBuffer";
            }

            if (m_shadowVisibleInstanceSumBuffer == null)
            {
                m_shadowVisibleInstanceSumBuffer = new ComputeBuffer(MAX_INSTANCE_NUM, sizeof(int), ComputeBufferType.Default);
                m_shadowVisibleInstanceSumBuffer.name = "shadowVisibleInstanceSumBuffer";
            }

            int groupSumArraySize = Mathf.CeilToInt((float)MAX_INSTANCE_NUM / 128.0f);
            if (m_groupSumBuffer == null)
            {
                m_groupSumBuffer = new ComputeBuffer(groupSumArraySize, sizeof(int), ComputeBufferType.Default);
                m_groupSumBuffer.name = "groupSumBuffer";
            }   

            if (m_groupSumScanBuffer == null)
            {
                m_groupSumScanBuffer = new ComputeBuffer(groupSumArraySize, sizeof(int), ComputeBufferType.Default);
                m_groupSumScanBuffer.name = "groupSumScanBuffer";
            }

            if (m_kernelCullingInstance == -1)
            {
                m_kernelCullingInstance = m_CullingInstances.FindKernel("CullInstance");
            }

            if (m_kernelCullingShadowInstance == -1)
            {
                m_kernelCullingShadowInstance = m_CullingInstances.FindKernel("CullInstanceShadow");
            }

            if (m_kernelParallelPreSum == -1)
            {
                m_kernelParallelPreSum = m_ScanInstances.FindKernel("PresumVisibilityBuffer");
            }

            if (m_kernelGroupSumArray == -1)
            {
                m_kernelGroupSumArray = m_ScanInstances.FindKernel("PresumGroup");
            }

            if (m_kernelCopyVisibleInstances == -1)
            {
                m_kernelCopyVisibleInstances = m_CopyTransform.FindKernel("CopyVisibleInstances");
            }
        }

        m_CopyTransform.SetTexture(m_kernelCopyTransform, "_Positions", m_finalTransforms);
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        //RenderingEventManager.BeforeExecuteRenderGraphDelegate -= OnBeforeExecuteRenderGraph;
        RenderingEventManager.RemoveEvent(RenderingEvents.DepthPassEvent, RenderCullingChunksDepthPass);
        //RenderingEventManager.RemoveEvent(RenderingEvents.ShadowCasterPassEvent, RenderShadowCasterPass);
        RenderingEventManager.RemoveEvent(RenderingEvents.OpaqueForwardPassEvent, RenderCullingChunks);
        RenderingEventManager.RemoveShadowCasterEvent(RenderShadowCasterPass);

        SafeReleaseBuffer(m_argBuffer);
        SafeReleaseBuffer(m_argShadowBuffer);
        SafeReleaseBuffer(m_transformBuffer);
        SafeReleaseBuffer(m_inputColorsBuffer);

        SafeReleaseBuffer(m_visibilityBuffer);
        SafeReleaseBuffer(m_shadowVisibilityBuffer);
        SafeReleaseBuffer(m_visibleInstanceSumBuffer);
        SafeReleaseBuffer(m_shadowVisibleInstanceSumBuffer);
        SafeReleaseBuffer(m_counterBuffer);
        SafeReleaseBuffer(m_counterShadowBuffer);
        SafeReleaseBuffer(m_groupSumBuffer);
        SafeReleaseBuffer(m_groupSumScanBuffer);

        if (m_finalTransforms != null)
        {
            m_finalTransforms.Release();
            SafeRelease(m_finalTransforms);
        }

        if (m_voxelColors != null)
        {
            m_voxelColors.Release();
            SafeRelease(m_voxelColors);
        }

        m_kernelCopyTransform = -1;
        m_kernelCullingInstance = -1;
        m_kernelCullingShadowInstance = -1;
        m_kernelParallelPreSum = -1;
        m_kernelGroupSumArray = -1;
        m_kernelParallelPreSum = -1;
        m_kernelCopyVisibleInstances = -1;

        if (m_mesh != null)
        {
            m_mesh.Clear();
            SafeRelease(m_mesh);
        }

        SafeRelease(m_material);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        //begin chunk culling
        m_camera = camera;
        m_cullingFinished = false;
        float3x2[] chunkBounds = m_voxelWorld.ActiveChunkBounds;
        if (chunkBounds != null)
        {
            parallelCullingJobData.Execute(chunkBounds, GeometryUtility.CalculateFrustumPlanes(camera), camera.transform.position);
        }
        m_cameraPos = camera.transform.position;
    }

    private void ProcessCullingResult()
    {
        if (!m_cullingFinished)
        {
            parallelCullingJobData.WaitForComplete();
            m_visibleChunks.Clear();
            for (int i = 0; i < parallelCullingJobData._cullingResults.Length; i++)
            {
                m_voxelWorld.ActiveChunks[i].CullResult = parallelCullingJobData._cullingResults[i];
                if (parallelCullingJobData._cullingResults[i] != (byte)FrustumCullingResult.FRUSTUM_OUTSIDE)
                {
                    m_visibleChunks.Add(m_voxelWorld.ActiveChunks[i]);
                }
            }

            m_visibleChunks.Sort((a, b) => { return Vector3.Distance(a.m_worldBound.center, parallelCullingJobData._cameraPos).
                CompareTo(Vector3.Distance(b.m_worldBound.center, parallelCullingJobData._cameraPos)); });
            parallelCullingJobData.Release();
            m_cullingFinished = true;
        }
    }

    private bool IsVoxelChunkInShadowDistance(VoxelChunk chunk, Vector3 cameraPos, float distance)
    {
        int isInShadow = 0;
        //for (int i = 0; i < 8; ++i)
        {
            Vector3 boxCorner = new Vector3(chunk.m_worldBound.min.x, chunk.m_worldBound.min.y, chunk.m_worldBound.min.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.min.x, chunk.m_worldBound.min.y, chunk.m_worldBound.max.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.min.x, chunk.m_worldBound.max.y, chunk.m_worldBound.min.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.min.x, chunk.m_worldBound.max.y, chunk.m_worldBound.max.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.max.x, chunk.m_worldBound.min.y, chunk.m_worldBound.min.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.max.x, chunk.m_worldBound.min.y, chunk.m_worldBound.max.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.max.x, chunk.m_worldBound.max.y, chunk.m_worldBound.min.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;

            boxCorner = new Vector3(chunk.m_worldBound.max.x, chunk.m_worldBound.max.y, chunk.m_worldBound.max.z);
            isInShadow += Vector3.Distance(boxCorner, cameraPos) < distance ? 1 : 0;
            if (isInShadow > 0)
                return true;
        }

        return false;
    }

    private void RenderShadowCasterPass(ScriptableRenderContext context, CommandBuffer cmd, Insanity.ShadowSettings shadowSettings, 
        ref ShadowDrawingSettings shadowDrawSettings, int cascadeIndex)
    {
        if (!m_casterShadow)
            return;
        ProcessCullingResult();
        using (new ProfilingScope(cmd, m_DrawShadowIndirectProfilingSampler))
        { 
            ClearVoxelRenderBatches();
            foreach (var voxelChunk in m_visibleChunks)
            {
                if (!IsVoxelChunkInShadowDistance(voxelChunk, m_cameraPos, shadowSettings.maxShadowDistance))
                {
                    continue;
                }

                bool cullingInstance = m_enablePerInstanceCulling && voxelChunk.CullResult == (byte)FrustumCullingResult.FRUSTUM_INTERSECT;

                VoxelRenderBatch batch = GetVoxelRenderBatch(voxelChunk, cullingInstance);
                batch.AddChunk(voxelChunk.Positions, voxelChunk.Colors);
            }

            foreach (var voxelBatch in m_FullyInFrustumBatch)
            {
                RenderVoxelBatchShadow(voxelBatch, false, shadowSettings, ref shadowDrawSettings, cascadeIndex, cmd);
            }

            foreach (var voxelBatch in m_PartInFrustumBatch)
            {
                RenderVoxelBatchShadow(voxelBatch, true, shadowSettings, ref shadowDrawSettings, cascadeIndex, cmd);
            }
        }
    }

    private void RenderVoxelBatchShadow(VoxelRenderBatch voxelRenderBatch, bool cullingInstance, ShadowSettings shadowSettings, 
        ref ShadowDrawingSettings shadowDrawSettings, int cascadeIndex, CommandBuffer cmd)
    {
        cmd.SetBufferData(m_transformBuffer, voxelRenderBatch.Positions);
        m_args[1] = (uint)voxelRenderBatch.ActiveVoxelsNum;
        cmd.SetBufferData(m_argShadowBuffer, m_args);
        int groupThreadX = Mathf.CeilToInt((float)voxelRenderBatch.ActiveVoxelsNum / 128);
        if (cullingInstance)
        {
            //culling
            cmd.SetBufferCounterValue(m_counterShadowBuffer, 0);
            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingShadowInstance, "_InputPositions", m_transformBuffer);
            cmd.SetComputeVectorParam(m_CullingInstances, "_CamPosition", m_cameraPos);
            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingShadowInstance, "_CounterShadowBuffer", m_counterShadowBuffer);
            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingShadowInstance, "_ShadowVisibilityBuffer", m_shadowVisibilityBuffer);
            cmd.SetComputeVectorParam(m_CullingInstances, "_ChunkPosition", Vector3.zero);

            cmd.SetComputeVectorParam(m_CullingInstances, "_VoxelExtends", VoxelChunk.VOXEL_HALF_SIZE);
            cmd.SetComputeFloatParam(m_CullingInstances, "_InstanceCount", m_args[1]);
            cmd.SetComputeFloatParam(m_CullingInstances, "_ShadowDistance", shadowSettings.maxShadowDistance);
            cmd.SetComputeVectorParam(m_CullingInstances, "_ShadowCascadeSphere", shadowDrawSettings.splitData.cullingSphere);

            Matrix4x4 v = m_camera.worldToCameraMatrix;
            Matrix4x4 p = m_camera.projectionMatrix;
            Matrix4x4 vpMatrix = p * v;
            //m_CullingInstances.SetMatrix("_VP", vpMatrix);
            cmd.SetComputeMatrixParam(m_CullingInstances, "_VP", vpMatrix);
            cmd.DispatchCompute(m_CullingInstances, m_kernelCullingShadowInstance, groupThreadX, 1, 1);

            cmd.CopyCounterValue(m_counterShadowBuffer, m_argShadowBuffer, sizeof(uint));

            //presum visibility
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_VisibilityBufferIn", m_shadowVisibilityBuffer);
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_GroupSumArray", m_groupSumBuffer);
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_ScannedInstancePredicates", m_visibleInstanceSumBuffer);
            cmd.DispatchCompute(m_ScanInstances, m_kernelParallelPreSum, groupThreadX, 1, 1);


            if (groupThreadX > 1)
            {
                //presum group
                cmd.SetComputeBufferParam(m_ScanInstances, m_kernelGroupSumArray, "_GroupSumArrayIn", m_groupSumBuffer);
                cmd.SetComputeBufferParam(m_ScanInstances, m_kernelGroupSumArray, "_GroupSumArrayOut", m_groupSumScanBuffer);
                cmd.SetComputeIntParam(m_ScanInstances, "_GroupsNum", Mathf.NextPowerOfTwo(groupThreadX));

                cmd.SetComputeIntParam(m_ScanInstances, "_GroupSumArrayInSize", groupThreadX);
                cmd.DispatchCompute(m_ScanInstances, m_kernelGroupSumArray, 1, 1, 1);
                cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumScanBuffer);
            }
            else
            {
                //m_CopyTransform.SetBuffer(m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumBuffer);
                cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumBuffer);
            }

            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_VisibilityBuffer", m_shadowVisibilityBuffer);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_PredicateScanBuffer", m_visibleInstanceSumBuffer);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_InputPositions", m_transformBuffer);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_Positions", m_finalTransforms);
            cmd.DispatchCompute(m_CopyTransform, m_kernelCopyVisibleInstances, groupThreadX, 1, 1);

            //cmd.SetGlobalVector("_ChunkPosition", voxelChunk.m_worldPosition);
            //MaterialPropertyBlock materialPropertyBlock = voxelChunk.materialProperty;
            m_materialProperty.SetVector("_ChunkPosition", Vector3.zero);
            m_materialProperty.SetTexture("_Positions", m_finalTransforms);
            cmd.DrawMeshInstancedIndirect(m_mesh, 0, m_material, 1, m_argShadowBuffer, 0, m_materialProperty);
        }
        else
        {
            cmd.SetComputeIntParam(m_CopyTransform, "_InstanceCount", (int)m_args[1]);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyTransform, "_InputPositions", m_transformBuffer);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyTransform, "_Positions", m_finalTransforms);
            cmd.DispatchCompute(m_CopyTransform, m_kernelCopyTransform, groupThreadX, 1, 1);

            m_materialProperty.SetVector("_ChunkPosition", Vector3.zero);
            m_materialProperty.SetTexture("_Positions", m_finalTransforms);
            cmd.DrawMeshInstancedProcedural(m_mesh, 0, m_material, 2, (int)m_args[1], m_materialProperty);
        }
    }

    private void RenderCullingChunksDepthPass(ScriptableRenderContext context, CommandBuffer cmd)
    {
        ProcessCullingResult();
    }

    private void RenderCullingChunks(ScriptableRenderContext context, CommandBuffer cmd)
    {
        ProcessCullingResult();
        using (new ProfilingScope(cmd, m_DrawIndirectProfilingSampler))
        {
            ClearVoxelRenderBatches();
            int drawCount = 0;
            foreach (var voxelChunk in m_visibleChunks)
            {
                bool cullingInstance = m_enablePerInstanceCulling && voxelChunk.CullResult == (byte)FrustumCullingResult.FRUSTUM_INTERSECT;

                VoxelRenderBatch batch = GetVoxelRenderBatch(voxelChunk, cullingInstance);
                batch.AddChunk(voxelChunk.Positions, voxelChunk.Colors);
            }

            foreach (var voxelBatch in m_FullyInFrustumBatch)
            {
                RenderVoxelBatch(voxelBatch, false, cmd);
            }

            foreach (var voxelBatch in m_PartInFrustumBatch)
            {
                RenderVoxelBatch(voxelBatch, true, cmd);
            }
        }
    }

    private void RenderVoxelBatch(VoxelRenderBatch voxelRenderBatch, bool cullingInstance, CommandBuffer cmd)
    {
        cmd.SetBufferData(m_transformBuffer, voxelRenderBatch.Positions);
        cmd.SetBufferData(m_inputColorsBuffer, voxelRenderBatch.Colors);

        m_args[1] = (uint)voxelRenderBatch.ActiveVoxelsNum;
        int groupThreadX = Mathf.CeilToInt((float)voxelRenderBatch.ActiveVoxelsNum / 128.0f);

        cmd.SetBufferData(m_argBuffer, m_args);
        if (m_enablePerInstanceCulling && cullingInstance)
        {
            cmd.SetBufferCounterValue(m_counterBuffer, 0);
            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingInstance, "_InputPositions", m_transformBuffer);
            cmd.SetComputeVectorParam(m_CullingInstances, "_CamPosition", m_cameraPos);
            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingInstance, "_CounterBuffer", m_counterBuffer);

            cmd.SetComputeBufferParam(m_CullingInstances, m_kernelCullingInstance, "_VisibilityBuffer", m_visibilityBuffer);

            cmd.SetComputeVectorParam(m_CullingInstances, "_CamPosition", m_cameraPos);
            cmd.SetComputeVectorParam(m_CullingInstances, "_ChunkPosition", Vector3.zero);

            cmd.SetComputeVectorParam(m_CullingInstances, "_VoxelExtends", VoxelChunk.VOXEL_HALF_SIZE);
            cmd.SetComputeFloatParam(m_CullingInstances, "_InstanceCount", voxelRenderBatch.ActiveVoxelsNum);
            Matrix4x4 v = m_camera.worldToCameraMatrix;
            Matrix4x4 p = m_camera.projectionMatrix;
            Matrix4x4 vpMatrix = p * v;

            cmd.SetComputeMatrixParam(m_CullingInstances, "_VP", vpMatrix);
            cmd.DispatchCompute(m_CullingInstances, m_kernelCullingInstance, groupThreadX, 1, 1);

            cmd.CopyCounterValue(m_counterBuffer, m_argBuffer, sizeof(uint));

            //presum visibility
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_VisibilityBufferIn", m_visibilityBuffer);
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_GroupSumArray", m_groupSumBuffer);
            cmd.SetComputeBufferParam(m_ScanInstances, m_kernelParallelPreSum, "_ScannedInstancePredicates", m_visibleInstanceSumBuffer);
            cmd.DispatchCompute(m_ScanInstances, m_kernelParallelPreSum, groupThreadX, 1, 1);


            if (groupThreadX > 1)
            {
                //presum group
                cmd.SetComputeBufferParam(m_ScanInstances, m_kernelGroupSumArray, "_GroupSumArrayIn", m_groupSumBuffer);
                cmd.SetComputeBufferParam(m_ScanInstances, m_kernelGroupSumArray, "_GroupSumArrayOut", m_groupSumScanBuffer);
                cmd.SetComputeIntParam(m_ScanInstances, "_GroupsNum", Mathf.NextPowerOfTwo(groupThreadX));

                cmd.SetComputeIntParam(m_ScanInstances, "_GroupSumArrayInSize", groupThreadX);
                cmd.DispatchCompute(m_ScanInstances, m_kernelGroupSumArray, 1, 1, 1);
                cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumScanBuffer);
            }
            else
            {
                //m_CopyTransform.SetBuffer(m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumBuffer);
                cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_GroupSumArray", m_groupSumBuffer);
            }

            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_VisibilityBuffer", m_visibilityBuffer);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_PredicateScanBuffer", m_visibleInstanceSumBuffer);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_InputPositions", m_transformBuffer);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_InputColors", m_inputColorsBuffer);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_Positions", m_finalTransforms);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyVisibleInstances, "_Colors", m_voxelColors);
            cmd.DispatchCompute(m_CopyTransform, m_kernelCopyVisibleInstances, groupThreadX, 1, 1);

            //cmd.SetGlobalVector("_ChunkPosition", voxelChunk.m_worldPosition);
            //MaterialPropertyBlock materialPropertyBlock = voxelChunk.materialProperty;
            m_materialProperty.SetVector("_ChunkPosition", Vector3.zero);
            m_materialProperty.SetTexture("_Positions", m_finalTransforms);
            m_materialProperty.SetTexture("_Colors", m_voxelColors);
            cmd.DrawMeshInstancedIndirect(m_mesh, 0, m_material, 2, m_argBuffer, 0, m_materialProperty);
        }
        else
        {
            //int threadGroupX = Mathf.CeilToInt((float)voxelChunk.Positions.Length / 128);
            cmd.SetComputeIntParam(m_CopyTransform, "_InstanceCount", (int)m_args[1]);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyTransform, "_InputPositions", m_transformBuffer);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyTransform, "_Positions", m_finalTransforms);
            cmd.SetComputeBufferParam(m_CopyTransform, m_kernelCopyTransform, "_InputColors", m_inputColorsBuffer);
            cmd.SetComputeTextureParam(m_CopyTransform, m_kernelCopyTransform, "_Colors", m_voxelColors);
            cmd.DispatchCompute(m_CopyTransform, m_kernelCopyTransform, groupThreadX, 1, 1);
            //cmd.SetGlobalVector("_ChunkPosition", voxelChunk.m_worldPosition);
            //cmd.DrawMeshInstancedIndirect(m_mesh, 0, m_material, 2, m_argBuffer);
            //MaterialPropertyBlock materialPropertyBlock = voxelChunk.materialProperty;
            //m_materialProperty.SetVector("_ChunkPosition", Vector3.zero);
            m_materialProperty.SetTexture("_Positions", m_finalTransforms);
            m_materialProperty.SetTexture("_Colors", m_voxelColors);
            cmd.DrawMeshInstancedProcedural(m_mesh, 0, m_material, 2, voxelRenderBatch.ActiveVoxelsNum, m_materialProperty);
        }
    }


    VoxelRenderBatch GetVoxelRenderBatch(VoxelChunk chunk, bool cullingInstance)
    {
        List<VoxelRenderBatch> renderList = cullingInstance ? m_PartInFrustumBatch : m_FullyInFrustumBatch;
        if (renderList.Count == 0)
        {
            VoxelRenderBatch voxelRenderBatch = VoxelRenderBatch.Get();
            renderList.Add(voxelRenderBatch);
            return voxelRenderBatch;
        }

        VoxelRenderBatch active = renderList[renderList.Count - 1];
        if ((active.ActiveVoxelsNum + chunk.VoxelsNum) >= MAX_INSTANCE_NUM)
        {
            active = VoxelRenderBatch.Get();
            renderList.Add(active);
        }

        return active;
    }

    void ClearVoxelRenderBatches()
    {
        foreach (var batch in  m_PartInFrustumBatch)
        {
            batch.ClearData();
            VoxelRenderBatch.Release(batch);
        }
        m_PartInFrustumBatch.Clear();

        foreach (var batch in m_FullyInFrustumBatch)
        { 
            batch.ClearData();
            VoxelRenderBatch.Release(batch);
        }
        m_FullyInFrustumBatch.Clear();
    }
    */

    private void LateUpdate()
    {
        
    }
}
