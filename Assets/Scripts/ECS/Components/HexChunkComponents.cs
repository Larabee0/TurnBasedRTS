using System;
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
/// 4 bytes min
/// Orignally just a tag component but added Chunk Index to it cos convientant.
/// </summary>
public struct HexChunkTag : IComponentData
{
    public static implicit operator int(HexChunkTag v) { return v.Index; }
    public static implicit operator HexChunkTag(int v) { return new HexChunkTag { Index = v }; }
    public int Index;
}

/// <summary>
/// 56 bytes min
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
/// 8 bytes min
/// A component which stores a reference to the HexGrid root entity for the chunks to use
/// </summary>
public struct HexGridReference : IComponentData
{
    public Entity Value;
    public static implicit operator Entity(HexGridReference v) { return v.Value; }
    public static implicit operator HexGridReference(Entity v) { return new HexGridReference { Value = v }; }
}

/// <summary>
/// 204 bytes min
/// During Chunk Triangulation, the cells provide their chunks with this buffer containing
/// all the data needed for the chunk to triangulate all meshes in the chunk.
/// 
/// ## APPLIES TO <see cref="TriangulateChunksJob"/> ONLY ##
/// The buffer includes all cells within the chunk and any directly adjacent cells to the chunk.
/// It does not include all cells in the grid and thus the buffer cannot be accessed by just the cell's index.
/// 
/// The job sorts the buffer by cell index, then binary searches that buffer. With the chunks set to 4x4 cells, this list
/// in the worst case will be 36 items long (7 344 bytes). Binary Search has a complexity of O(log n).
/// This makes the average and worst case time complexity log 36 or 1.5563, not including the initial sorting time.
/// With the whole buffer for each job, the time complexity would be O(1) however, on grid of 20*16 (320 cells)
/// this would require 65 280 bytes (65kb) of memory, most opitimially as a job wide ReadOnly array but this would
/// mean sorting that as a buffer in massive 65kb entity.
/// 
/// This solution is most optimial for single chunk regeneration rather than whole map regeneration,
/// as you don't need the whole array for a single chunk.
/// Despite it not being most optimial for whole map regeneration, other aspects of the triangulator
/// are more impactful on performance than sorting and accessing the cell buffer by cell index.
/// Especially since there are only about 4-7 calls of this per 16 cells in the chunk.
/// </summary>
public struct HexChunkCellWrapper : IBufferElementData, IComparable<HexChunkCellWrapper>
{
    public HexCellBasic cellBasic;
    public HexCellTerrain cellTerrain;
    public HexCellNeighbours cellNeighbours;
    public int Index => cellBasic.Index;
    public float3 Position => cellBasic.Position;
    public int Elevation => cellTerrain.elevation;

    public int CompareTo(HexChunkCellWrapper other)
    {
        return Index.CompareTo(other.Index);
    }
}

/// <summary>
/// 8 bytes min
/// Component added to cells that need to add HexChunkCellWrapper to a neighbouring chunk
/// </summary>
public struct HexCellChunkNeighbour : IBufferElementData
{
    public Entity value;
    public static implicit operator Entity(HexCellChunkNeighbour v) { return v.value; }
    public static implicit operator HexCellChunkNeighbour(Entity v) { return new HexCellChunkNeighbour { value = v }; }
}

/// <summary>
/// 8 bytes min
/// This component is added to cells when a chunk wants their data provided as a <see cref="HexChunkCellWrapper"/>
/// The <see cref="CellWrappersToChunksJob"/> provides this data to every chunk in its HexChunkCellBuilder buffer, then removes the
/// buffer from the cell.
/// </summary>
public struct HexChunkCellBuilder : IBufferElementData
{
    public Entity Chunk;
    public static implicit operator Entity(HexChunkCellBuilder v) { return v.Chunk; }
    public static implicit operator HexChunkCellBuilder(Entity v) { return new HexChunkCellBuilder { Chunk = v }; }
}

/// <summary>
/// Tagging component used to notify the Triangulator that a chunk needs to be updated, even if it is currently updating.
/// </summary>
public struct HexChunkRefreshRequest : IComponentData { }

/// <summary>
/// used to indicate to the triangulator the chunk is actively refreshing
/// This is removed from the chunk in <see cref="HexChunkMeshApplicatorSystem.CompleteTriangulator(EntityCommandBuffer)"/>
/// </summary>
public struct HexChunkRefresh : IComponentData { }

/// <summary>
/// Tagging component used notify the triangulator a given chunk is currently regenerating, so cannot be refreshed right now.
/// This is removed from the chunk in <see cref="HexChunkMeshApplicatorSystem.CompleteTriangulator(EntityCommandBuffer)"/>
/// 
/// This also performs double duty as a time stamp to make sure meshes and chunks line up correctly after triangulation.
/// </summary>
public struct HexChunkMeshUpdating : IComponentData { public double timeStamp; }

/// <summary>
/// Similar to the chunk refresh request, this notifies the <see cref="HexChunkColliderSystem"/> to update a chunk's collider,
/// even if it is already doing so.
/// </summary>
public struct HexChunkColliderRebuildRequest : IComponentData { }

/// <summary>
/// This component is used to notify the <see cref="HexChunkColliderSystem"/> that the chunk's collider is currently regenerating,
/// So it cannot be regenerated right now
/// </summary>
public struct HexChunkColliderRebuild : IComponentData { }

/// <summary>
/// Provides an entity reference to the chunk of its current collider entity. For proper data disposal,
/// the collider is stoned in its own entity and entirely replaced when the a new one is generated.
/// </summary>
public struct HexChunkColliderReference : IComponentData
{
    public Entity value;
}

/// <summary>
/// Used to notify the Triangulator system a chunk has recieved all the data it needs from the HexCells
/// so can now be triangulated.
/// </summary>
public struct HexChunkCellDataCompleted : IComponentData { }

/// <summary>
/// This is horrific. There should never be more than 1 of these in the Entity World
/// This should only be attached to the <see cref="HexChunkTriangulatorSystem"/>
/// 
/// This component forms three main jobs -
/// (1) It provides an Burst referencable noiseColours NativeArray
/// This is used by <see cref="TriangulateChunksJob"/> in the <see cref="HexChunkTriangulatorSystem"/> and
/// by the <see cref="HexGenerateMapJob"/> in the <see cref="HexMapGeneratorSystem"/>
/// 
/// (2) It provides an Burst referencable hashGrid NativeArray.
/// This is used by <see cref="TriangulateChunksJob"/> in the <see cref="HexChunkTriangulatorSystem"/> 
/// 
/// (3) It provides the meshDataWrappers UnsafeList. This is used to exchange data between <see cref="HexChunkTriangulatorSystem"/>
/// and the <see cref="HexChunkMeshApplicatorSystem"/>. Specifically the Mesh.MeshDataArray instance for a batch of chunks.
/// Because Mesh.MeshDataArray allocates persistantly, it most opitimal to allocate as many meshes at once as possible.
/// 
/// In the case where more than 1 chunk updates we'd need 14 meshes, we want all 14 to be within the same Mesh.MeshDataArray
/// for allocation reasons. This MeshDataArray is stored within a <see cref="MeshDataWrapper"/> which also contains a timestamp - 
/// which is the time the MeshDataArray was allocated The chunks also have this timestamp in
/// their <see cref="HexChunkMeshUpdating"/> component
/// 
/// <see cref="MeshDataWrapper"/> also contains a UnsafeParallelHashSet of type int which contains all the chunks
/// indexes that have meshes in this MeshDataArray.
/// 
/// In order for the native contains to contain other native contains, they must be unsafe variant.
/// The unsafeness means the atomic safety handle can't detect memory leaks caused by them not being disposed,
/// basically just means *really* make sure they are disposed of properly.
/// 
/// </summary>
public struct HexChunkTriangulatorArray : IComponentData
{
    public UnsafeList<MeshDataWrapper> meshDataWrappers;
    public NativeArray<float4> noiseColours;
    public NativeArray<HexHash> hashGrid;
}

/// <summary>
/// This replaces the <see cref="MeshCollider"/> on a chunks collider entity when it is marked for disposal by
/// the <see cref="HexChunkColliderSystem"/>. Then in the <see cref="HexChunkColliderDisposalSystem"/> the the collider is
/// disposed of and the entity destroyed.
/// 
/// <see cref="HexChunkColliderDisposalSystem"/> runs in "FixedUpdate", as it was found it is possible for a collider to be
/// disposed after the broadphase update in fixed update, which caused the PhysicsWorld to throw a null reference exception
/// if you raycast and hit the collider in the <see cref="HexMapEditorSystem"/> as the collider was now null.
/// 
/// </summary>
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

/// <summary>
/// Contains an unsafe list of <see cref="HexChunkColliderQueue"/> which is used by the <see cref="HexChunkColliderSystem"/>
/// to build MeshColliders with the system builds colliders for all the chunks added to 
/// list.
/// </summary>
public struct HexChunkColliderArray : IComponentData
{
    public UnsafeList<HexChunkColliderQueue> colliderQueue;
}

/// <summary>
/// Contains a ReadOnly MeshDataArray and the chunk entities that have terrain meshes in it.
/// 
/// </summary>
public struct HexChunkColliderQueue
{
    public Mesh.MeshDataArray colliderTargetMeshes;
    public UnsafeList<Entity> entities;
}

/// <summary>
/// Tag component for hex chunk collider entities.
/// </summary>
public struct HexChunkCollier : IComponentData { }

/// <summary>
/// ohh a managed component? wow, awful.
/// Yes it is awful and its probably worse than you think it is as it stores a list.
/// But don't worry its supposed to be a singleton instance excuslively for the <see cref="HexChunkColliderSystem"/>
/// 
/// The MeshColliders are created in the background in a system.threading Task. This is because the baking of a collider takes
/// a long time, 100s of miliseconds across 15 threads on my desktop machine. This creates a large frame time spike which appears
/// as a freeze. The C# Jobs system can only run jobs within 1 frame, all Jobs running in a frame must finish in that frame,
/// in order to resolve the Job dependencies for next frame.
/// 
/// So this contains a list of <see cref="HexChunkColliderBatch"/> a component that contains all the information for scheduling and
/// running and completing the Tasks to generate the collider batch for the group of chunks
/// requested in the <see cref="HexChunkColliderQueue"/>
/// </summary>
public class HexChunkColliderBatches : IComponentData
{
    public List<HexChunkColliderBatch> batches = new();
}

/// <summary>
/// A collider batch is simply a <see cref="HexChunkColliderQueue"/> reference of chunks and a MeshDataArray.
/// 
/// When schedule is called, the tasks list gets populated by a bunch of System.Threading Tasks that produce
/// a collider BlobAssetReference (a <see cref="MeshCollider"/> is a IComponentData containing a BlobAssetReference of type Collider)
/// 
/// The <see cref="HexChunkColliderSystem"/> checks every frame while <see cref="HexChunkColliderBatches"/> contains items to see
/// if all the tasks in a batch have completed, when they have it calls GetCompletedData and then disposes <see cref="HexChunkColliderQueue"/>
/// and if the resulting BlobAsset reference is unneeded, it disposes of that too.
/// </summary>
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

    /// <summary>
    /// It is very important to dispose of the data structures in this class as they are all allocated persistantly.
    /// </summary>
    public void Dispose()
    {
        batch.colliderTargetMeshes.Dispose();
        batch.entities.Dispose();
        tasks.Clear();
    }

    /// <summary>
    /// Starts all teh BakeCollider Tasks and locks out the method from being called again
    /// </summary>
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
                tasks.Add(Task<BlobAssetReference<Collider>>.Factory.StartNew(
                    () => BakeCollider(batch.colliderTargetMeshes, localMeshIndex)));
            }
            IndexClosure();
        }

    }

    /// <summary>
    /// When the <see cref="AllCompleted"/> is true, this method is called.
    /// The <see cref="HexChunkColliderSystem"/> goes backwards through the batches list when checking for completed colliders
    /// This ensures the most recently scheduled colliders are applied rather than the most recently completed, and this method
    /// is an integral part of that.
    /// 
    /// </summary>
    /// <param name="colliders"> list of colliders to apply to chunks</param>
    /// <param name="entities"> list of chunks to apply colliders to</param>
    /// <param name="existingChunks">HashSet of chunks that have already been added to the entities list.</param>
    public void GetCompletedData(NativeList<BlobAssetReference<Collider>> colliders,
        NativeList<Entity> entities,
        NativeHashSet<Entity> existingChunks)
    {
        // don't run the method if false
        if (!AllCompleted)
        {
            return;
        }

        // require our batch.entities list to contain *something*
        if (batch.entities.Length > 0)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                Entity chunkEntity = batch.entities[i];

                // if the task completed successfully and the given chunk has not already had a collider queued for application
                // we will add the collider and chunk to the colliders array, entities array and hashset. Queueing it for
                // application to the chunk.
                if (tasks[i].IsCompletedSuccessfully && !existingChunks.Contains(chunkEntity))
                {
                    colliders.Add(tasks[i].Result);

                    entities.Add(chunkEntity);
                    existingChunks.Add(chunkEntity);
                }
                // if the hashset already contains this chunk we need to dispose of the blob, it will never get used.
                else if (existingChunks.Contains(chunkEntity))
                {
                    tasks[i].Result.Dispose();
                }
                else if (tasks[i].IsFaulted) // if this is true log the error and move on, no blob was generated to dispose of
                {
                    Debug.LogException(tasks[i].Exception);
                }
                else if (tasks[i].IsCanceled) // if this is true log the fact it was cancelled and move on.
                {
                    Debug.LogWarning("Collider Task was canceled");
                }
            }
        }
    }

    /// <summary>
    /// This method is what the System.Threaidng Task runs in the background on another thread.
    /// It takes the MeshData at the given index and produces a BlobAssetReference<Collider> (<see cref="MeshCollider"/>)
    /// from it.
    /// 
    /// All code in this method is executed on a different thread in the background, so it doesn't really matter how quickly this
    /// runs.
    /// </summary>
    /// <param name="meshDataArray"> MeshDataArray Containing the mesh we want to turn into a collider </param>
    /// <param name="index"> The Index the meseh we want to bake is stored in the meshDataArray at. </param>
    /// <returns></returns>
    private static BlobAssetReference<Collider> BakeCollider(Mesh.MeshDataArray meshDataArray, in int index)
    {
        // getting data out of a MeshDataArray is a little interesting, you basically copy it to an array of the right size.
        var triangles = new NativeArray<int>(meshDataArray[index].GetSubMesh(0).indexCount,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);

        meshDataArray[index].GetIndices(triangles, 0);

        // except because reasons, the MeshCollider.Create method excpets triangles as int3s not ints
        // this is *fairly* easy to work around, we just create another array 1/3 the size of the index buffer we just copied
        var tempTrianges = new NativeArray<int3>(triangles.Length / 3,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);

        // then using the unsafe untility we can copy the int array into the int3 array as the int3 array takes up the same amount of
        // physical memory space as the int array does.
        unsafe
        {
            UnsafeUtility.MemCpy(tempTrianges.GetUnsafePtr(),
                triangles.GetUnsafePtr(),
                triangles.Length * UnsafeUtility.SizeOf<int>());
        }

        // for some reason get vertices only returns vertices as vector3s
        var vertices = new NativeArray<Vector3>(meshDataArray[index].GetSubMesh(0).vertexCount,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);

        meshDataArray[index].GetVertices(vertices);

        // that is fine though the array can be reinterupted as a float3 array when we parse it to MeshCollider.Create
        // MeshCollider.CreateManaged is a different code path for System.Threading, this ensures that all native contains
        // are created during the collider creation are allocated with persistant memory, as this is the only type of memory other
        // threads can allocate outside of C# Jobs. It also makes sure it disposes of these containers.
        BlobAssetReference<Collider> collider = MeshCollider.CreateManaged(vertices.Reinterpret<float3>(),
            tempTrianges,
            CollisionFilter.Default,
            Unity.Physics.Material.Default);

        // once the collider has been created we can safely dispose of everything and just return the collider.
        triangles.Dispose();
        tempTrianges.Dispose();
        vertices.Dispose();

        return collider;
    }
}