using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct HexMeshData : IComponentData
{
    public MeshType type;
    public bool useCollider;
    public bool useCellData;
    public bool useUVCoordinates;
    public bool useUV2Coordinates;

    [HideInInspector]
    public int meshIndex;
}

public struct HexMeshUninitilised : IComponentData { }

public struct HexMeshChunkIndex : IComponentData
{
    public int meshArrayIndex;
}

public struct HexMeshDebugger : IComponentData
{
    public int subMesh;
    public uint triangleArrayCount;
    public int vertexArrayCount;
}