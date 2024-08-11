#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
SamplerState s_point_clamp_sampler;
SamplerState s_point_repeat_sampler;
SamplerState s_linear_clamp_sampler;

// --------------------------------------------
// Output functions
// --------------------------------------------
float PackAOOutput(float AO, float depth)
{
    uint packedDepth = PackFloatToUInt(depth, 0, 23);
    uint packedAO = PackFloatToUInt(AO, 24, 8);
    uint packedVal = packedAO | packedDepth;
    // If it is a NaN we have no guarantee the sampler will keep the bit pattern, hence we invalidate the depth, meaning that the various bilateral passes will skip the sample.
    if ((packedVal & 0x7FFFFFFF) > 0x7F800000)
    {
        packedVal = packedAO;
    }

    // We need to output as float as gather4 on an integer texture is not always supported.
    return asfloat(packedVal);
}

void UnpackData(float data, out float AO, out float depth)
{
    depth = UnpackUIntToFloat(asuint(data), 0, 23);
    AO = UnpackUIntToFloat(asuint(data), 24, 8);
}

void UnpackGatheredData(float4 data, out float4 AOs, out float4 depths)
{
    UnpackData(data.x, AOs.x, depths.x);
    UnpackData(data.y, AOs.y, depths.y);
    UnpackData(data.z, AOs.z, depths.z);
    UnpackData(data.w, AOs.w, depths.w);
}

void GatherAOData(Texture2D<float> _AODataSource, float2 UV, out float4 AOs, out float4 depths)
{
    float4 data = _AODataSource.Gather(s_point_clamp_sampler, UV);
    UnpackGatheredData(data, AOs, depths);
}

