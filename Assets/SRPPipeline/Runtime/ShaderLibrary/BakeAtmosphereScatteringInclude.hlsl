#ifndef BAKE_ATMOSPHERE_SCATTERING_INCLUDED
#define BAKE_ATMOSPHERE_SCATTERING_INCLUDED

RWTexture2DArray<half4> _Cubemap;

//https://www.shadertoy.com/view/WlsSRr
// direction -> cubemap face normal
float3 GetCubeFaceDir(float3 dir)
{
    float3 absDir = abs(dir);
    float maxDir = max(max(absDir.x, absDir.y), absDir.z);
    float3 faceDir;
    faceDir.x = step(maxDir, absDir.x) * sign(dir.x);
    faceDir.y = step(maxDir, absDir.y) * sign(dir.y);
    faceDir.z = step(maxDir, absDir.z) * sign(dir.z);
    return faceDir;
}

// cubemap face normal -> face ID
float CubeFaceDirToID(float3 faceDir)
{
    return dot(clamp(faceDir, 0., 1.), float3(0., 1., 2.)) + dot(clamp(-faceDir, 0., 1.), float3(3., 4., 5.));
}

// cubemap face normal, direction -> face UVs
float2 CubeFaceCoords(float3 faceDir, float3 viewDir)
{
    float3 uv3d = viewDir / dot(viewDir, faceDir) - faceDir;
    float3 uDir = float3(faceDir.z + abs(faceDir.y), 0, -faceDir.x);
    float3 vDir = float3(0, 1. - abs(faceDir.y), -faceDir.y);
    return float2(dot(uv3d, uDir), dot(uv3d, vDir)) * .5 + .5;
}

#define CUBEMAP_SIZE 32

#define CUBE_FACE_POSITIVEX 0
#define CUBE_FACE_NEGATIVEX 1
#define CUBE_FACE_POSITIVEY 2
#define CUBE_FACE_NEGATIVEY 3
#define CUBE_FACE_POSITIVEZ 4
#define CUBE_FACE_NEGATIVEZ 5

float3 GetDirection(float2 uv, uint face)
{
    float3 dir;
    
    if (face == CUBE_FACE_POSITIVEX)
    {
        dir = float3(1, uv.y, -uv.x);
    }
    else if (face == CUBE_FACE_NEGATIVEX)
    {
        dir = float3(-1, uv.y, uv.x);
    }
    else if (face == CUBE_FACE_POSITIVEY)
    {
        dir = float3(uv.x, 1, -uv.y);
    }
    else if (face == CUBE_FACE_NEGATIVEY)
    {
        dir = float3(uv.x, -1, uv.y);
    }
    else if (face == CUBE_FACE_POSITIVEZ)
    {
        dir = float3(uv.x, uv.y, 1);
    }
    else if (face == CUBE_FACE_NEGATIVEZ)
    {
        dir = float3(-uv.x, uv.y, -1);
    }

    return normalize(dir);
}


float3 GetDirection(uint2 faceCoord, uint face)
{
    float3 dir;
    float2 uv = ((float2) faceCoord + 0.5) / CUBEMAP_SIZE * 2 - 1;
    
    if (face == CUBE_FACE_POSITIVEX)
    {
        dir = float3(1, uv.y, -uv.x);
    }
    else if (face == CUBE_FACE_NEGATIVEX)
    {
        dir = float3(-1, uv.y, uv.x);
    }
    else if (face == CUBE_FACE_POSITIVEY)
    {
        dir = float3(uv.x, 1, -uv.y);
    }
    else if (face == CUBE_FACE_NEGATIVEY)
    {
        dir = float3(uv.x, -1, uv.y);
    }
    else if (face == CUBE_FACE_POSITIVEZ)
    {
        dir = float3(uv.x, uv.y, 1);
    }
    else if (face == CUBE_FACE_NEGATIVEZ)
    {
        dir = float3(-uv.x, uv.y, -1);
    }

    return normalize(dir);
}



#endif
