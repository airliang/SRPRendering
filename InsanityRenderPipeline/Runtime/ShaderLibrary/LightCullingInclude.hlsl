#ifndef LIGHT_CULLING_INCLUDED
#define LIGHT_CULLING_INCLUDED

#define FRUSTUM_PLANE_LEFT 0
#define FRUSTUM_PLANE_RIGHT 1
#define FRUSTUM_PLANE_BOTTOM 2
#define FRUSTUM_PLANE_TOP 3
#define FRUSTUM_PLANE_NEAR 4
#define FRUSTUM_PLANE_FAR 5

#define TILE_SIZE 16
#define MAX_LIGHT_NUM_PER_TILE 256

struct TileFrustum
{
    float4 planes[4]; // left, right, top, bottom, near, far frustum planes.
};

struct TileAABB
{
    float3 center;
    float3 extents;
};

bool SphereOutsidePlane(float4 sphere, float4 plane)
{
    return dot(plane, float4(sphere.xyz, 1)) + sphere.w <= 0;
}

bool SphereIntersectsAABB(in float4 sphere, in TileAABB aabb)
{
    float3 vDelta = max(0, abs(aabb.center - sphere.xyz) - aabb.extents);
    float fDistSq = dot(vDelta, vDelta);
    return fDistSq <= sphere.w * sphere.w;
}

// Check to see of a light is partially contained within the frustum.
// sphere must in view space
bool SphereInsideFrustum(float4 sphere, TileFrustum frustum, float zNear, float zFar)
{
    bool result = true;
 
    if (sphere.z + sphere.w < zNear || sphere.z - sphere.w > zFar)
    {
        result = false;
    }
 
    // Then check frustum planes
    for (int i = 0; i < 4 && result; i++)
    {
        if (SphereOutsidePlane(sphere, frustum.planes[i]))
        {
            result = false;
        }
    }
 
    return result;
}

bool SpotlightVsAABB(float3 position, float3 direction, float range, float angle, TileAABB aabb)
{
    if (!SphereIntersectsAABB(float4(position, range), aabb))
        return false;
    float sphereRadius = length(aabb.extents); //dot(aabb.extents, aabb.extents);
    float3 v = aabb.center - position;
    float lenSq = dot(v, v);
    float v1Len = dot(v, direction);
    float distanceClosestPoint = cos(angle) * sqrt(lenSq - v1Len * v1Len) - v1Len * sin(angle);
    bool angleCull = distanceClosestPoint > sphereRadius;
    bool frontCull = v1Len > sphereRadius + range;
    bool backCull = v1Len < -sphereRadius;
    return !(angleCull || frontCull || backCull);
}


#endif
