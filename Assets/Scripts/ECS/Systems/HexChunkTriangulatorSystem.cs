using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(HexSystemGroup)), BurstCompile]
public partial struct HexChunkTriangulatorSystem : ISystem
{
    private EntityQuery TriangulatorInitQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.AddComponentData(state.SystemHandle,new HexChunkTriangulatorArray
        {
            meshDataWrappers = new(1,Allocator.Persistent)
        });

        EntityQueryBuilder builder = new(Allocator.Temp);
        NativeArray<EntityQuery> entityQueries = new(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        entityQueries[0] = TriangulatorInitQuery = builder.WithAll<HexChunkCellDataCompleted, HexChunkTag, HexChunkRefresh, HexChunkCellWrapper, HexCellReference, HexChunkMeshEntities>().WithNone<HexChunkMeshUpdating>().Build(ref state);
        builder.Reset();

        entityQueries[1] = builder.WithAll<HexChunkCellDataCompleted, HexChunkTag, HexChunkRefresh, HexChunkCellWrapper, HexCellReference, HexChunkMeshEntities>().Build(ref state);
        builder.Reset();

        entityQueries[2] = builder.WithAll<HexCellReference, HexChunkTag, HexChunkRefresh>().WithNone<HexChunkCellWrapper, HexChunkMeshUpdating>().Build(ref state);
        builder.Reset();

        entityQueries[3] = builder.WithAll<HexChunkRefreshRequest>().WithNone<HexChunkRefresh>().Build(ref state);

        state.RequireAnyForUpdate(entityQueries);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        HexChunkTriangulatorArray data = state.EntityManager.GetComponentData<HexChunkTriangulatorArray>(state.SystemHandle);
        if(data.meshDataWrappers.Length > 0)
        {
            for (int i = 0; i < data.meshDataWrappers.Length; i++)
            {
                data.meshDataWrappers[i].chunksIncluded.Dispose();
                data.meshDataWrappers[i].meshDataArray.Dispose();
            }   
        }
        data.meshDataWrappers.Dispose();
        data.noiseColours.Dispose();
        data.hashGrid.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = GetEndSimEntityCommandBuffer(ref state);
        EntityCommandBuffer.ParallelWriter ecbBegin = GetBeginSimEntityCommandBuffer(ref state);
        EntityCommandBuffer.ParallelWriter ecbPres = GetBeginPresEntityCommandBuffer(ref state);

        if (!TriangulatorInitQuery.IsEmpty)
        {
            Triangulate(ref state,ecbEnd);
        }
        new CellToChunkDataRequestJob { ecbEnd = ecbPres, ecbBegin = ecbBegin }.ScheduleParallel();
        new HandleChunkRefreshRequestJob { ecb = ecbEnd }.ScheduleParallel();
    }

    [BurstCompile]
    private void Triangulate(ref SystemState state, EntityCommandBuffer.ParallelWriter ecbEnd)
    {
        double timeStamp = SystemAPI.Time.ElapsedTime;
        int chunkCount = TriangulatorInitQuery.CalculateEntityCount();
        UnsafeParallelHashSet<int> includedChunks = new(chunkCount, Allocator.Persistent);
        NativeArray<HexChunkTag> chunkIndices = TriangulatorInitQuery.ToComponentDataArray<HexChunkTag>(Allocator.Temp);
        NativeArray<Entity> chunkWitnessComps = TriangulatorInitQuery.ToEntityArray(Allocator.Temp);
        HexChunkMeshUpdating witness = new() { timeStamp = timeStamp };
        NativeParallelHashMap<int2, int> chunkToMesh = new(chunkCount * 7, Allocator.TempJob);
        for (int i = 0, meshIndex = 0; i < chunkIndices.Length; i++)
        {
            includedChunks.Add(chunkIndices[i]);
            ecbEnd.AddComponent(chunkWitnessComps[i].Index, chunkWitnessComps[i], witness);
            for (int m = 0; m < 7; m++, meshIndex++)
            {
                chunkToMesh.Add(new int2(chunkIndices[i].Index, m), meshIndex);
            }
        }
        MeshDataWrapper meshData = new(timeStamp, includedChunks, Mesh.AllocateWritableMeshData(chunkCount * 7));
        RefRW<HexChunkTriangulatorArray> data = SystemAPI.GetComponentRW<HexChunkTriangulatorArray>(state.SystemHandle);
        data.ValueRW.meshDataWrappers.Add(meshData);

        state.Dependency = chunkToMesh.Dispose(new TriangulateChunksJob
        {
            wrapSize = SystemAPI.GetComponent<HexGridBasic>(SystemAPI.GetComponent<HexGridReference>(chunkWitnessComps[0])).wrapSize,
            featureCollections = SystemAPI.GetSingleton<HexFeatureCollectionComponent>(),
            specialPrefabs = SystemAPI.GetSingletonBuffer<HexFeatureSpecialPrefab>().ToNativeArray(Allocator.TempJob),
            hashGrid = data.ValueRW.hashGrid,
            noiseColours = data.ValueRW.noiseColours,
            chunkToMeshMap = chunkToMesh,
            meshDataArray = meshData.meshDataArray,
            ecbEnd = ecbEnd,
        }.ScheduleParallel(state.Dependency));
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEndSimEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetBeginSimEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetBeginPresEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}
