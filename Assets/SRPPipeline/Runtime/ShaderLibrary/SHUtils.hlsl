#ifndef SH_UTILS_INCLUDED
#define SH_UTILS_INCLUDED

//L = 0 sh function 
float Y0(float3 v)
{
    return 0.2820947917f;
}

//L = 1, M = -1
float Y1(float3 v)
{
    return -0.4886025119f * v.y;
}
//L = 1, M = 0
float Y2(float3 v)
{
    return 0.4886025119f * v.z;
}
//L = 1, M = 1
float Y3(float3 v)
{
    return -0.4886025119f * v.x;
}

//L = 2, M = -2
float Y4(float3 v)
{
    return 1.0925484306f * v.x * v.y;
}
//L = 2, M = -1
float Y5(float3 v)
{
    return -1.0925484306f * v.y * v.z;
}
//L = 2, M = 0
float Y6(float3 v)
{
    return 0.3153915652f * (3.0f * v.z * v.z - 1.0f);
}
//L = 2, M = 1
float Y7(float3 v)
{
    return -1.0925484306f * v.x * v.z;
}
//L = 2, M = 2
float Y8(float3 v)
{
    return 0.5462742153f * (v.x * v.x - v.y * v.y);
}

//L = 3, M = -3
float Y9(float3 v)
{
    return -0.59004359 * v.y * (3.0f * v.x * v.x - v.y * v.y);
}
//L = 3, M = -2
float Y10(float3 v)
{
    return 2.89061144 * v.x * v.y * v.z;
}
//L = 3, M = -1
float Y11(float3 v)
{
    return -0.4570458 * v.y * (-1 + 5.0 * v.z * v.z);
}
//L = 3, M = 0
float Y12(float3 v)
{
    return 0.37317633259 * v.z * (5.0f * v.z * v.z - 3.0f);
}
//L = 3, M = 1
float Y13(float3 v)
{
    return 0.4570458 * v.y * (-1 + 5.0 * v.z * v.z);
}
//L = 3, M = 2
float Y14(float3 v)
{
    return 1.44530572132 * (v.x * v.x - v.y * v.y) * v.z;
}
//L = 3, M = 3
float Y15(float3 v)
{
    return -0.59004359 * v.x * (v.x * v.x - 3.0f * v.y * v.y);
}

float Y(uint L, float3 V)
{
    switch (L)
    {
    case 0:
        return Y0(V);
    case 1:
        return Y1(V);
    case 2:
        return Y2(V);
    case 3:
        return Y3(V);
    case 4:
        return Y4(V);
    case 5:
        return Y5(V);
    case 6:
        return Y6(V);
    case 7:
        return Y7(V);
    case 8:
        return Y8(V);
    default:
        return 0.0f;
    }
}

#endif
