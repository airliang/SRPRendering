# Indirect Rendering With Compute Shader
## Introduction to Indirect Rendering
In conventional rendering, CPU decides the results of rendering. For example, if we are going to render instances, the number of instances is decided by cpu. 
What about if we don't want to render the result by CPU? Indirect rendering is a good option to solve this problem. And why is it named Indirect Rendering? The strait meaning is that render objects by the index in the array.
## API Understanding
Unity Core API: 
__CommandBuffer.DrawMeshInstancedIndirect__
__Graphics.DrawMeshInstancedIndirect__
Key parameters explanation

![](indirect_rendering/indirect_api_param.PNG)

bufferWithArgs: 5 integer numbers at given argsOffset.
For example, if we use the offset of bufferWithArgs begin with 1, we should write our codes like this:
```c#
m_args[0] = 0;  
m_args[1] = numOfIndex;             // 0 - index count per instance, 
m_args[2] = 0;                      // 1 - instance count
m_args[3] = 0;                      // 2 - start index location
m_args[4] = 0;                      // 3 - base vertex location
m_args[5] = 0;                      // 4 - start instance location

m_argsBuffer = new ComputeBuffer(6, sizeof(uint), ComputeBufferType.IndirectArguments);
Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, m_argsBuffer, 4, materialPropertyBlock);
```
## Instance rendering Using Indirect Draw
Goal: Invisible instances will be culled and not be drawn.
### Data
- Original transformBuffer
- Original boundingBuffer
- argBuffer
- visibleBuffer
- Visible transformBuffer

### Rendering Process
![](indirect_rendering/process.png)

All the steps should be implemented by __compute shader__ except DrawIndirect.
### Per Instance Culling
We use compute shader to test the visibility of each instance, and of course each instance is culled in a single thread. Then we put the culled result into the visible buffer.
We calculate the visibility of each instance by compute shader. This step is very simple, setting the original transform buffer as input and then calculating visibility as output.

![](indirect_rendering/culling.png)

We should know how many instances will be drawn after culling. The function __InterlockedAdd__ may be used to perform an atomic add of value. 
For example, the visibility value will be 1 or 0. We get the code:
```c++
_IsVisibleBuffer[tID] = isVisible;
InterlockedAdd(_ArgsBuffer[argsIndex], isVisible);
```
Remember that __ArgsBuffer__ can represent the instance count.
>__Note__: __InterlockedAdd__ function always causes a problem in mobile platform. We should use another way to solve this program.
Instead of using __InterlockedAdd__, we use the __CounterBuffer__ to record the visible number of instances.
Declare counter buffer:
```c#
var buffer = new ComputeBuffer(size, sizeof(int), ComputeBufferType.Counter);
counterBuffer.SetCounterValue(0);
```
Use it in compute shader:
```c++
RWStructuredBuffer<int> counterBuffer;
counterBuffer.IncrementCounter();
```
Then copy the count value to argBuffer:
```c#
ComputeBuffer.CopyCount(counterBuffer, argBuffer, 0);
```
### Compaction
We should pack the visibility buffer to a final visible transform buffer. As the output visibility buffer shown above, the false visibility test must be erased from the original transform buffer.
We can use the parallel prefix sum to finish the compaction.
Here is the up pass of parallel prefix sum:

![](indirect_rendering/presum_uppass.png)

Down pass:

![](indirect_rendering/presum_downpass.png)

See the result here:

![](indirect_rendering/presum_result.png)

### Copy Visible Transforms
After we get the index result, we can copy the visible transforms into the final transform buffer which will be used in the vertexshader as the worldspace transform.
We use the dispatch thread id as the index of Input and Output buffer above, then we use the Output value as the final transform buffer index.
```c++
StructuredBuffer<Transform> _OriginTransforms;
RWStructureBuffer<Transform> _FinalVisibleTransforms;
StructuredBuffer<int> _VisibleBuffer;
StructuredBuffer<int> _VisibleIndexBuffer;

[numthreads(128,1,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    uint tid = id.x;
    if (_VisibleBuffer[tid] == 1)
    {
        int packIndex = _VisibleIndexBuffer[tid];
        _FinalVisibleTransforms[packIndex] = _OriginTransforms[tid];
    }
};

```
We can get the transform from the _FinalVisibleTransforms buffer in our vertex shader. This is an example:
```c++
StructuredBuffer<Transform> _FinalVisibleTransforms;

Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
{
    matrix4x4 localToWorld = _FinalVisibleTransforms[instanceID].localToWorld;
    ......
}
```
### Applications
If we are rendering a large numbers of plants such as grass, we can use a chunk to represent a group of grass in the world. Then we can cull the chunk in CPU first. After culling the chunk, we can use computeshader to implement the per instance culling of each chunk.

![](indirect_rendering/applications.png)

### Level of Details of Instance
#### API usage
According to the Indirect Argument buffer, it can represent the base vertex location and base index location. So we can combine all lod submesh into a single mesh, and record the geometry information of each submesh.
If the instance has 2 levels of LODs, then we can render the LOD in this way:
```c#
public class IndirectRenderingMesh
{
    public Mesh mesh;
    public Material material;
    public MaterialPropertyBlock lod00MatPropBlock;
    public MaterialPropertyBlock lod01MatPropBlock;

    public MaterialPropertyBlock shadowLod00MatPropBlock;
    public MaterialPropertyBlock shadowLod01MatPropBlock;

    public uint numOfVerticesLod00;
    public uint numOfVerticesLod01;
    public uint numOfIndicesLod00;
    public uint numOfIndicesLod01;
}

public void GenerateRenderData(Mesh[] lodMeshes)
{
    IndirectRenderingMesh irm = new IndirectRenderingMesh();
            
    // Initialize Mesh
    irm.mesh = new Mesh()
    //Combine the lodMeshes into a single mesh......
    irm.numOfVerticesLod00 = (uint)lodMeshes[0].vertexCount;
    irm.numOfVerticesLod01 = (uint)lodMeshes[1].vertexCount;

    irm.numOfIndicesLod00 = lodMeshes[0].GetIndexCount(0);
    irm.numOfIndicesLod01 = lodMeshes[1].GetIndexCount(0);
    
    //Setup arg buffer
    uint[] args = new uint[10];
    //LOD 0
    args[0] = irm.numOfIndicesLod00;    // 0 - index count per instance, 
    args[1] = 0;                        // 1 - instance count
    args[2] = 0;                        // 2 - start index location
    args[3] = 0;                        // 3 - base vertex location
    args[4] = 0;                        // 4 - start instance location
    //LOD 1
    args[5] = irm.numOfIndicesLod01;    // 0 - index count per instance, 
    args[6] = 0;                        // 1 - instance count
    args[7] = args[0];                  // 2 - start index location
    args[8] = 0;                        // 3 - base vertex location
    args[9] = 0;                        // 4 - start instance location
}

void DrawInstances()
{
    //Draw LOD 0
    Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_ArgsBuffer, 0);
    //Draw LOD 1
    Graphics.DrawMeshInstancedIndirect(irm.mesh, 0, irm.material, m_ArgsBuffer, sizeof(uint) * 5);
}
```

#### Sorting
We know that lod level is relative to the distance between the instance and the camera. So we have to sort the whole instances array by distance to camera. The best option of the algorithms is Bitonic Sorting. Because __Bitonic Sorting__ is appropriate in GPU programming.
After sorting the instances, we should find real index and the lod level according to the sorting datas.
```c++
StructuredBuffer<SortingData> _SortingData;

void CSMain(uint3 _dispatchThreadId)
{
    uint tID = _dispatchThreadID.x;
    SortingData sortingData = _SortingData[tID];
    int index = sortingData.index;
    int lod = sortingData.lod;
    //write the instance count of the argbuffers
    ......
}
```

## Reference
[Indirect-Rendering-With-Compute-Shaders](https://github.com/ellioman/Indirect-Rendering-With-Compute-Shaders)
