using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public struct EdgeVertices
{
    public float3 v1;
    public float3 v2;
    public float3 v3;
    public float3 v4;
    public float3 v5;

    public EdgeVertices(float3 corner1, float3 corner2)
    {
        v1 = corner1;
        v2 = math.lerp(corner1, corner2, 0.25f);
        v3 = math.lerp(corner1, corner2, 0.5f);
        v4 = math.lerp(corner1, corner2, 0.75f);
        v5 = corner2;
    }

    public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
    {
        v1 = corner1;
        v2 = math.lerp(corner1, corner2, outerStep);
        v3 = math.lerp(corner1, corner2, 0.5f);
        v4 = math.lerp(corner1, corner2, 1f - outerStep);
        v5 = corner2;
    }

    public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step)
    {
        EdgeVertices result;
        result.v1 = HexMetrics.TerraceLerp(a.v1, b.v1, step);
        result.v2 = HexMetrics.TerraceLerp(a.v2, b.v2, step);
        result.v3 = HexMetrics.TerraceLerp(a.v3, b.v3, step);
        result.v4 = HexMetrics.TerraceLerp(a.v4, b.v4, step);
        result.v5 = HexMetrics.TerraceLerp(a.v5, b.v5, step);
        return result;
    }
}
