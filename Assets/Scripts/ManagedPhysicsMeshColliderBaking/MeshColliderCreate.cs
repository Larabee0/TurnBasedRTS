using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
public static class MeshColliderCreate
{
    public static void Create(float3[] vertices, int3[] triangles)
    {
       float3[] uniqueVertices = MeshConnectivityBuilder.WeldVertices(triangles, vertices);
    }


    [Conditional(CompilationSymbols.CollectionsChecksSymbol), Conditional(CompilationSymbols.DebugChecksSymbol)]
    public static void CheckIndexAndThrow(int index, int length, int min = 0)
    {
        if (index < min || index >= length)
            throw new IndexOutOfRangeException($"Index {index} is out of range [{min}, {length}].");
    }

}
