using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(HexSystemGroup)), BurstCompile]
public partial struct HexCellSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        
        NativeArray<EntityQuery> entityQueries = new(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        
        entityQueries[0] =  builder.WithAll<HexCellSetReferenceInNeighbouringChunk>()
            .WithNone<HexGridNeighbourEntitySet>().Build(ref state);
        
        builder.Reset();
        
        entityQueries[1] = builder
            .WithAll<HexChunkCellBuilder, HexCellBasic, HexCellTerrain, HexCellNeighbours>()
            .WithNone<FindNeighbours, HexGridNeighbourEntitySet>().Build(ref state);

        builder.Reset();

        entityQueries[2] = builder
            .WithAll<HexShaderRefresh, HexCellBasic, HexCellTerrain, HexCellNav>().Build(ref state);

        state.RequireAnyForUpdate(entityQueries);
        
        builder.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = GetEndSimEntityCommandBuffer(ref state);
        EntityCommandBuffer.ParallelWriter ecbBegin = GetBeginSimEntityCommandBuffer(ref state);
        new CellWrappersToChunksJob { ecbEnd = ecbEnd }.ScheduleParallel();
        new ProvideChunkNeighbourCellsJob { ecb = ecbBegin }.ScheduleParallel();
        new ProvideCellsToShader { ecbEnd = ecbEnd, shaderEntity = SystemAPI.GetSingletonEntity<HexShaderSettings>() }.ScheduleParallel();
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
        var ecbSingleton = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}

[BurstCompile,WithNone(typeof(HexGridNeighbourEntitySet))]
public partial struct ProvideChunkNeighbourCellsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexCellSetReferenceInNeighbouringChunk> targetChunks)
    {
        NativeHashSet<Entity> consumedChunks = new(targetChunks.Length, Allocator.Temp);

        for (int i = 0; i < targetChunks.Length; i++)
        {
            if (!consumedChunks.Contains(targetChunks[i].chunk))
            {
                consumedChunks.Add(targetChunks[i].chunk);
                ecb.AppendToBuffer(jobChunkIndex, targetChunks[i].chunk, new HexCellChunkNeighbour { value = main});
            }
        }

        ecb.RemoveComponent<HexCellSetReferenceInNeighbouringChunk>(jobChunkIndex, main);
    }
}

[BurstCompile, WithAll(typeof(HexShaderRefresh))]
public partial struct ProvideCellsToShader : IJobEntity
{
    public Entity shaderEntity;
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNav navigation)
    {
        ecbEnd.AppendToBuffer(jobChunkIndex, shaderEntity, new HexCellShaderRefresh { hexCellNav = navigation, index = basic.Index, terrainTypeIndex = terrain.terrainTypeIndex });
        ecbEnd.AddComponent<HexShaderCellDataComplete>(jobChunkIndex, shaderEntity);
        ecbEnd.RemoveComponent<HexShaderRefresh>(jobChunkIndex, main);
    }
}