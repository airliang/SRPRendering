using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using TMPro;


public enum FrustumCullingResult : byte
{
    FRUSTUM_INSIDE,
    FRUSTUM_INTERSECT,
    FRUSTUM_OUTSIDE
}

[BurstCompile]
public struct ParallelCullingJob : IJobParallelFor
{
    public NativeArray<byte> _cullingResults;
    [ReadOnly]
    public NativeArray<float3x2> _meshBounds;
    [ReadOnly]
    public NativeArray<float4> _frustumPlanes;

    private int frustumsNum;

    public ParallelCullingJob(NativeArray<float3x2> meshBounds, NativeArray<float4> frustumPlanes, NativeArray<byte> cullingResults)
    {
        _meshBounds = meshBounds;
        _frustumPlanes = frustumPlanes;
        frustumsNum = _frustumPlanes.Length / 6;
        _cullingResults = cullingResults;
    }

    private bool VisibleTest(float3 center, float3 extents, int planeIndex = 0)
    {
        uint planeMask = 63;

        //Vector3 m = center; // center of AABB
        //Vector3 extent = extents; // half-diagonal
        uint mk = 1;
        while (mk <= planeMask)
        {
            float4 plane = _frustumPlanes[planeIndex];
            // if clip plane is active...
            if ((planeMask & mk) > 0)
            {
                float3 normal = plane.xyz;
                float dist = math.dot(normal, center) + plane.w;
                float radius = math.dot(extents, math.abs(normal));

                if (dist + radius < 0)
                    return false;
            }
            mk += mk;
            planeIndex++; // next plane
        }
        return true;
    }

    float GetPlaneDistanceToPoint(float4 plane, float3 point)
    {
        return math.dot(plane.xyz, point) + plane.w;
    }

    private FrustumCullingResult FrustumAABBTest(float3 center, float3 extents)
    {
        FrustumCullingResult result = FrustumCullingResult.FRUSTUM_INSIDE;

        for (int i = 0; i < 6; ++i)
        {
            int outSide = 0;
            int inSide = 0;
            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(-extents.x, -extents.y, -extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(extents.x, -extents.y, -extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(-extents.x, -extents.y, extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(extents.x, -extents.y, extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(-extents.x, extents.y, -extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(extents.x, extents.y, -extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }

            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(-extents.x, extents.y, extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            }
            if (GetPlaneDistanceToPoint(_frustumPlanes[i], center + new float3(extents.x, extents.y, extents.z)) < 0)
            {
                outSide++;
            }
            else
            {
                inSide++;
            } 
            // If all corners are out         
            if (inSide == 0)
            {
                return FrustumCullingResult.FRUSTUM_OUTSIDE;
            } // If some corners are out and others are in         
            else if (outSide > 0)
            {
                result = FrustumCullingResult.FRUSTUM_INTERSECT;
            }
        }

        return result;
    }

    public void Execute(int index)
    {
        float3x2 bounds = _meshBounds[index];
        //bool visible = false;
        //for (int i = 0; i < frustumsNum; i++)
        //{
        //    visible = VisibleTest(bounds.c0, bounds.c1, i * 6);
        //    if (visible)
        //        break;
        //}

        //_cullingResults[index] = visible;
        _cullingResults[index] = (byte)FrustumAABBTest(bounds.c0, bounds.c1);
    }
}

//[BurstCompile]
public class ParallelCullingJobData
{
    private JobHandle _cullJobHandle;
    public ParallelCullingJob job;
    public NativeArray<byte> _cullingResults;
    public NativeArray<float4> _cullingPlanes;
    public NativeArray<float3x2> _cullingBounds;
    public Vector3 _cameraPos;

    public void Execute(float3x2[] meshBounds, Plane[] frustumPlanes, Vector3 cameraPos)
    {
        _cullingResults = new NativeArray<byte>(meshBounds.Length, Allocator.TempJob);
        _cullingPlanes = new NativeArray<float4>(frustumPlanes.Length, Allocator.TempJob);
        _cullingBounds = new NativeArray<float3x2>(meshBounds.Length, Allocator.TempJob);
        for (int i = 0; i < _cullingPlanes.Length; i++)
        {
            _cullingPlanes[i] = new float4(frustumPlanes[i].normal.x, frustumPlanes[i].normal.y,
                frustumPlanes[i].normal.z, frustumPlanes[i].distance);
        }
        _cullingBounds.CopyFrom(meshBounds);
        ParallelCullingJob job = new ParallelCullingJob(_cullingBounds, _cullingPlanes, _cullingResults);
        _cullJobHandle = job.Schedule(meshBounds.Length, meshBounds.Length / SystemInfo.processorCount + 1);
        _cameraPos = cameraPos;
    }

    public bool WaitForComplete()
    {

        _cullJobHandle.Complete();
        return true;
    }

    public void Release()
    {
        if (_cullingResults.IsCreated)
            _cullingResults.Dispose();

        if (_cullingPlanes.IsCreated)
            _cullingPlanes.Dispose();

        if ( _cullingBounds.IsCreated)
            _cullingBounds.Dispose();
    }
}

