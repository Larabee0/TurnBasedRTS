using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// 5 bytes min
/// Serializable componet for authroing HexMeshes in the HexGridChunk prefab.
/// This controls what type of mesh will be set for the entity.
/// </summary>
[Serializable]
public struct HexMeshData : IComponentData
{
    public MeshType type;

    [HideInInspector]
    public int meshIndex;
}

/// <summary>
/// tagging component to trigger the <see cref="HexChunkMeshApplicatorSystem"/> to intitialise this mesh component
/// with a unique empty mesh & also to assign the correct material instance to it.
/// </summary>
public struct HexMeshUninitilised : IComponentData { }

/// <summary>
/// 4 bytes min
/// The meshes for a whole grid are kept in 1 big RenderMeshArray Shared Component, along side the materials.
/// This stores the raw index that this HexMesh entity's mesh is stored at in the shared component, so the correct
/// mesh can be retrived from the array during
/// mesh to entity application <see cref="HexChunkMeshApplicatorSystem.CompleteTriangulator(EntityCommandBuffer)"/>
/// </summary>
public struct HexMeshChunkIndex : IComponentData
{
    public int meshArrayIndex;
}

/// <summary>
/// 12 bytes min
/// Debugging component added when enable in the <see cref="HexMeshBaker"/>
/// to display the submesh index, triangle & vertex array length this entity is renderering.
/// </summary>
public struct HexMeshDebugger : IComponentData
{
    public int subMesh;
    public uint triangleArrayCount;
    public int vertexArrayCount;
}


public struct MeshDataWrapper
{
    public double TimeStamp;
    public UnsafeParallelHashSet<int> chunksIncluded;
    public Mesh.MeshDataArray meshDataArray;

    public MeshDataWrapper(double timeStamp, UnsafeParallelHashSet<int> chunksIncluded, Mesh.MeshDataArray meshDataArray)
    {
        TimeStamp = timeStamp;
        this.chunksIncluded = chunksIncluded;
        this.meshDataArray = meshDataArray;
    }
}
