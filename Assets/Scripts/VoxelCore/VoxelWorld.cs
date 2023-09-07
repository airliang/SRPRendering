using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


public class VoxelWorld
{
    public Vector3Int m_worldChunkSize = Vector3Int.one;

    private VoxelChunk[,,] m_chunks = null;
    private List<VoxelChunk> m_chunksArray = new List<VoxelChunk>();
    private float3x2[] m_chunksBoundArray;
    // Start is called before the first frame update

    public void ImportChunksHMap(Texture2D heightmap)
    {

    }

    public void GenerateRandomChunks()
    {
        float heightMapScale = 16.0f;
        m_chunksArray.Clear();
        int heightMapSize = 64;
        Vector2Int chunksNum = new Vector2Int(heightMapSize / VoxelChunk.VOXELS_NUMBER.x, heightMapSize / VoxelChunk.VOXELS_NUMBER.z);
        float chunkSizeX = VoxelChunk.VOXELS_NUMBER.x;
        float chunkSizeY = VoxelChunk.VOXELS_NUMBER.y;
        float chunkSizeZ = VoxelChunk.VOXELS_NUMBER.z;
        for (int y = 0; y < chunksNum.y; y++)
        {
            for (int x = 0; x < chunksNum.x; x++)
            {
                VoxelChunk chunk = new VoxelChunk(new Vector3(x * VoxelChunk.VOXELS_NUMBER.x, 0, y * VoxelChunk.VOXELS_NUMBER.z));
                m_chunksArray.Add(chunk);
            }
        }

        Vector2 uvScale = new Vector2(1.5f, 1.5f);
        
        float voxelSizeHalf = VoxelChunk.VOXEL_HALF_SIZE.x;
        float[] pixels = new float[heightMapSize * heightMapSize];
        for (int z = 0; z < heightMapSize; z++)
        {
            for(int x = 0; x < heightMapSize; x++)
            {
                float noise = Mathf.PerlinNoise((float)x / heightMapSize * uvScale.x, (float)z / heightMapSize * uvScale.y);
                pixels[z * heightMapSize + x] = noise;
                Vector3Int posInChunk = new Vector3Int(x % VoxelChunk.VOXELS_NUMBER.x, (int)(noise * heightMapScale) % VoxelChunk.VOXELS_NUMBER.y, 
                    z % VoxelChunk.VOXELS_NUMBER.z);

                int height = (int)(noise * heightMapScale);
                for (int i = 0; i < height; i++)
                {
                    VoxelChunk voxelChunk = FindChunk(new Vector3(x + voxelSizeHalf, i + voxelSizeHalf, z + voxelSizeHalf));
                    if (voxelChunk == null)
                    {
                        voxelChunk = new VoxelChunk(new Vector3(x + voxelSizeHalf, i % VoxelChunk.VOXELS_NUMBER.y + voxelSizeHalf, z + voxelSizeHalf));
                        m_chunksArray.Add(voxelChunk);
                    }
                    voxelChunk.AddVoxel(new Vector3Int(posInChunk.x, i, posInChunk.z));
                }
            }
        }

        m_chunksBoundArray = new float3x2[m_chunksArray.Count];
        int boundIndex = 0;
        foreach (var voxelChunk in m_chunksArray)
        {
            voxelChunk.GenerateRenderingData();
            float3x2 bound = new float3x2(voxelChunk.m_worldBound.center, voxelChunk.m_worldBound.extents);
            m_chunksBoundArray[boundIndex++] = bound;
        }

        
    }

    public VoxelChunk FindChunk(Vector3 worldPos)
    {
        VoxelChunk chunk = m_chunksArray.Find((chunk) =>
        {
            return chunk.m_worldBound.Contains(worldPos);
        });

        return chunk;
    }

    public List<VoxelChunk> ActiveChunks
    {
        get
        {
            return m_chunksArray;
        }
    }

    public float3x2[] ActiveChunkBounds { get { return m_chunksBoundArray; } }
}
