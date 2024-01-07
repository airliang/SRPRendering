using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelRenderBatch
{
    private List<Vector4> m_VoxelPositions = new List<Vector4>();
    private List<uint> m_VoxelColors = new List<uint>();

    public List<Vector4> Positions
    {
        get { return m_VoxelPositions; }
    }

    public List<uint> Colors
    {
        get { return m_VoxelColors; }
    }

    public int ActiveVoxelsNum
    {
        get { return m_VoxelColors.Count; }
    }
    
    public void AddChunk(Vector4[] positions, uint[] colors)
    {
        m_VoxelPositions.AddRange(positions);
        m_VoxelColors.AddRange(colors);
    }

    public void ClearData()
    {
        m_VoxelPositions.Clear();
        m_VoxelColors.Clear();
    }

    private static Stack<VoxelRenderBatch> s_VoxelRenderBatchPool = new Stack<VoxelRenderBatch>();

    public static VoxelRenderBatch Get()
    {
        if (s_VoxelRenderBatchPool.Count == 0)
        {
            s_VoxelRenderBatchPool.Push(new VoxelRenderBatch());
        }

        return s_VoxelRenderBatchPool.Pop();
    }

    public static void Release(VoxelRenderBatch batch)
    {
        if (batch != null)
        {
            batch.ClearData();
            s_VoxelRenderBatchPool.Push(batch);
        }
    }
}
