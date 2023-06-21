using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile, UpdateInGroup(typeof(HexSystemGroup))]
public partial struct HexFeatureSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {

        EntityQueryBuilder builder = new(Allocator.Temp);
        NativeArray<EntityQuery> entityQueries = new(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        entityQueries[0] = builder.WithAllRW<HexFeatureSpawnedFeature, HexFeatureRequest>().WithAll<HexFeatureUpdate>().Build(ref state);

        state.RequireAnyForUpdate(entityQueries);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new HexFeatureJob
        {
            specialPrefabs = SystemAPI.GetSingletonBuffer<HexFeatureSpecialPrefab>(true).ToNativeArray(Allocator.TempJob),
            prefabCollections = SystemAPI.GetSingleton<HexFeatureCollectionComponent>(),
            ecb = GetEndSimEntityCommandBuffer(ref state)
        }.ScheduleParallel();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEndSimEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }

}
