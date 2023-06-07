using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using MeshCollider = Unity.Physics.MeshCollider;

/// <summary>
/// Orignally just a tag component but added Chunk Index to it cos convientant.
/// </summary>
public struct HexChunkTag : IComponentData
{
    public static implicit operator int(HexChunkTag v) { return v.Index; }
    public static implicit operator HexChunkTag(int v) { return new HexChunkTag { Index = v }; }
    public int Index;
}

/// <summary>
/// Entities containing a mesh for each component of the chunk.
/// This component is attached to the chunk root entity
/// </summary>
public struct HexChunkMeshEntities : IComponentData
{
    public Entity Terrain;
    public Entity Rivers;
    public Entity Water;
    public Entity WaterShore;
    public Entity Estuaries;
    public Entity Roads;
    public Entity Walls;

    public Entity this[int index] => index switch
    {
        0 => Terrain,
        1 => Rivers,
        2 => Water,
        3 => WaterShore,
        4 => Estuaries,
        5 => Roads,
        6 => Walls,
        _ => Entity.Null,
    };
}

/// <summary>
/// During Chunk Triangulation, the cells provide their chunks with this buffer containing all the data needed for the chunk to triangulate.
/// This includes all cells within the chunk and any directly adjacent cells to the chunk.
/// </summary>
public struct HexChunkCellWrapper : IBufferElementData, IComparable<HexChunkCellWrapper>
{
    public HexCellBasic cellBasic;
    public HexCellTerrain cellTerrain;
    public HexCellNeighbours cellNeighbours;
    public int Index => cellBasic.Index;
    public float3 Position => cellBasic.Position;
    public int Elevation => cellTerrain.elevation;
    public int wrapSize;
    public int CompareTo(HexChunkCellWrapper other)
    {
        return Index.CompareTo(other.Index);
    }
}

/// <summary>
/// Component added to cells that need to add HexChunkCellWrapper to a neighbouring chunk
/// </summary>
public struct HexCellChunkNeighbour : IBufferElementData
{
    public Entity value;
    public static implicit operator Entity(HexCellChunkNeighbour v) { return v.value; }
    public static implicit operator HexCellChunkNeighbour(Entity v) { return new HexCellChunkNeighbour { value = v }; }
}

public struct HexCellGetChunkNeighbour : IComponentData { }

public struct HexChunkCellBuilder : IBufferElementData
{
    public Entity Chunk;
    public static implicit operator Entity(HexChunkCellBuilder v) { return v.Chunk; }
    public static implicit operator HexChunkCellBuilder(Entity v) { return new HexChunkCellBuilder { Chunk = v }; }
}

public struct HexChunkRefreshRequest : IComponentData { }

public struct HexChunkMeshUpdating : IComponentData { public double timeStamp; }

public struct HexChunkRefresh : IComponentData { }

public struct HexChunkColliderRebuildRequest : IComponentData { }

public struct HexChunkColliderRebuild : IComponentData { }

public struct HexChunkColliderReference : IComponentData
{
    public Entity value;
}

public struct HexChunkCellDataUnsortedCompleted : IComponentData { }
public struct HexChunkCellDataCompleted : IComponentData { }

public struct HexChunkTriangulatorArray : IComponentData
{
    public UnsafeList<MeshDataWrapper> meshDataWrappers;
    public NativeArray<float4> noiseColours;
    public NativeArray<HexHash> hashGrid;
}

public struct HexChunkColliderForDisposal : IComponentData
{
    public BlobAssetReference<Collider> colliderBlob;

    public void Dispose()
    {
        if (colliderBlob.IsCreated)
        {
            colliderBlob.Dispose();
        }
    }
}

public struct HexChunkColliderArray : IComponentData
{
    public UnsafeList<HexChunkColliderQueue> colliderQueue;
}

public struct HexChunkColliderQueue
{
    public Mesh.MeshDataArray colliderTargetMeshes;
    public UnsafeList<Entity> entities;
}

public struct HexChunkCollier : IComponentData { }

public class HexChunkColliderBatches : IComponentData
{
    public List<HexChunkColliderBatch> batches = new();
}

public class HexChunkColliderBatch
{
    public HexChunkColliderQueue batch;
    public List<Task<BlobAssetReference<Collider>>> tasks = new();
    private bool scheduled = false;
    public bool AllCompleted
    {
        get
        {
            if (!scheduled)
            {
                return false;
            }
            for (int i = 0; i < tasks.Count; i++)
            {
                if (!tasks[i].IsCompleted)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public void Dispose()
    {
        batch.colliderTargetMeshes.Dispose();
        batch.entities.Dispose();
        tasks.Clear();
    }

    public void Schedule()
    {
        if (scheduled)
        {
            return;
        }
        scheduled = true;

        // throws index out of range exceptions from thread (batch.colliderTargetMeshes[meshIndex];)
        // Because BakeCollider is running on another thread right from call, meshIndex will change with the for loop
        // and also change for the thread (its parsed by reference not value).

        // This leads to multiple threads with the same index and potentially threads with an index = length.
        // index = length will throw an error index out of range exeception.

        // (when a for loop ends meshIndex becomes equal to length,
        // this is how it forfills its "meshIndex < length;" condition to end the loop)

        // for (int meshIndex = 0; meshIndex < batch.entities.Length; meshIndex++)
        // {
        //     tasks.Add(Task<BlobAssetReference<Collider>>.Factory.StartNew(() => BakeCollider(batch.colliderTargetMeshes, meshIndex)));
        // }


        // no errors
        // Solution is Closure. Here a local method is used to assign the current index to a local variable called localMeshIndex.
        // Calling the method breaks the reference to the for loop meshIndex and ensures no duplicate indices for the threads
        // and prevents the index = length condition.

        for (int meshIndex = 0; meshIndex < batch.entities.Length; meshIndex++)
        {
            void IndexClosure()
            {
                int localMeshIndex = meshIndex;
                tasks.Add(Task<BlobAssetReference<Collider>>.Factory.StartNew(() => BakeCollider(batch.colliderTargetMeshes, localMeshIndex)));
            }
            IndexClosure();
        }

    }

    public void GetCompletedData(NativeList<BlobAssetReference<Collider>> colliders, NativeList<Entity> entities,NativeHashSet<Entity> existinChunks)
    {
        if (!AllCompleted)
        {
            return;
        }
        if (batch.entities.Length > 0)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                Entity chunkEntity = batch.entities[i];
                if (tasks[i].IsCompletedSuccessfully && !existinChunks.Contains(chunkEntity))
                {
                    colliders.Add(tasks[i].Result);
                    
                    entities.Add(chunkEntity);
                    existinChunks.Add(chunkEntity);
                }
                else if (existinChunks.Contains(chunkEntity))
                {
                    tasks[i].Result.Dispose();
                }
                else if (tasks[i].IsFaulted)
                {
                    Debug.LogException(tasks[i].Exception);
                }
                else if (tasks[i].IsCanceled)
                {
                    Debug.LogWarning("Collider Task was canceled");
                }
            }
        }

    }

    private static BlobAssetReference<Collider> BakeCollider(Mesh.MeshDataArray meshDataArray, in int index)
    {
        var triangles = new NativeArray<int>(meshDataArray[index].GetSubMesh(0).indexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        meshDataArray[index].GetIndices(triangles, 0);
        var tempTrianges = new NativeArray<int3>(triangles.Length / 3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        unsafe
        {
            UnsafeUtility.MemCpy(tempTrianges.GetUnsafePtr(), triangles.GetUnsafePtr(), triangles.Length * UnsafeUtility.SizeOf<int>());
        }

        var vertices = new NativeArray<Vector3>(meshDataArray[index].GetSubMesh(0).vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        meshDataArray[index].GetVertices(vertices);
        BlobAssetReference<Collider> collider = MeshCollider.CreateManaged(vertices.Reinterpret<float3>(), tempTrianges, CollisionFilter.Default, Unity.Physics.Material.Default);
        triangles.Dispose();
        tempTrianges.Dispose();
        vertices.Dispose();
        return collider;
    }
}