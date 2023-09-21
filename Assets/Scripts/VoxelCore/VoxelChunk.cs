using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct VoxelData
{
    public char visible;
    public uint color;
}

public class VoxelChunk
{
    public static Vector3Int VOXELS_NUMBER = new Vector3Int(32, 32, 32);
    public static Vector3 VOXEL_SIZE = Vector3.one;
    public static Vector3 VOXEL_HALF_SIZE = VOXEL_SIZE * 0.5f;
    VoxelData[,,] m_voxels = new VoxelData[VOXELS_NUMBER.x, VOXELS_NUMBER.y, VOXELS_NUMBER.z];

    public Vector3 m_worldPosition;
    public Bounds m_worldBound;

    public Vector4[] m_positions;   //xyz-pos, 
    public uint[] m_colors;

    private MaterialPropertyBlock m_materialProperty = new MaterialPropertyBlock();

    public MaterialPropertyBlock materialProperty
    {
        get { return m_materialProperty; }
    }

    public VoxelChunk(Vector3 worldPosition)
    {
        m_worldPosition = worldPosition;
        Vector3 center = m_worldPosition + new Vector3(VOXELS_NUMBER.x, VOXELS_NUMBER.y, VOXELS_NUMBER.z) * 0.5f;
        m_worldBound = new Bounds(center, VOXELS_NUMBER);
    }

    public void AddVoxel(int x, int y, int z, uint color)
    {
        AddVoxel(new Vector3Int(x, y, z), color);
    }

    public void AddVoxel(Vector3Int pos, uint color)
    {
        m_voxels[pos.x, pos.y, pos.z] = new VoxelData()
        {
            visible = (char)1,
            color = color
        };
    }

    public void RemoveVoxel(int x, int y, int z)
    {
        RemoveVoxel(new Vector3Int(x, y, z));
    }

    public void RemoveVoxel(Vector3Int pos)
    {
        m_voxels[pos.x, pos.y, pos.z].visible = (char)0;
    }

    public void GenerateRenderingData()
    {
        Vector3 center = m_worldPosition + new Vector3(VOXELS_NUMBER.x, VOXELS_NUMBER.y, VOXELS_NUMBER.z) * 0.5f;
        
        List<Vector4> positions = new List<Vector4>();
        List<uint> colors = new List<uint>();
        int instanceNum = 0;
        for (int x = 0; x < VOXELS_NUMBER.x; x++)
        {
            for (int y = 0; y < VOXELS_NUMBER.y; y++)
            {
                for (int z = 0; z < VOXELS_NUMBER.z; z++)
                {
                    if (m_voxels[x, y, z].visible == 1)
                    {
                        if (!IsSurround(x, y, z))
                        {
                            Vector3 posInChunk = new Vector3(x, y, z) + VOXEL_HALF_SIZE;
                            if (instanceNum == 0)
                            {
                                m_worldBound = new Bounds(posInChunk + m_worldPosition, VOXEL_SIZE);
                            }
                            else
                            {
                                m_worldBound.Encapsulate(new Bounds(posInChunk + m_worldPosition, VOXEL_SIZE));
                            }
                            positions.Add(new Vector4(posInChunk.x, posInChunk.y, posInChunk.z, 1));
                            colors.Add(m_voxels[x, y, z].color);
                            instanceNum++;
                        }
                    }
                }
            }
        }
        m_positions = new Vector4[instanceNum];
        positions.CopyTo(m_positions);
        m_colors = new uint[instanceNum];
        colors.CopyTo(m_colors);
    }

    public bool IsSurround(int x, int y, int z)
    {
        //voxel is on the edge
        if (x == VOXELS_NUMBER.x - 1 || x == 0 || y == VOXELS_NUMBER.y - 1 || y == 0 || z == VOXELS_NUMBER.z - 1 || z == 0)
        {
            return false;
        }

        if (m_voxels[x + 1, y, z].visible == 1 && m_voxels[x - 1, y, z].visible == 1 
            && m_voxels[x, y + 1, z].visible == 1 && m_voxels[x, y - 1, z].visible == 1 
            && m_voxels[x, y, z + 1].visible == 1 && m_voxels[x, y, z - 1].visible == 1)
        {
            return true;
        }

        return false;
    }

    public Vector3 WorldPosition
    {
        get { return m_worldPosition; }
        set { m_worldPosition = value; }
    }

    public Vector4[] Positions
    {
        get { return m_positions; }
    }

    public uint[] Colors { get { return m_colors; } }

    public byte CullResult
    { get; set; }
}
