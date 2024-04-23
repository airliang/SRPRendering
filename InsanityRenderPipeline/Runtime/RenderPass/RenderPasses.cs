using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Insanity
{
    public partial class RenderPasses
    {
        static Mesh s_FullScreenTriangleMesh;

        public static void Initialize()
        {
            float nearClipZ = -1;
            if (SystemInfo.usesReversedZBuffer)
                nearClipZ = 1;

            if (s_FullScreenTriangleMesh == null)
            {
                s_FullScreenTriangleMesh = new Mesh();
                s_FullScreenTriangleMesh.vertices = GetFullScreenTriangleVertexPosition(nearClipZ);
                s_FullScreenTriangleMesh.uv = GetFullScreenTriangleTexCoord();
                s_FullScreenTriangleMesh.triangles = new int[3] { 0, 1, 2 };
            }

            // Should match Common.hlsl
            static Vector3[] GetFullScreenTriangleVertexPosition(float z /*= UNITY_NEAR_CLIP_VALUE*/)
            {
                var r = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    Vector2 uv = new Vector2((i << 1) & 2, i & 2);
                    r[i] = new Vector3(uv.x * 2.0f - 1.0f, uv.y * 2.0f - 1.0f, z);
                }
                return r;
            }

            // Should match Common.hlsl
            static Vector2[] GetFullScreenTriangleTexCoord()
            {
                var r = new Vector2[3];
                for (int i = 0; i < 3; i++)
                {
                    if (SystemInfo.graphicsUVStartsAtTop)
                        r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
                    else
                        r[i] = new Vector2((i << 1) & 2, i & 2);
                }
                return r;
            }
        }

        public static void Cleanup()
        {
            CoreUtils.Destroy(s_FullScreenTriangleMesh);
        }
    }
}

