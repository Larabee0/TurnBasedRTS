using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(HexSystemGroup)), BurstCompile]
public partial struct HexGridCreatorSystem : ISystem
{
    private EntityQuery HexGridSetNeighbourEntitySet;
    private EntityQuery HexGridSetNeighbourEntitySetGrid;
    private bool latch;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        NativeArray<EntityQuery> entityQueries = new(9, Allocator.Temp);
        entityQueries[0] = HexGridSetNeighbourEntitySet = builder.WithAll<HexGridNeighbourEntitySet, HexCellNeighbours>().Build(ref state);
        builder.Reset();
        entityQueries[1] = HexGridSetNeighbourEntitySetGrid = builder.WithAll<HexGridNeighbourEntitySet, HexGridBasic, HexCellReference>().Build(ref state);
        builder.Reset();
        entityQueries[2] = builder.WithAll<HexChunkTag, HexGridReference>().WithNone<HexCellReference>().Build(ref state);
        builder.Reset();
        entityQueries[3] = builder.WithAllRW<HexCellReference>().WithAll<HexGridBasic, HexGridSortCells>().Build(ref state);
        builder.Reset();
        entityQueries[4] = builder.WithAllRW<HexCellReference>().WithAll<HexGridBasic, HexGridNeighbourEntitySetUnsorted>().Build(ref state);
        builder.Reset();
        entityQueries[5] = builder.WithAllRW<HexCellReference>().WithAll<HexGridBasic, HexGridChunkBuffer, HexGridInitialiseCells>()
            .WithNone<HexGridSortCells, HexGridCreateChunks>().Build(ref state);
        builder.Reset();
        entityQueries[6] = builder.WithAllRW<HexCellNeighbours>().WithAll<HexCellBasic, FindNeighbours>().Build(ref state);
        builder.Reset();
        entityQueries[7] = builder.WithAllRW<HexCellNeighbours>().WithAll<HexGridNeighbourEntitySet, HexCellChunkReference>().Build(ref state);
        builder.Reset();
        entityQueries[8] = builder.WithAll<HexGridUnInitialised, HexGridBasic, HexGridCreateChunks>().Build(ref state);
        state.RequireAnyForUpdate(entityQueries);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);
        HexPrefabsComponent prefabs = SystemAPI.GetSingleton<HexPrefabsComponent>();
        new InitiliseChunksAndColumnsJob
        {
            ecbEnd = ecb,
            HexGridChunkPrefab = prefabs.hexGridChunk,
        }.ScheduleParallel();

        new InstantiateCellsJob
        {
            ecbEnd = ecb,
            HexCellPrefab = prefabs.hexCell
        }.ScheduleParallel();

        new SortHexCellChunkJob
        {
            ecbEnd = ecb,
        }.ScheduleParallel();

        new InitialiseCellsJob
        {
            ecbEnd = ecb,
        }.ScheduleParallel();

        new FindCellNeighboursJob
        {
            ecbEnd = ecb,
        }.ScheduleParallel();

        new SortHexCellIndexJob
        {
            ecbEnd = ecb,
        }.ScheduleParallel();

        if (!HexGridSetNeighbourEntitySetGrid.IsEmpty && !HexGridSetNeighbourEntitySet.IsEmpty)
        {
            state = CompleteNeighbours(ref state, ecb);
            latch = true;
        }
        else if (!HexGridSetNeighbourEntitySetGrid.IsEmpty && latch)
        {
            state = RequestInitialChunkRefresh(ref state, ecb);
        }
    }

    [BurstCompile]
    private SystemState RequestInitialChunkRefresh(ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
    {
        Entity grid = HexGridSetNeighbourEntitySetGrid.GetSingletonEntity();
        DynamicBuffer<HexGridChunkBuffer> chunks = SystemAPI.GetBuffer<HexGridChunkBuffer>(grid);
        for (int i = 0; i < chunks.Length; i++)
        {
            ecb.AddComponent<HexChunkRefreshRequest>(grid.Index, chunks[i].Value);
        }

        ecb.RemoveComponent<HexGridNeighbourEntitySet>(grid.Index, grid);
        latch = false;

        HexGridBasic basicData = SystemAPI.GetComponent<HexGridBasic>(grid);
        ecb.AddComponent<HexGridActive>(grid.Index, grid);
        state.EntityManager.AddComponentData(state.SystemHandle, new HexShaderInitialise { grid = grid, x = basicData.cellCountX, z = basicData.cellCountZ });
        // HexMapUIInterface.Instance.SetMap(grid);
        return state;
    }

    [BurstCompile]
    private SystemState CompleteNeighbours(ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
    {
        Entity grid = HexGridSetNeighbourEntitySetGrid.GetSingletonEntity();
        NativeArray<HexCellReference> hexCells = state.EntityManager.GetBuffer<HexCellReference>(grid, true).ToNativeArray(Allocator.TempJob);

        new CompleteNeighboursJob
        {
            hexCells = hexCells,
            ecbEnd = ecb,
        }.ScheduleParallel();
        return state;
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}
